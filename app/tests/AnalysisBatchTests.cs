using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class AnalysisBatchTests
    {
        private static AnalysisOptions Options()
        {
            return new AnalysisOptions { PerformColorAnalysis = true, ColorThreshold = 0.25m, CheckImagePixels = true };
        }

        private static List<string> Files(int count)
        {
            return Enumerable.Range(1, count).Select(i => @"C:\files\f" + i + ".pdf").ToList();
        }

        /// <summary>Drives one fake process through a clean single-page success.</summary>
        private static void SucceedOnePage(FakePfcToolProcess p)
        {
            p.EmitStdout("PageCount=1 BookmarkCount=0");
            p.EmitStdout("Page=1 Size=612.000000,792.000000 Color=0");
            p.Finish(0, "stdout", "stderr", "exit");
        }

        [Fact]
        public void NeverExceedsTheConcurrencyCap()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(10), Options(), factory, maxConcurrency: 3);

            batch.Start();

            Assert.Equal(3, factory.Created.Count);
        }

        [Fact]
        public void RefillsThePoolAsEachFileCompletes()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(5), Options(), factory, maxConcurrency: 2);

            batch.Start();
            Assert.Equal(2, factory.Created.Count);

            SucceedOnePage(factory.Created[0]);
            Assert.Equal(3, factory.Created.Count);

            SucceedOnePage(factory.Created[1]);
            Assert.Equal(4, factory.Created.Count);
        }

        [Fact]
        public void FinishesOnlyWhenTheQueueAndTheRunningSetAreBothEmpty()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(3), Options(), factory, maxConcurrency: 2);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();
            SucceedOnePage(factory.Created[0]);
            Assert.Equal(0, finished);
            SucceedOnePage(factory.Created[1]);
            Assert.Equal(0, finished);

            // The third and last file.
            SucceedOnePage(factory.Created[2]);
            Assert.Equal(1, finished);
            Assert.True(batch.Finished);
            Assert.Equal(3, batch.Results.Count);
            Assert.Empty(batch.Failures);
        }

        [Fact]
        public void AnEmptyFileListFinishesImmediately()
        {
            // ProcessWindow never finished one of these: NextFile did nothing, no
            // analyzer ever completed, and FinishBatch was never reached. It stayed
            // latent only because MainForm guards against an empty drop.
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(new List<string>(), Options(), factory);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();

            Assert.Equal(1, finished);
            Assert.True(batch.Finished);
            Assert.Empty(factory.Created);
        }

        [Fact]
        public void CancelDrainsTheQueueAndCancelsWhatIsRunning()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(6), Options(), factory, maxConcurrency: 2);

            batch.Start();
            Assert.Equal(2, factory.Created.Count);

            batch.Cancel();

            Assert.True(factory.Created[0].Killed);
            Assert.True(factory.Created[1].Killed);

            // No further files are started as the cancelled ones drain.
            factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            factory.Created[1].Finish(1, "stdout", "stderr", "exit");

            Assert.Equal(2, factory.Created.Count);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void AFailedFileIsRecordedAsAFailureAndTheBatchCarriesOn()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(2), Options(), factory, maxConcurrency: 1);

            batch.Start();
            factory.Created[0].EmitStderr("cannot open document");
            factory.Created[0].Finish(1, "stdout", "stderr", "exit");

            Assert.Single(batch.Failures);
            Assert.Equal(2, factory.Created.Count);

            SucceedOnePage(factory.Created[1]);

            Assert.Single(batch.Results);
            Assert.Single(batch.Failures);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void EventsCarryTheItemThatIdentifiesTheFile()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(2), Options(), factory, maxConcurrency: 1);

            var started = new List<BatchItem>();
            var completed = new List<BatchItem>();
            batch.FileStarted += item => started.Add(item);
            batch.FileCompleted += (item, analyzer) => completed.Add(item);

            batch.Start();
            SucceedOnePage(factory.Created[0]);
            SucceedOnePage(factory.Created[1]);

            Assert.Equal(new[] { 0, 1 }, started.Select(i => i.Index).ToArray());
            Assert.Equal(new[] { 0, 1 }, completed.Select(i => i.Index).ToArray());
            Assert.Equal(@"C:\files\f1.pdf", started[0].Filename);
        }

        [Fact]
        public void ADuplicatePathIsAnalyzedOnce()
        {
            // Over-reporting page totals is not a cosmetic bug: customers price their own
            // customers' work from these numbers.
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(
                new List<string> { @"C:\files\a.pdf", @"C:\files\A.PDF", @"C:\files\b.pdf" },
                Options(), factory, maxConcurrency: 4);

            Assert.Equal(2, batch.Items.Count);

            batch.Start();

            Assert.Equal(2, factory.Created.Count);
        }

        [Fact]
        public void AStormOfSynchronousFailuresDoesNotOverflowTheStack()
        {
            // Go() raises AnalysisComplete on the calling thread when Start throws, so a
            // pump that recursed from its own completion handler would nest one frame set
            // per queued file. A missing pfc-tool.exe and a few hundred dropped files
            // would then overflow the stack, which .NET cannot catch.
            var factory = new FakePfcToolProcessFactory
            {
                StartThrows = new InvalidOperationException("The system cannot find the file specified")
            };
            var batch = new AnalysisBatch(Files(1000), Options(), factory, maxConcurrency: 4);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();

            Assert.Equal(1, finished);
            Assert.Equal(1000, batch.Failures.Count);
            Assert.Empty(batch.Results);
        }

        /// <summary>
        /// Completes 32 fakes concurrently from 32 real threads and checks the pool comes
        /// out consistent: every file is accounted for exactly once and BatchFinished
        /// fires exactly once.
        /// </summary>
        /// <remarks>
        /// This is a smoke test of the lock and the pump handoff under genuine
        /// concurrency, not a reliable regression test for the specific
        /// BatchFinished-before-a-pending-FileCompleted race that <c>pendingCompletions</c>
        /// exists to close. That race's window is only the width of the two delegate
        /// unsubscribes bracketing the FileCompleted call in OnAnalyzerComplete, and a
        /// Barrier does not reliably land two of these 32 threads inside a window that
        /// narrow: with <c>pendingCompletions</c> deliberately reverted, this exact test
        /// still passed 15/15 in isolation, and the same scenario run in-process passed
        /// 2000/2000. Only an artificial delay inserted into OnAnalyzerComplete reliably
        /// exposed the bug (200/200 violations with a 1ms delay, 0/200 without it) -- that
        /// experiment, not this test, is the real evidence the invariant holds. Do not add
        /// a delay or any other hook to production code to make this test bite; a test
        /// that is honest about what it does and does not prove is worth more than one
        /// that is believed to prove more than it does.
        /// </remarks>
        [Fact]
        public void ConcurrentCompletionSmokeTest()
        {
            const int n = 32;
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(n), Options(), factory, maxConcurrency: n);

            int batchFinishedCount = 0;
            int completedAfterFinish = 0;

            batch.BatchFinished += () => Interlocked.Increment(ref batchFinishedCount);
            batch.FileCompleted += (item, analyzer) =>
            {
                if (Volatile.Read(ref batchFinishedCount) != 0)
                    Interlocked.Increment(ref completedAfterFinish);
            };

            batch.Start();
            Assert.Equal(n, factory.Created.Count);

            // A barrier maximizes how many of the N completions actually land at the same
            // moment. It widens the odds of hitting the OnAnalyzerComplete race described
            // above, but -- per the class remarks -- not reliably: treat a pass here as a
            // consistency check, not as proof the race is closed.
            var barrier = new Barrier(n);
            var threads = new Thread[n];
            for (int i = 0; i < n; i++)
            {
                var process = factory.Created[i];
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    process.Finish(1, "stdout", "stderr", "exit");
                });
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            Assert.Equal(n, factory.Created.Count);
            Assert.Equal(n, batch.Results.Count + batch.Failures.Count);
            Assert.Equal(1, batchFinishedCount);
            Assert.Equal(0, completedAfterFinish);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void AThrowingFileCompletedHandlerDoesNotWedgeTheBatch()
        {
            // pendingCompletions must come back down even when a FileCompleted subscriber
            // throws, or the finish condition in Pump() can never be satisfied again.
            // Confirmed by reverting the `finally` around the decrement and re-running this
            // exact scenario: without it, BatchFinished is never raised and Finished stays
            // false, which in the GUI makes the window impossible to close (FormClosing
            // sets e.Cancel = true while !batchFinished).
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(2), Options(), factory, maxConcurrency: 2);

            int batchFinishedRaised = 0;
            batch.BatchFinished += () => batchFinishedRaised++;

            bool first = true;
            batch.FileCompleted += (item, analyzer) =>
            {
                if (first) { first = false; throw new InvalidOperationException("boom"); }
            };

            batch.Start();
            Assert.Equal(2, factory.Created.Count);

            Assert.Throws<InvalidOperationException>(() => factory.Created[0].Finish(1, "stdout", "stderr", "exit"));
            factory.Created[1].Finish(1, "stdout", "stderr", "exit");

            Assert.Equal(1, batchFinishedRaised);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void ProgressIsForwardedWithTheItem()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(1), Options(), factory, maxConcurrency: 1);

            var progress = new List<Tuple<int, int, int>>();
            batch.FileProgress += (item, completed, total) => progress.Add(Tuple.Create(item.Index, completed, total));

            batch.Start();
            factory.Created[0].EmitStdout("PageCount=4 BookmarkCount=0");

            Assert.Single(progress);
            Assert.Equal(Tuple.Create(0, 0, 4), progress[0]);
        }
    }
}
