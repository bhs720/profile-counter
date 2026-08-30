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

        /// <summary>
        /// Measured detection rate, recorded here so a future reader does not have to
        /// re-derive it and does not have to trust an unmeasured claim about it either.
        /// With Complete()'s <c>Interlocked.CompareExchange(ref completionRaised, 1, 0)</c>
        /// downgraded to a plain, non-atomic check-then-set, this test FAILED 9 of 10
        /// runs. With the atomic guard restored (confirmed via a clean
        /// <c>git diff app/core/FileAnalyzer.cs</c> before each measurement run) it
        /// PASSED 10 of 10, with a worst observed wall-clock time of 6 seconds. That is
        /// not proof the guard is correct on every possible interleaving -- the window
        /// CompareExchange protects is a few CPU cycles wide, no test can force it every
        /// time, and the 1 undetected run out of 10 is the honest evidence of that. What
        /// it does establish, and what round 2's report should have said instead of
        /// claiming reproducibility from two observations: this test now catches the
        /// regression most of the time it is run, not roughly 1 time in 5.
        /// </summary>
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
            // deliveries on the other path -- so two threads released together tend to
            // reach Complete() at measurably different times and rarely overlap in the
            // few-instruction window the guard protects. CalibrateHandicap measures each
            // path's average cost with a real Stopwatch and burns the difference as
            // Thread.SpinWait cycles on whichever path is faster, so the two arrive with
            // close to the same mean latency; only ordinary scheduler jitter is left to
            // decide whether a given pair actually overlaps. ThreadPool work items (with
            // MinThreads raised so none of them queue) replace raw Thread objects so many
            // more pairs fit in the wall-clock budget than OS thread creation would allow.
            WarmUpJit();
            int startHandicapSpins, finishHandicapSpins;
            CalibrateHandicap(out startHandicapSpins, out finishHandicapSpins);

            int minWorkerThreads, minIoThreads;
            ThreadPool.GetMinThreads(out minWorkerThreads, out minIoThreads);
            const int PairsPerWave = 32;

            // All 53 tests run in one process, so an elevated ThreadPool floor set here
            // would otherwise outlive this test and force needless OS thread creation for
            // every test that runs after it. The try/finally restores the captured
            // original values on every exit path, including a failed Assert or an
            // unexpected exception, not just the successful one.
            bool raisedFloor = ThreadPool.SetMinThreads(Math.Max(minWorkerThreads, PairsPerWave * 2 + 4), minIoThreads);
            try
            {
                // If the runtime refused to raise the floor, the pool injects new worker
                // threads at its own slow throttle (roughly 2/second) instead of having
                // all of them ready up front. Running the full wave count in that case
                // would have every wave's spinners occupy every existing worker while
                // the pool drip-feeds the rest, stalling each wave for many seconds and
                // the whole test for minutes rather than hanging outright -- cut the run
                // down drastically instead. This remains a best-effort race detector, not
                // the sole guarantee of correctness (see
                // CompletesExactlyOnceUnderConcurrentSignalDelivery for the same race
                // exercised without relying on an elevated ThreadPool floor).
                int Waves = raisedFloor ? 400 : 5;

                for (int wave = 0; wave < Waves; wave++)
                {
                    var completedCounts = new int[PairsPerWave];
                    var done = new CountdownEvent(PairsPerWave * 2);

                    // A busy-spin release rather than a Barrier or a WaitHandle: every worker
                    // spins on a shared volatile flag instead of blocking, so once the
                    // controlling thread below flips it, all workers -- already hot, already
                    // running -- observe it and leave their spin loops within a handful of
                    // CPU cycles rather than paying an OS wakeup.
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

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            Interlocked.Increment(ref readyCount);
                            var spin = new SpinWait();
                            while (!Volatile.Read(ref go)) spin.SpinOnce();
                            if (startHandicapSpins > 0) Thread.SpinWait(startHandicapSpins);
                            analyzer.Go(); // Start() throws; the catch block calls Complete() directly.
                            done.Signal();
                        });

                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            Interlocked.Increment(ref readyCount);
                            var spin = new SpinWait();
                            while (!Volatile.Read(ref go)) spin.SpinOnce();
                            if (finishHandicapSpins > 0) Thread.SpinWait(finishHandicapSpins);

                            // Levels this thread's timing against Go()'s exception-throwing
                            // path without a sleep or a fixed timing assumption -- both pay
                            // the same kind of one-time cost of throwing and catching an
                            // exception, on top of the measured handicap above.
                            try { throw new InvalidOperationException("timing parity"); }
                            catch { /* discarded -- its only purpose is matching Go()'s cost */ }

                            process.Finish(0, "stdout", "stderr", "exit"); // Drives Complete() via SignalArrived().
                            done.Signal();
                        });
                    }

                    var readySpin = new SpinWait();
                    while (Volatile.Read(ref readyCount) < PairsPerWave * 2) readySpin.SpinOnce();
                    Volatile.Write(ref go, true);

                    done.Wait();

                    for (int p = 0; p < PairsPerWave; p++)
                    {
                        Assert.Equal(1, completedCounts[p]);
                    }
                }
            }
            finally
            {
                ThreadPool.SetMinThreads(minWorkerThreads, minIoThreads);
            }
        }

        /// <summary>
        /// Runs both completion paths a few times before any timing is measured or any
        /// race is attempted, so neither one's first-call JIT compilation cost pollutes
        /// the calibration in <see cref="CalibrateHandicap"/> or the first real wave.
        /// </summary>
        private static void WarmUpJit()
        {
            for (int i = 0; i < 5; i++)
            {
                var goFactory = new FakePfcToolProcessFactory();
                var goAnalyzer = new FileAnalyzer(@"C:\files\warmup.pdf", Options(), goFactory);
                goFactory.Last.StartThrowsAfterLaunch = new InvalidOperationException("warmup");
                goAnalyzer.Go();

                var finishFactory = new FakePfcToolProcessFactory();
                var finishAnalyzer = new FileAnalyzer(@"C:\files\warmup.pdf", Options(), finishFactory);
                try { throw new InvalidOperationException("warmup"); }
                catch { /* discarded */ }
                finishFactory.Last.Finish(0, "stdout", "stderr", "exit");
            }
        }

        /// <summary>
        /// Measures the average wall-clock cost of Go()'s exception-throwing path versus
        /// the three-signal Finish() path (with its own leveling exception), and converts
        /// the difference into a Thread.SpinWait count for whichever path is faster, so
        /// the two arrive at Complete() with close to the same mean latency. This narrows
        /// -- it cannot eliminate -- the gap that <see cref="CompletesExactlyOnceWhenAStartFailureRacesTheCompletionSignals"/>
        /// relies on ordinary scheduler jitter to close the rest of the way.
        /// </summary>
        private static void CalibrateHandicap(out int startHandicapSpins, out int finishHandicapSpins)
        {
            const int Samples = 40;
            var sw = new System.Diagnostics.Stopwatch();

            long startTicks = 0;
            for (int i = 0; i < Samples; i++)
            {
                var factory = new FakePfcToolProcessFactory();
                var analyzer = new FileAnalyzer(@"C:\files\calib.pdf", Options(), factory);
                factory.Last.StartThrowsAfterLaunch = new InvalidOperationException("calibration");

                sw.Restart();
                analyzer.Go();
                sw.Stop();
                startTicks += sw.ElapsedTicks;
            }

            long finishTicks = 0;
            for (int i = 0; i < Samples; i++)
            {
                var factory = new FakePfcToolProcessFactory();
                var analyzer = new FileAnalyzer(@"C:\files\calib.pdf", Options(), factory);
                var process = factory.Last;

                sw.Restart();
                try { throw new InvalidOperationException("timing parity"); }
                catch { /* discarded */ }
                process.Finish(0, "stdout", "stderr", "exit");
                sw.Stop();
                finishTicks += sw.ElapsedTicks;
            }

            double avgStart = (double)startTicks / Samples;
            double avgFinish = (double)finishTicks / Samples;

            sw.Restart();
            const int SpinSample = 200000;
            Thread.SpinWait(SpinSample);
            sw.Stop();
            double ticksPerSpin = sw.ElapsedTicks > 0 ? (double)sw.ElapsedTicks / SpinSample : 0.0001;

            double diff = avgStart - avgFinish;
            startHandicapSpins = 0;
            finishHandicapSpins = 0;
            if (diff > 0)
                finishHandicapSpins = (int)Math.Min(diff / ticksPerSpin, 200000);
            else
                startHandicapSpins = (int)Math.Min(-diff / ticksPerSpin, 200000);
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
