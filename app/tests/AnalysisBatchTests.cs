using System;
using System.Collections.Generic;
using System.Linq;
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

        private sealed class Harness
        {
            public readonly FakePfcToolProcessFactory Factory = new FakePfcToolProcessFactory();
            public readonly QueueDispatcher Dispatcher = new QueueDispatcher();
            public readonly AnalysisBatch Batch;

            public Harness(IEnumerable<string> files, int maxConcurrency)
            {
                Batch = new AnalysisBatch(files, Options(), Factory, Dispatcher, maxConcurrency);
            }

            /// <summary>Starts the batch and drains the dispatcher.</summary>
            public void Start()
            {
                Batch.Start();
                Dispatcher.RunUntilIdle();
            }

            /// <summary>Completes one fake process, then drains the posted completion.</summary>
            public void Complete(int index)
            {
                SucceedOnePage(Factory.Created[index]);
                Dispatcher.RunUntilIdle();
            }
        }

        [Fact]
        public void NeverExceedsTheConcurrencyCap()
        {
            var h = new Harness(Files(10), maxConcurrency: 3);
            h.Start();

            Assert.Equal(3, h.Factory.Created.Count);
        }

        [Fact]
        public void RefillsThePoolAsEachFileCompletes()
        {
            var h = new Harness(Files(5), maxConcurrency: 2);
            h.Start();
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(0);
            Assert.Equal(3, h.Factory.Created.Count);

            h.Complete(1);
            Assert.Equal(4, h.Factory.Created.Count);
        }

        [Fact]
        public void FinishesOnlyWhenTheQueueAndTheRunningSetAreBothEmpty()
        {
            var h = new Harness(Files(3), maxConcurrency: 2);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;

            h.Start();
            h.Complete(0);
            Assert.Equal(0, finished);
            h.Complete(1);
            Assert.Equal(0, finished);

            h.Complete(2);
            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Equal(3, h.Batch.Results.Count);
            Assert.Empty(h.Batch.Failures);
        }

        [Fact]
        public void AnEmptyFileListFinishesImmediately()
        {
            // ProcessWindow never finished one of these: NextFile did nothing, no analyzer
            // ever completed, and FinishBatch was never reached. It stayed latent only
            // because MainForm guards against an empty drop.
            var h = new Harness(new List<string>(), maxConcurrency: 0);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;

            h.Start();

            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Empty(h.Factory.Created);
        }

        [Fact]
        public void BatchFinishedIsAlwaysRaisedAfterEveryFileCompleted()
        {
            // The ordering pendingCompletions used to defend is now structural:
            // FileCompleted and the Pump that may raise BatchFinished run in the same
            // dispatched action, in that order.
            var h = new Harness(Files(3), maxConcurrency: 3);
            var order = new List<string>();
            h.Batch.FileCompleted += (item, analyzer) => order.Add("completed:" + item.Index);
            h.Batch.BatchFinished += () => order.Add("finished");

            h.Start();
            h.Complete(0);
            h.Complete(1);
            h.Complete(2);

            Assert.Equal("finished", order.Last());
            Assert.Equal(3, order.Count(x => x.StartsWith("completed:")));
        }

        [Fact]
        public void CancelDrainsTheQueueAndCancelsWhatIsRunning()
        {
            var h = new Harness(Files(6), maxConcurrency: 2);
            h.Start();
            Assert.Equal(2, h.Factory.Created.Count);

            h.Batch.Cancel();
            h.Dispatcher.RunUntilIdle();

            Assert.True(h.Factory.Created[0].Killed);
            Assert.True(h.Factory.Created[1].Killed);

            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Factory.Created[1].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Equal(2, h.Factory.Created.Count);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void CancellingFromWithinFileStartedStillKillsTheProcessAndFinishes()
        {
            // BEHAVIOUR CHANGE, deliberate. Cancel() now posts, so it no longer takes
            // effect inside the caller's stack frame: the process for the file being
            // started does start, and is then killed. The guarantees that matter are
            // unchanged -- no orphaned child, the batch finishes, and the cancelled file
            // is not counted in Results.
            var h = new Harness(Files(1), maxConcurrency: 1);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;
            h.Batch.FileStarted += item => h.Batch.Cancel();

            h.Start();

            Assert.Single(h.Factory.Created);
            Assert.True(h.Factory.Created[0].Started);
            Assert.True(h.Factory.Created[0].Killed);

            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Empty(h.Batch.Results);
        }

        [Fact]
        public void AFailedFileIsRecordedAsAFailureAndTheBatchCarriesOn()
        {
            var h = new Harness(Files(2), maxConcurrency: 1);
            h.Start();

            h.Factory.Created[0].EmitStderr("cannot open document");
            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Single(h.Batch.Failures);
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(1);

            Assert.Single(h.Batch.Results);
            Assert.Single(h.Batch.Failures);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void EventsCarryTheItemThatIdentifiesTheFile()
        {
            var h = new Harness(Files(2), maxConcurrency: 1);
            var started = new List<BatchItem>();
            var completed = new List<BatchItem>();
            h.Batch.FileStarted += item => started.Add(item);
            h.Batch.FileCompleted += (item, analyzer) => completed.Add(item);

            h.Start();
            h.Complete(0);
            h.Complete(1);

            Assert.Equal(new[] { 0, 1 }, started.Select(i => i.Index).ToArray());
            Assert.Equal(new[] { 0, 1 }, completed.Select(i => i.Index).ToArray());
            Assert.Equal(@"C:\files\f1.pdf", started[0].Filename);
        }

        [Fact]
        public void ADuplicatePathIsAnalyzedOnce()
        {
            // Over-reporting page totals is not cosmetic: customers price their own
            // customers' work from these numbers.
            var factory = new FakePfcToolProcessFactory();
            var dispatcher = new QueueDispatcher();
            var batch = new AnalysisBatch(
                new List<string> { @"C:\files\a.pdf", @"C:\files\A.PDF", @"C:\files\b.pdf" },
                Options(), factory, dispatcher, maxConcurrency: 4);

            Assert.Equal(2, batch.Items.Count);

            batch.Start();
            dispatcher.RunUntilIdle();

            Assert.Equal(2, factory.Created.Count);
        }

        [Fact]
        public void AStormOfSynchronousFailuresDoesNotOverflowTheStack()
        {
            // Go() raises AnalysisComplete on the calling thread when Start throws. The
            // completion is posted, so it cannot re-enter the pump -- this is now
            // structural rather than caught by a flag.
            var factory = new FakePfcToolProcessFactory
            {
                StartThrows = new InvalidOperationException("The system cannot find the file specified")
            };
            var dispatcher = new QueueDispatcher();
            var batch = new AnalysisBatch(Files(1000), Options(), factory, dispatcher, maxConcurrency: 4);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();
            dispatcher.RunUntilIdle();

            Assert.Equal(1, finished);
            Assert.Equal(1000, batch.Failures.Count);
            Assert.Empty(batch.Results);
        }

        [Fact]
        public void AThrowingFileCompletedHandlerDoesNotStallThePool()
        {
            // THREE files at concurrency ONE. The previous version of this test used two
            // files at concurrency two, where the other analyzer's completion did the
            // pumping -- so it passed even with Pump() outside the finally. With a queue
            // behind the pool, a skipped Pump is permanent.
            var h = new Harness(Files(3), maxConcurrency: 1);
            int batchFinishedRaised = 0;
            h.Batch.BatchFinished += () => batchFinishedRaised++;

            bool first = true;
            h.Batch.FileCompleted += (item, analyzer) =>
            {
                if (first) { first = false; throw new InvalidOperationException("boom"); }
            };

            h.Start();
            Assert.Single(h.Factory.Created);

            SucceedOnePage(h.Factory.Created[0]);
            Assert.Throws<InvalidOperationException>(() => h.Dispatcher.RunUntilIdle());

            // The pool must have refilled despite the throw.
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(1);
            h.Complete(2);

            Assert.Equal(1, batchFinishedRaised);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void AThrowingFileStartedHandlerDoesNotStrandItsAnalyzer()
        {
            // An analyzer added to `running` must always be started, or nothing will ever
            // deliver a completion for it and the batch can never finish. Go() runs in a
            // finally for exactly this reason.
            var h = new Harness(Files(2), maxConcurrency: 1);
            int batchFinishedRaised = 0;
            h.Batch.BatchFinished += () => batchFinishedRaised++;

            bool first = true;
            h.Batch.FileStarted += item =>
            {
                if (first) { first = false; throw new InvalidOperationException("boom"); }
            };

            h.Batch.Start();
            Assert.Throws<InvalidOperationException>(() => h.Dispatcher.RunUntilIdle());

            // Started despite the throwing subscriber.
            Assert.Single(h.Factory.Created);
            Assert.True(h.Factory.Created[0].Started);

            h.Complete(0);
            h.Complete(1);

            Assert.Equal(1, batchFinishedRaised);
            Assert.True(h.Batch.Finished);
            Assert.Equal(2, h.Batch.Results.Count);
        }

        [Fact]
        public void ProgressIsForwardedWithTheItem()
        {
            var h = new Harness(Files(1), maxConcurrency: 1);
            var progress = new List<Tuple<int, int, int>>();
            h.Batch.FileProgress += (item, completed, total) => progress.Add(Tuple.Create(item.Index, completed, total));

            h.Start();
            h.Factory.Created[0].EmitStdout("PageCount=4 BookmarkCount=0");
            h.Dispatcher.RunUntilIdle();

            Assert.Single(progress);
            Assert.Equal(Tuple.Create(0, 0, 4), progress[0]);
        }
    }
}
