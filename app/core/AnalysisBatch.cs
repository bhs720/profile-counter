using System;
using System.Collections.Generic;
using System.IO;

namespace TIFPDFCounter
{
    /// <summary>
    /// Runs a bounded pool of <see cref="FileAnalyzer"/> over a queue of files, keeping
    /// at most <c>maxConcurrency</c> pfc-tool.exe processes alive at once and refilling
    /// the pool as each one finishes.
    /// <para>
    /// Events are raised on whatever thread completed the work -- an analyzer's
    /// callbacks arrive on thread pool threads -- and never while the lock is held. A
    /// UI caller marshals them onto its own thread in its handlers. It must not do so by
    /// blocking: the UI thread does real work per completed file, and the threads it
    /// would block are the very thread pool threads the redirected stream readers need
    /// in order to deliver their end-of-file callbacks.
    /// </para>
    /// </summary>
    public sealed class AnalysisBatch
    {
        private readonly object gate = new object();
        private readonly Queue<BatchItem> queue = new Queue<BatchItem>();
        private readonly Dictionary<FileAnalyzer, BatchItem> running = new Dictionary<FileAnalyzer, BatchItem>();
        private readonly List<BatchItem> items = new List<BatchItem>();
        private readonly List<TPCFile> results = new List<TPCFile>();
        private readonly List<FileAnalyzer> failures = new List<FileAnalyzer>();

        private readonly AnalysisOptions options;
        private readonly IPfcToolProcessFactory processFactory;
        private readonly Func<DateTime> clock;
        private readonly int maxConcurrency;

        /// <summary>
        /// True while a thread is inside <see cref="Pump"/>. A completion that arrives
        /// during a pump -- including one raised synchronously by Go() -- records nothing
        /// more than "there is work to do" and returns, because the running pump
        /// re-evaluates on its next iteration. Without this, Go() raising
        /// AnalysisComplete on the calling thread would re-enter the pump from inside
        /// itself, nesting one frame set per queued file: a missing pfc-tool.exe and a
        /// few hundred dropped files would overflow the stack, which .NET cannot catch.
        /// </summary>
        private bool pumping;

        private bool cancelled;
        private bool finished;

        public event Action<BatchItem> FileStarted = delegate { };
        public event Action<BatchItem, int, int> FileProgress = delegate { };
        public event Action<BatchItem, FileAnalyzer> FileCompleted = delegate { };
        public event Action BatchFinished = delegate { };

        /// <param name="maxConcurrency">
        /// Zero means one process per core, less one, which is what ships.
        /// </param>
        public AnalysisBatch(
            IEnumerable<string> filenames,
            AnalysisOptions options,
            IPfcToolProcessFactory processFactory,
            int maxConcurrency = 0,
            Func<DateTime> clock = null)
        {
            if (filenames == null) throw new ArgumentNullException("filenames");
            if (options == null) throw new ArgumentNullException("options");
            if (processFactory == null) throw new ArgumentNullException("processFactory");

            this.options = options;
            this.processFactory = processFactory;
            this.clock = clock;
            this.maxConcurrency = maxConcurrency > 0 ? maxConcurrency : Math.Max(Environment.ProcessorCount - 1, 1);

            // A file must never be counted twice: the page totals this produces are what
            // customers price their own customers' work from. MainForm already filters a
            // drop, so this is a guard rather than the rule -- silent rather than fatal,
            // because throwing here would crash the application over a condition that has
            // never occurred in practice.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string filename in filenames)
            {
                if (!seen.Add(NormalizeForComparison(filename)))
                    continue;

                var item = new BatchItem(items.Count, filename);
                items.Add(item);
                queue.Enqueue(item);
            }
        }

        private static string NormalizeForComparison(string filename)
        {
            try
            {
                return Path.GetFullPath(filename);
            }
            catch
            {
                // Not a path this process can resolve. Compare it as written rather than
                // discarding it.
                return filename ?? string.Empty;
            }
        }

        /// <summary>Every file this batch will analyze, in order, with duplicates removed.</summary>
        public IReadOnlyList<BatchItem> Items { get { return items; } }

        /// <summary>Files that analyzed successfully. Safe to read once the batch has finished.</summary>
        public IReadOnlyList<TPCFile> Results { get { return results; } }

        /// <summary>Analyzers that failed. Safe to read once the batch has finished.</summary>
        public IReadOnlyList<FileAnalyzer> Failures { get { return failures; } }

        public bool Finished
        {
            get { lock (gate) { return finished; } }
        }

        public void Start()
        {
            Pump();
        }

        /// <summary>
        /// Drains the queue and kills whatever is running. The batch finishes once the
        /// cancelled analyzers have delivered their completion signals.
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.Print("Cancel batch");

            List<FileAnalyzer> toCancel;
            lock (gate)
            {
                cancelled = true;
                queue.Clear();
                toCancel = new List<FileAnalyzer>(running.Keys);
            }

            foreach (var analyzer in toCancel)
                analyzer.Cancel();

            // Nothing was running, so no completion will arrive to finish the batch.
            Pump();
        }

        private void Pump()
        {
            lock (gate)
            {
                if (pumping)
                    return;

                pumping = true;
            }

            while (true)
            {
                BatchItem item = null;
                FileAnalyzer analyzer = null;
                bool raiseFinished = false;

                lock (gate)
                {
                    if (!cancelled && running.Count < maxConcurrency && queue.Count > 0)
                    {
                        item = queue.Dequeue();
                        analyzer = CreateAnalyzer(item);
                        running.Add(analyzer, item);
                    }
                    else if (running.Count == 0 && queue.Count == 0 && !finished)
                    {
                        finished = true;
                        raiseFinished = true;
                    }
                    else
                    {
                        // Either the pool is full, or work is still in flight and its
                        // completion will pump again. Releasing `pumping` under the same
                        // lock as this decision is what makes that safe: a completion
                        // either removes itself from `running` before this check, and so
                        // is seen here, or arrives after `pumping` is false and pumps
                        // itself.
                        pumping = false;
                        return;
                    }
                }

                if (raiseFinished)
                {
                    BatchFinished();
                    lock (gate) { pumping = false; }
                    return;
                }

                FileStarted(item);

                // Go() can raise AnalysisComplete on this very thread, when the process
                // fails to start. That re-enters OnAnalyzerComplete -> Pump, which sees
                // `pumping` and returns; this loop then picks the work up on its next
                // iteration instead of recursing.
                analyzer.Go();
            }
        }

        private FileAnalyzer CreateAnalyzer(BatchItem item)
        {
            var analyzer = new FileAnalyzer(item.Filename, options, processFactory, clock);
            analyzer.ProgressChanged += OnAnalyzerProgress;
            analyzer.AnalysisComplete += OnAnalyzerComplete;
            return analyzer;
        }

        private void OnAnalyzerProgress(FileAnalyzer analyzer, int completed, int total)
        {
            BatchItem item;
            lock (gate)
            {
                if (!running.TryGetValue(analyzer, out item))
                    return;
            }

            FileProgress(item, completed, total);
        }

        private void OnAnalyzerComplete(FileAnalyzer analyzer)
        {
            BatchItem item;
            lock (gate)
            {
                if (!running.TryGetValue(analyzer, out item))
                    return;

                running.Remove(analyzer);

                // Cancelled is checked before Failed on purpose. A cancelled analyzer's
                // process is killed, so it exits non-zero and is marked failed too; it is
                // not a file that failed to analyze.
                if (analyzer.Cancelled)
                {
                    // Neither a result nor a failure.
                }
                else if (analyzer.Failed)
                {
                    failures.Add(analyzer);
                }
                else if (analyzer.Result != null)
                {
                    results.Add(analyzer.Result);
                }
            }

            analyzer.ProgressChanged -= OnAnalyzerProgress;
            analyzer.AnalysisComplete -= OnAnalyzerComplete;

            FileCompleted(item, analyzer);
            Pump();
        }
    }
}
