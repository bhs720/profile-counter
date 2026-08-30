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
    /// All state lives on the dispatcher's thread. Analyzer callbacks arrive on thread
    /// pool threads and are posted straight onto it, so there is no lock here and no
    /// re-entrancy guard: Go() completing synchronously posts rather than re-entering the
    /// pump. The dispatcher must never run an action inline; see <see cref="IDispatcher"/>.
    /// </para>
    /// </summary>
    public sealed class AnalysisBatch
    {
        private readonly Queue<BatchItem> queue = new Queue<BatchItem>();
        private readonly Dictionary<FileAnalyzer, BatchItem> running = new Dictionary<FileAnalyzer, BatchItem>();
        private readonly List<BatchItem> items = new List<BatchItem>();
        private readonly List<TPCFile> results = new List<TPCFile>();
        private readonly List<FileAnalyzer> failures = new List<FileAnalyzer>();

        private readonly AnalysisOptions options;
        private readonly IPfcToolProcessFactory processFactory;
        private readonly IDispatcher dispatcher;
        private readonly Func<DateTime> clock;
        private readonly int maxConcurrency;

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
            IDispatcher dispatcher,
            int maxConcurrency = 0,
            Func<DateTime> clock = null)
        {
            if (filenames == null) throw new ArgumentNullException("filenames");
            if (options == null) throw new ArgumentNullException("options");
            if (processFactory == null) throw new ArgumentNullException("processFactory");
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");

            this.options = options;
            this.processFactory = processFactory;
            this.dispatcher = dispatcher;
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

        public bool Finished { get { return finished; } }

        public void Start()
        {
            dispatcher.Post(Pump);
        }

        /// <summary>
        /// Drains the queue and kills whatever is running. Asynchronous: the work is
        /// dispatched, so it does not take effect inside the caller's stack frame. The
        /// batch finishes once the cancelled analyzers have delivered their completions.
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.Print("Cancel batch");

            dispatcher.Post(() =>
            {
                cancelled = true;
                queue.Clear();

                foreach (var analyzer in new List<FileAnalyzer>(running.Keys))
                    analyzer.Cancel();

                // Nothing was running, so no completion will arrive to finish the batch.
                Pump();
            });
        }

        private void Pump()
        {
            while (!cancelled && running.Count < maxConcurrency && queue.Count > 0)
            {
                BatchItem item = queue.Dequeue();
                FileAnalyzer analyzer = CreateAnalyzer(item);
                running.Add(analyzer, item);

                // Go() in a finally: an analyzer that reached `running` MUST be started,
                // or nothing will ever deliver a completion for it and the batch can never
                // finish. Raising FileStarted first preserves the event order the GUI
                // paints from. Go() can raise AnalysisComplete on this very thread when
                // the process fails to start, but that completion is posted, so it cannot
                // re-enter this loop.
                try
                {
                    FileStarted(item);
                }
                finally
                {
                    analyzer.Go();
                }
            }

            if (!finished && running.Count == 0 && queue.Count == 0)
            {
                finished = true;
                BatchFinished();
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
            dispatcher.Post(() =>
            {
                BatchItem item;
                if (!running.TryGetValue(analyzer, out item))
                    return;

                FileProgress(item, completed, total);
            });
        }

        private void OnAnalyzerComplete(FileAnalyzer analyzer)
        {
            dispatcher.Post(() =>
            {
                BatchItem item;
                if (!running.TryGetValue(analyzer, out item))
                    return;

                running.Remove(analyzer);

                analyzer.ProgressChanged -= OnAnalyzerProgress;
                analyzer.AnalysisComplete -= OnAnalyzerComplete;

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

                // Pump in a finally so a throwing subscriber cannot stall the pool: with a
                // queue behind it, a skipped Pump is permanent.
                try
                {
                    FileCompleted(item, analyzer);
                }
                finally
                {
                    Pump();
                }
            });
        }
    }
}
