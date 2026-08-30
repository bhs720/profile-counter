using System.Collections.Generic;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class QueueDispatcherTests
    {
        [Fact]
        public void PostDoesNotRunTheActionInline()
        {
            // The whole threading design rests on this. AnalysisBatch drops its
            // re-entrancy flag because a synchronously-completing analyzer posts and
            // returns instead of re-entering the pump. A dispatcher that ran actions
            // inline would silently restore the recursion the flag used to catch.
            var dispatcher = new QueueDispatcher();
            bool ran = false;

            dispatcher.Post(() => ran = true);

            Assert.False(ran);
            Assert.Equal(1, dispatcher.PendingCount);

            dispatcher.RunUntilIdle();
            Assert.True(ran);
        }

        [Fact]
        public void RunUntilIdleDrainsActionsQueuedByOtherActions()
        {
            // Pump() posts nothing itself, but a completion posted from inside a running
            // action must still be drained by the same RunUntilIdle call, or tests would
            // have to guess how many times to pump.
            var dispatcher = new QueueDispatcher();
            var order = new List<string>();

            dispatcher.Post(() =>
            {
                order.Add("first");
                dispatcher.Post(() => order.Add("second"));
            });

            int ran = dispatcher.RunUntilIdle();

            Assert.Equal(2, ran);
            Assert.Equal(new[] { "first", "second" }, order);
        }

        [Fact]
        public void ActionsRunInPostOrder()
        {
            var dispatcher = new QueueDispatcher();
            var order = new List<int>();

            dispatcher.Post(() => order.Add(1));
            dispatcher.Post(() => order.Add(2));
            dispatcher.Post(() => order.Add(3));
            dispatcher.RunUntilIdle();

            Assert.Equal(new[] { 1, 2, 3 }, order);
        }

        [Fact]
        public void AThrowingActionDoesNotStrandTheQueue()
        {
            // A subscriber's exception propagates to the caller of RunUntilIdle -- the
            // message loop, in the GUI -- but must not leave the remaining actions
            // permanently undrainable.
            var dispatcher = new QueueDispatcher();
            bool secondRan = false;

            dispatcher.Post(() => { throw new System.InvalidOperationException("boom"); });
            dispatcher.Post(() => secondRan = true);

            Assert.Throws<System.InvalidOperationException>(() => dispatcher.RunUntilIdle());

            dispatcher.RunUntilIdle();
            Assert.True(secondRan);
        }
    }
}
