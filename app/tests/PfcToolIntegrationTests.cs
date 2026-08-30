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

            var batch = new AnalysisBatch(files, Options(), new PfcToolProcessFactory(), new ThreadPoolDispatcher(), maxConcurrency: 2);

            using (var done = new ManualResetEventSlim(false))
            {
                batch.BatchFinished += () => done.Set();
                batch.Start();

                Assert.True(done.Wait(TimeSpan.FromSeconds(180)), "The batch did not finish within 180 seconds.");
            }

            Assert.Equal(files.Length, batch.Results.Count + batch.Failures.Count);
            Assert.Empty(batch.Failures);
        }

        /// <summary>
        /// Marshals onto a thread pool thread, never inline. There is no UI thread to
        /// post onto here, and unlike <see cref="QueueDispatcher"/> this test has nothing
        /// driving a manual drain -- it just blocks on a wait handle while real
        /// pfc-tool.exe child processes run and complete on their own thread pool threads.
        /// </summary>
        private sealed class ThreadPoolDispatcher : IDispatcher
        {
            public void Post(Action action)
            {
                ThreadPool.QueueUserWorkItem(_ => action());
            }
        }
    }
}
