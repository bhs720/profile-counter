using System;
using System.Collections.Generic;
using System.Threading;
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

            // A regression that dropped the dispose would leak a Process handle per
            // file -- invisible on a handful of files, fatal on a batch of thousands.
            Assert.True(h.Process.Disposed);
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

            // The next task's batch tests Cancelled before Failed, so both must be true
            // when a cancelled process is killed and then exits non-zero.
            Assert.True(h.Analyzer.Failed);
        }

        [Fact]
        public void CancelBeforeGoNeverStartsTheProcessAndCompletesExactlyOnce()
        {
            // AnalysisBatch.Pump adds an analyzer to its running set, then raises
            // FileStarted, and only then calls Go() -- all outside its lock. Cancel()
            // can run on another thread in that window and, before this fix, would kill
            // a process that had not started yet (PfcToolProcess.Kill swallows the
            // InvalidOperationException from an unstarted Process.HasExited), after
            // which Go() started the child anyway and it ran to completion despite the
            // batch having been cancelled.
            var h = new Harness();

            h.Analyzer.Cancel();
            h.Analyzer.Go();

            Assert.False(h.Process.Started);
            Assert.True(h.Analyzer.Cancelled);
            Assert.Equal(1, h.CompletedCount);
        }

        [Fact]
        public void ADuplicatedEndOfFileSignalDoesNotSubstituteForTheMissingThird()
        {
            // signalsOutstanding is one shared counter: without per-signal idempotence, a
            // signal fired twice would decrement it twice and complete the analysis with
            // stderr never having ended -- truncating the very message that would explain
            // a failure.
            var h = new Harness();
            h.GoAndReportOnePage();

            h.Process.EndStdout();
            h.Process.EndStdout();
            h.Process.Exit(0);

            Assert.Equal(0, h.CompletedCount);

            // Completion still requires the real stderr signal.
            h.Process.EndStderr();
            Assert.Equal(1, h.CompletedCount);
        }

        [Fact]
        public void CompletesExactlyOnceUnderConcurrentSignalDelivery()
        {
            // FakePfcToolProcess raises events synchronously, so the other tests in this
            // file never exercise the cross-thread publication of exitCode under real
            // concurrency, or the signalsOutstanding decrement racing across real OS
            // threads. This test fires the three completion signals from three separate
            // threads released together by a Barrier, repeated enough times that a race
            // would show up.
            //
            // What this covers: signalsOutstanding's atomicity under genuine thread
            // scheduling, and that exitCode written by the exit-signal thread is correctly
            // visible to whichever thread's decrement finishes the file. An earlier
            // mutation check confirmed this: downgrading SignalArrived()'s
            // Interlocked.Decrement(ref signalsOutstanding) to a non-atomic decrement made
            // this test fail.
            //
            // What this does NOT cover: Complete()'s
            // Interlocked.CompareExchange(ref completionRaised, 1, 0) guard. Measured with
            // that guard deliberately downgraded to a non-atomic check-then-set, this test
            // detected 0 of 10 runs, at both 200 and 2000 iterations of this loop -- not a
            // gap iteration count can close, because all three signals here land via the
            // same atomic signalsOutstanding decrement, which guarantees exactly one thread
            // ever reaches Complete() per iteration, so completionRaised is never actually
            // contended by this test. See the XML doc on completionRaised in
            // app/core/FileAnalyzer.cs for the real race that guard protects (Go()'s catch
            // block racing SignalArrived() on a failed process start) and why no test in
            // this suite currently reaches it.
            for (int i = 0; i < 200; i++)
            {
                var factory = new FakePfcToolProcessFactory();
                var analyzer = new FileAnalyzer(@"C:\files\a.pdf", Options(), factory);
                int completedCount = 0;
                analyzer.AnalysisComplete += a => Interlocked.Increment(ref completedCount);

                analyzer.Go();
                var process = factory.Last;
                process.EmitStdout("PageCount=1 BookmarkCount=0");
                process.EmitStdout("Page=1 Size=612.000000,792.000000 Color=0");

                int exitCode = (i % 2 == 0) ? 0 : 7;
                var barrier = new Barrier(3);

                var t1 = new Thread(() => { barrier.SignalAndWait(); process.EndStdout(); });
                var t2 = new Thread(() => { barrier.SignalAndWait(); process.EndStderr(); });
                var t3 = new Thread(() => { barrier.SignalAndWait(); process.Exit(exitCode); });

                t1.Start(); t2.Start(); t3.Start();
                t1.Join(); t2.Join(); t3.Join();

                Assert.Equal(1, completedCount);

                if (exitCode == 0)
                {
                    Assert.False(analyzer.Failed);
                }
                else
                {
                    Assert.True(analyzer.Failed);
                    Assert.Contains("exit code: " + exitCode, analyzer.Errors.ToString());
                }
            }
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
