using System;
using System.Collections.Generic;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class FileAnalyzerTests
    {
        private static AnalysisOptions Options()
        {
            return new AnalysisOptions
            {
                PerformColorAnalysis = true,
                ColorThreshold = 0.25m,
                CheckImagePixels = true
            };
        }

        private sealed class Harness
        {
            public FakePfcToolProcessFactory Factory = new FakePfcToolProcessFactory();
            public FileAnalyzer Analyzer;
            public int CompletedCount;
            public List<Tuple<int, int>> Progress = new List<Tuple<int, int>>();
            public DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0);

            public Harness(string filename = @"C:\files\a.pdf")
            {
                Analyzer = new FileAnalyzer(filename, Options(), Factory, () => Now);
                Analyzer.AnalysisComplete += a => CompletedCount++;
                Analyzer.ProgressChanged += (a, completed, total) => Progress.Add(Tuple.Create(completed, total));
            }

            public FakePfcToolProcess Process { get { return Factory.Last; } }

            public void GoAndReportOnePage()
            {
                Analyzer.Go();
                Process.EmitStdout("PageCount=1 BookmarkCount=0");
                Process.EmitStdout("Page=1 Size=612.000000,792.000000 Color=0");
            }
        }

        public static IEnumerable<object[]> SignalOrderings()
        {
            yield return new object[] { new[] { "stdout", "stderr", "exit" } };
            yield return new object[] { new[] { "stdout", "exit", "stderr" } };
            yield return new object[] { new[] { "stderr", "stdout", "exit" } };
            yield return new object[] { new[] { "stderr", "exit", "stdout" } };
            yield return new object[] { new[] { "exit", "stdout", "stderr" } };
            yield return new object[] { new[] { "exit", "stderr", "stdout" } };
        }

        [Theory]
        [MemberData(nameof(SignalOrderings))]
        public void CompletesExactlyOnceAfterAllThreeSignals_InAnyOrder(string[] order)
        {
            var h = new Harness();
            h.GoAndReportOnePage();

            // Two of the three signals must not be enough. Completing early is how a
            // failure message got truncated while stderr was still filling.
            h.Process.Finish(0, order[0]);
            Assert.Equal(0, h.CompletedCount);
            h.Process.Finish(0, order[1]);
            Assert.Equal(0, h.CompletedCount);

            h.Process.Finish(0, order[2]);
            Assert.Equal(1, h.CompletedCount);
            Assert.False(h.Analyzer.Failed);
            Assert.False(h.Analyzer.Cancelled);
            Assert.NotNull(h.Analyzer.Result);
            Assert.Equal(1, h.Analyzer.Result.PageCount);
            Assert.Single(h.Analyzer.Result.Pages);
        }

        [Fact]
        public void ANonZeroExitCodeFailsTheFile()
        {
            var h = new Harness();
            h.GoAndReportOnePage();
            h.Process.Finish(1, "stdout", "stderr", "exit");

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("exit code: 1", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void APageCountMismatchFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=3 BookmarkCount=0");
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Expected=3 Received=1", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void AZeroPageCountFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=0 BookmarkCount=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Page count is zero", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void NoHeaderLineFailsTheFileRatherThanThrowing()
        {
            // Reading Result.PageCount unguarded here used to throw
            // NullReferenceException on a thread pool thread, which takes the whole
            // application down.
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("No page count was reported", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void APageLineBeforeTheHeaderFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Page spec came before page count", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void GarbageOnStdoutFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("error: cannot open document");
            h.Process.Finish(1, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("not in an expected format", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void ABlankStdoutLineIsNotAViolation()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=1 BookmarkCount=0");
            h.Process.EmitStdout("");
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.False(h.Analyzer.Failed);
        }

        [Fact]
        public void StderrIsAccumulated()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStderr("cannot open file");
            h.Process.EmitStderr("giving up");
            h.Process.Finish(1, "stdout", "stderr", "exit");

            string errors = h.Analyzer.Errors.ToString();
            Assert.Contains("cannot open file", errors);
            Assert.Contains("giving up", errors);
        }

        [Fact]
        public void AFailureToStartReportsAFailedFileAndStillCompletes()
        {
            // If the process never starts, none of the three signals can ever arrive, so
            // the batch would wait on this analyzer forever.
            // The process is created in the FileAnalyzer constructor, not in Go(), so
            // this has to be set on the process rather than on the factory.
            var h = new Harness();
            h.Process.StartThrows = new InvalidOperationException("The system cannot find the file specified");

            h.Analyzer.Go();

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Could not start pfc-tool.exe", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void CancelMarksTheFileCancelledAndKillsTheProcess()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Analyzer.Cancel();

            Assert.True(h.Analyzer.Cancelled);
            Assert.True(h.Process.Killed);

            h.Process.Finish(1, "stdout", "stderr", "exit");
            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Cancelled);
        }

        [Fact]
        public void TheProcessIsLaunchedWithTheConfiguredPathAndArguments()
        {
            var options = Options();
            options.ToolPath = @"C:\build\pfc-tool.exe";
            var factory = new FakePfcToolProcessFactory();
            var analyzer = new FileAnalyzer(@"C:\files\a.pdf", options, factory);

            analyzer.Go();

            Assert.Equal(@"C:\build\pfc-tool.exe", factory.Last.ToolPath);
            Assert.Equal("\"C:\\files\\a.pdf\" 0.25 1", factory.Last.Arguments);
        }

        [Fact]
        public void ProgressIsThrottledToOneEventEveryFiveHundredMilliseconds()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=4 BookmarkCount=0");

            // The header always reports progress, so the throttle starts from there.
            Assert.Single(h.Progress);
            Assert.Equal(Tuple.Create(0, 4), h.Progress[0]);

            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            Assert.Equal(2, h.Progress.Count);

            // Within the interval: suppressed.
            h.Now = h.Now.AddMilliseconds(400);
            h.Process.EmitStdout("Page=2 Size=612.0,792.0 Color=0");
            Assert.Equal(2, h.Progress.Count);

            // Past the interval: reported.
            h.Now = h.Now.AddMilliseconds(200);
            h.Process.EmitStdout("Page=3 Size=612.0,792.0 Color=0");
            Assert.Equal(3, h.Progress.Count);
            Assert.Equal(Tuple.Create(3, 4), h.Progress[2]);
        }
    }
}
