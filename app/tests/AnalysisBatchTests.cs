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
