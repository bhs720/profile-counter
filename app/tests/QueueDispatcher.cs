using System;
using System.Collections.Generic;
using TIFPDFCounter;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// A dispatcher tests drain by hand. Actions queue and run only when RunUntilIdle is
    /// called, which is what makes batch tests deterministic: no threads, no barriers, no
    /// timing.
    /// <para>
    /// The queue holds the only lock in the design. That is deliberate and correct --
    /// analyzer callbacks genuinely arrive on thread pool threads, so enqueuing must be
    /// safe from any thread. Everything downstream of the queue is single threaded.
    /// </para>
    /// </summary>
    public sealed class QueueDispatcher : IDispatcher
    {
        private readonly object gate = new object();
        private readonly Queue<Action> queue = new Queue<Action>();

        public int PendingCount
        {
            get { lock (gate) { return queue.Count; } }
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (gate) { queue.Enqueue(action); }
        }

        /// <summary>
        /// Runs queued actions until none remain, including any queued by the actions
        /// themselves. Returns how many ran. An exception from an action propagates; the
        /// remaining actions stay queued and a further call drains them.
        /// </summary>
        public int RunUntilIdle()
        {
            int ran = 0;
            while (true)
            {
                Action next;
                lock (gate)
                {
                    if (queue.Count == 0)
                        return ran;

                    next = queue.Dequeue();
                }

                ran++;
                next();
            }
        }
    }
}
