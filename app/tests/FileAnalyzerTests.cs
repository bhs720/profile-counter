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
            // file never exercise the completionRaised CompareExchange or the cross-thread
            // publication of exitCode under real concurrency. This test fires the three
            // completion signals from three separate threads released together by a
            // Barrier, repeated enough times that a race would show up.
            for (int i = 0; i < 2000; i++)
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
        public void CompletesExactlyOnceWhenAStartFailureRacesTheCompletionSignals()
        {
            // Complete() has two callers: SignalArrived() when the signal counter reaches
            // zero, and Go()'s catch block when Start() throws. Those two calls to
            // Complete() genuinely race in production: PfcToolProcess.Start() calls
            // process.Start() and then BeginErrorReadLine()/BeginOutputReadLine(). If
            // process.Start() succeeds but a later step throws, the child is already live
            // and EnableRaisingEvents is already set -- so Exited can fire on a thread
            // pool thread while Go()'s catch is still running Complete() on the calling
            // thread. This is the race completionRaised's CompareExchange exists to
            // survive; the earlier concurrency test never exercised it because its two
            // Complete() calls both went through the same SignalArrived() decrement path,
            // where the atomic counter alone already guarantees only one thread reaches
            // zero.
            //
            // The two paths to Complete() are not the same length -- Go()'s path throws
            // and catches a real exception, which costs far more than the three signal
            // deliveries on the other path -- so a single isolated pair of threads tends
            // to reach Complete() at consistently different times and rarely overlaps in
            // the few-instruction window the guard protects. Running many pairs per wave,
            // released from one shared barrier, relies on ordinary OS scheduler
            // contention (preemption, core migration) across the whole wave to land some
            // pair's two Complete() calls together, without asserting anything about
            // timing.
            const int Waves = 250;
            const int PairsPerWave = 12;

            for (int wave = 0; wave < Waves; wave++)
            {
                var completedCounts = new int[PairsPerWave];
                var threads = new List<Thread>();

                // A busy-spin release rather than a Barrier: every worker thread spins on
                // a shared volatile flag instead of blocking on a wait handle, so once the
                // controlling thread below flips it, all workers observe it and leave
                // their spin loops within a handful of CPU cycles -- no OS wakeup latency
                // to desynchronize them.
                int readyCount = 0;
                bool go = false;

                for (int p = 0; p < PairsPerWave; p++)
                {
                    int index = p;
                    var factory = new FakePfcToolProcessFactory();
                    var analyzer = new FileAnalyzer(@"C:\files\a.pdf", Options(), factory);
                    analyzer.AnalysisComplete += a => Interlocked.Increment(ref completedCounts[index]);

                    var process = factory.Last;
                    process.StartThrowsAfterLaunch = new InvalidOperationException("reader failed to attach");

                    threads.Add(new Thread(() =>
                    {
                        Interlocked.Increment(ref readyCount);
                        var spin = new SpinWait();
                        while (!Volatile.Read(ref go)) spin.SpinOnce();
                        analyzer.Go(); // Start() throws; the catch block calls Complete() directly.
                    }));

                    threads.Add(new Thread(() =>
                    {
                        Interlocked.Increment(ref readyCount);
                        var spin = new SpinWait();
                        while (!Volatile.Read(ref go)) spin.SpinOnce();

                        // Levels this thread's timing against Go()'s exception-throwing
                        // path without a sleep or a timing assumption -- both pay the
                        // same one-time cost of throwing and catching an exception.
                        try { throw new InvalidOperationException("timing parity"); }
                        catch { /* discarded -- its only purpose is matching Go()'s cost */ }

                        process.Finish(0, "stdout", "stderr", "exit"); // Drives Complete() via SignalArrived().
                    }));
                }

                foreach (var t in threads) t.Start();

                var readySpin = new SpinWait();
                while (Volatile.Read(ref readyCount) < PairsPerWave * 2) readySpin.SpinOnce();
                Volatile.Write(ref go, true);

                foreach (var t in threads) t.Join();

                for (int p = 0; p < PairsPerWave; p++)
                {
                    Assert.Equal(1, completedCounts[p]);
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
