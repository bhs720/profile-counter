using System;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// Runs the real pfc-tool.exe and asserts protocol conformance only.
    /// <para>
    /// Deliberately nothing about colour classification: the pfc-regression skill
    /// verifies that properly, against a shipped baseline over a corpus of real files.
    /// Repeating it here would produce a test that fails for reasons unrelated to the C#
    /// code -- a mupdf version bump, a patch refresh -- and that is not what this suite
    /// is for.
    /// </para>
    /// </summary>
    public class PfcToolIntegrationTests
    {
        private readonly ITestOutputHelper output;

        public PfcToolIntegrationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static AnalysisOptions Options()
        {
            return new AnalysisOptions
            {
                ToolPath = RepoLayout.PfcToolPath,
                PerformColorAnalysis = true,
                ColorThreshold = 0.25m,
                CheckImagePixels = true
            };
        }

        /// <summary>
        /// Runs one file to completion. FileAnalyzer has no timeout by design; a test
        /// host needs one so that a hang is a failing test rather than a hung suite.
        /// </summary>
        private static FileAnalyzer Analyze(string path)
        {
            var analyzer = new FileAnalyzer(path, Options(), new PfcToolProcessFactory());
            using (var done = new ManualResetEventSlim(false))
            {
                analyzer.AnalysisComplete += a => done.Set();
                analyzer.Go();

                Assert.True(done.Wait(TimeSpan.FromSeconds(120)),
                    "Analysis of " + path + " did not complete within 120 seconds.");
            }

            return analyzer;
        }

        [PfcToolFact]
        public void EverySamplePdfConformsToTheProtocol()
        {
            string[] files = Directory.GetFiles(RepoLayout.TestFilesDirectory, "*.pdf");
            Assert.NotEmpty(files);

            foreach (string file in files)
            {
                output.WriteLine("Analyzing " + Path.GetFileName(file));

                var analyzer = Analyze(file);

                Assert.False(analyzer.Cancelled, file);
                Assert.False(analyzer.Failed, file + " failed: " + analyzer.Errors);
                Assert.NotNull(analyzer.Result);
                Assert.True(analyzer.Result.PageCount > 0, file + " reported no pages.");

                // The count in the header and the number of Page= lines must agree.
                Assert.Equal(analyzer.Result.PageCount, analyzer.Result.Pages.Count);

                foreach (var page in analyzer.Result.Pages)
                {
                    Assert.True(page.Width > 0m, file + " page " + page.PageNumber + " has width " + page.Width);
                    Assert.True(page.Height > 0m, file + " page " + page.PageNumber + " has height " + page.Height);
                }
            }
        }

        [PfcToolFact]
        public void AFileThatDoesNotExistFailsCleanly()
        {
            string missing = Path.Combine(RepoLayout.TestFilesDirectory, "no-such-file-" + Guid.NewGuid().ToString("N") + ".pdf");

            var analyzer = Analyze(missing);

            Assert.True(analyzer.Failed);
            Assert.NotEqual(string.Empty, analyzer.Errors.ToString().Trim());
        }

        [PfcToolFact]
        public void ABatchOfEverySampleFileFinishes()
        {
            string[] files = Directory.GetFiles(RepoLayout.TestFilesDirectory, "*.pdf");
            Assert.NotEmpty(files);

            // AnalysisBatch is single-threaded by contract: it carries no lock of its own,
            // and its correctness depends entirely on its dispatcher running one posted
            // action at a time on a single logical thread. A dispatcher that ran actions on
            // arbitrary thread pool threads (e.g. via ThreadPool.QueueUserWorkItem) would
            // let two real analyzers complete at once and mutate `running`/`results` and
            // call Pump() concurrently -- exactly the race the dispatcher seam exists to
            // rule out. So this test drains QueueDispatcher from its own thread instead of
            // blocking on a wait handle: a wait handle has nothing to run the posted
            // actions, but the child processes still complete on their own thread pool
            // threads, so RunUntilIdle() is called in a loop until the batch reports
            // finished, sleeping briefly whenever nothing is queued yet.
            var dispatcher = new QueueDispatcher();
            var batch = new AnalysisBatch(files, Options(), new PfcToolProcessFactory(), dispatcher, maxConcurrency: 2);

            bool finished = false;
            batch.BatchFinished += () => finished = true;
            batch.Start();

            var deadline = DateTime.UtcNow.AddSeconds(180);
            while (!finished && DateTime.UtcNow < deadline)
            {
                if (dispatcher.RunUntilIdle() == 0)
                    Thread.Sleep(10); // nothing queued yet; a child process is still working
            }

            Assert.True(finished, "The batch did not finish within 180 seconds.");

            Assert.Equal(files.Length, batch.Results.Count + batch.Failures.Count);
            Assert.Empty(batch.Failures);
        }
    }
}
