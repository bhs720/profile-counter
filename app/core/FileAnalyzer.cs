using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TIFPDFCounter
{
    /// <summary>
    /// Reads the stdout of pfc-tool.exe and provides a <see cref="Result"/>
    /// </summary>
    public class FileAnalyzer
    {
        private IPfcToolProcess process;
        private readonly Func<DateTime> clock;

        /// <summary>
        /// The exit code delivered with <see cref="IPfcToolProcess.Exited"/>. Null means
        /// the process never exited, which is reachable only when Start threw -- and the
        /// file has already been failed in that case.
        /// </summary>
        private int? exitCode;

        /// <summary>
        /// Holds the last moment in time when <see cref="ProgressChanged"/> was invoked.
        /// Null if the event was never invoked.
        /// </summary>
        private DateTime? lastProgress;

        /// <summary>
        /// The minimum amount of time to wait between invocations of <see cref="ProgressChanged"/> event handler.
        /// </summary>
        private TimeSpan progressInterval = new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 0, milliseconds: 500);

        /// <summary>
        /// Analysis is finished only once the process has exited AND both redirected
        /// streams have signalled end-of-file. Exit can fire before the asynchronous
        /// readers have delivered their final lines, so exit alone is not enough.
        /// <para>
        /// These are set from several different threads: the exit notification and the
        /// two stream readers each arrive on a thread pool thread. Nothing here blocks
        /// waiting for the others -- whichever signal lands last performs the
        /// completion. An earlier version had the exit handler spin in
        /// <c>Thread.Sleep</c> until stdout finished, which parked a pool thread per
        /// analyzer while the callback that would release it needed a pool thread of its
        /// own; a burst of fast-failing files could stall the batch, and a sentinel that
        /// never arrived stranded the file as "Processing" forever.
        /// </para>
        /// </summary>
        private int signalsOutstanding = 3;

        /// <summary>
        /// Guards <see cref="AnalysisComplete"/> so it is raised exactly once, whichever
        /// signal happens to arrive last.
        /// <para>
        /// Deliberately no timeout anywhere in this class. A deadline would be a guess
        /// about how fast a machine is, and the machines this ships to are not this one --
        /// slower disks, network shares, antivirus in the path, a loaded terminal server.
        /// Completion is defined by the three signals rather than by the clock, so it is
        /// correct regardless of how long any of it takes.
        /// </para>
        /// <para>
        /// Complete() has two callers: SignalArrived(), when the signal counter reaches
        /// zero, and Go()'s catch block, when Start() throws. Against the real
        /// <see cref="PfcToolProcess"/> these cannot actually contend. Start() runs
        /// Process.Start(), then BeginErrorReadLine(), then BeginOutputReadLine(); if a
        /// later step throws, the earlier ones already succeeded, so only a subset of the
        /// three completion signals is even capable of firing. If BeginErrorReadLine()
        /// throws, only Exited can still fire, taking signalsOutstanding from 3 to 2. If
        /// BeginOutputReadLine() throws, only the stderr end-of-file and Exited can still
        /// fire, taking it from 3 to 1. Either way the counter never reaches zero, so
        /// SignalArrived() never calls Complete() -- Go()'s catch is the only caller that
        /// ever does, for that file.
        /// </para>
        /// <para>
        /// The guard stays anyway, as cheap defence-in-depth rather than protection
        /// against a live race: <see cref="IPfcToolProcess"/> is a seam a third party
        /// could implement differently (e.g. firing Exited before Start() returns, or
        /// delivering a signal it shouldn't have after a throw), and this class cannot
        /// vouch for an implementation it did not write. It also makes a double call to
        /// Go() harmless, should one ever happen.
        /// </para>
        /// <para>
        /// No test in the suite covers this guard's contended path, because that path is
        /// not reachable through the real <see cref="IPfcToolProcess"/> implementation --
        /// there is nothing to contend it with. The concurrency test that remains in
        /// FileAnalyzerTests.cs (CompletesExactlyOnceUnderConcurrentSignalDelivery)
        /// confirms this: measured with this CompareExchange deliberately downgraded to a
        /// non-atomic check-then-set, it detected 0 of 10 runs, at both 200 and 2000
        /// iterations of its loop. That is expected, not a gap -- all three of its signals
        /// go through SignalArrived()'s Interlocked.Decrement(ref signalsOutstanding),
        /// which already guarantees exactly one thread ever observes the decrement reach
        /// zero, so exactly one thread ever reaches this CompareExchange there.
        /// </para>
        /// <para>
        /// Do not remove the CompareExchange on the strength of this reasoning. It is
        /// still the only thing standing between a misbehaving <see cref="IPfcToolProcess"/>
        /// and a double-raised <see cref="AnalysisComplete"/>.
        /// </para>
        /// </summary>
        private int completionRaised;

        /// <summary>
        /// Guards each of the three completion signals so a duplicate delivery cannot
        /// stand in for one that never arrives. <see cref="signalsOutstanding"/> is one
        /// shared counter, so without these a signal fired twice would decrement it
        /// twice and complete the analysis with a signal still missing -- most
        /// dangerously, completing while stderr is still filling and truncating the very
        /// message explaining a failure. The real <see cref="PfcToolProcess"/> does not
        /// duplicate signals, but the seam lets any <see cref="IPfcToolProcess"/> stand
        /// in, and this class cannot vouch for one it did not write.
        /// </summary>
        private int stdoutEnded;
        private int stderrEnded;
        private int processExited;

        /// <summary>
        /// Guards which of Go() and Cancel() gets to decide whether the child process
        /// ever starts. Within <see cref="AnalysisBatch"/> this can no longer actually
        /// race: the batch holds no lock, and Cancel() posts, so it can only run as a
        /// later, separately dispatched action -- Pump's <c>running.Add</c>, the
        /// <c>FileStarted</c> raise and <c>analyzer.Go()</c> are one synchronous block on
        /// the dispatcher thread, and the batch can never reach Cancel() before Go() has
        /// already run.
        /// <para>
        /// The guard earns its place anyway because <see cref="FileAnalyzer"/> is public
        /// and has callers outside the batch -- the integration tests drive it directly
        /// -- so Go() and Cancel() can still arrive from different threads for those
        /// callers. Without this, Cancel() calls Kill() on a process that has not been
        /// started yet -- which PfcToolProcess.Kill() swallows, because
        /// <c>Process.HasExited</c> throws InvalidOperationException on an unstarted
        /// process -- and Go() then starts the child anyway, so it runs to completion
        /// despite having been cancelled.
        /// </para>
        /// <para>
        /// One atomic CompareExchange decides the outcome regardless of which method
        /// reaches it first: whichever of Go()/Cancel() transitions this out of
        /// <see cref="StateInitial"/> wins. If Cancel() wins, Go() never calls
        /// Process.Start() at all and completes the analyzer as cancelled directly. If
        /// Go() wins, the process is genuinely started and Cancel()'s subsequent Kill()
        /// call is the real, effective one.
        /// </para>
        /// </summary>
        private int state;

        private const int StateInitial = 0;
        private const int StateCancelled = 1;
        private const int StateStarted = 2;

        /// <summary>
        /// <see cref="Errors"/> is appended to from the stderr reader and from
        /// <see cref="Fail"/>, which can run on different threads at the same time.
        /// StringBuilder is not thread safe.
        /// </summary>
        private readonly object errorsLock = new object();

        public delegate void ProgressChangedEventHandler(FileAnalyzer instance, int completed, int total);
        public delegate void AnalysisCompleteEventHandler(FileAnalyzer instance);
        public event ProgressChangedEventHandler ProgressChanged = delegate { /* empty in case of no subscribers */ };
        public event AnalysisCompleteEventHandler AnalysisComplete = delegate { /* empty in case of no subscribers */ };

        /// <summary>
        /// The filename assigned to this <see cref="FileAnalyzer"/>.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// <see cref="Cancel"/> was called. <see cref="Result"/> is invalid or null.
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// <see cref="Fail(string)"/> was called. <see cref="Result"/> is invalid or null.
        /// </summary>
        public bool Failed { get; private set; }

        /// <summary>
        /// May be null or invalid if <see cref="Cancelled"/> is true or <see cref="Failed"/> is true.
        /// </summary>
        public TPCFile Result { get; private set; }

        /// <summary>
        /// Output from process StandardError. <see cref="FileAnalyzer"/> may also add error messages here.
        /// </summary>
        public StringBuilder Errors { get; private set; }

        /// <param name="clock">
        /// Supplies "now" for the <see cref="ProgressChanged"/> throttle. Injected so the
        /// throttle can be tested without a test that depends on wall-clock timing.
        /// </param>
        public FileAnalyzer(string filename, AnalysisOptions options, IPfcToolProcessFactory processFactory, Func<DateTime> clock = null)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (processFactory == null) throw new ArgumentNullException("processFactory");

            Filename = filename;
            Errors = new StringBuilder();
            this.clock = clock ?? (() => DateTime.Now);

            string args = PfcToolProtocol.FormatArguments(
                filename,
                options.PerformColorAnalysis,
                options.ColorThreshold,
                options.CheckImagePixels);
            Debug.Print("pfc-tool.exe {0}", args);

            process = processFactory.Create(options.ToolPath, args);
            process.OutputLineReceived += OnOutputLine;
            process.OutputEnded += OnOutputEnded;
            process.ErrorLineReceived += OnErrorLine;
            process.ErrorEnded += OnErrorEnded;
            process.Exited += OnExited;
        }

        public void Go()
        {
            // Claim the right to start the process. If this loses -- meaning Cancel()
            // already claimed StateCancelled first -- the process must never start:
            // complete the analyzer as cancelled directly instead of calling
            // Process.Start(). Reusing Complete() keeps completionRaised as the single
            // guard on AnalysisComplete and lets the batch still account for this file,
            // so BatchFinished remains reachable.
            if (Interlocked.CompareExchange(ref state, StateStarted, StateInitial) != StateInitial)
            {
                Debug.Print("Go FileAnalyzer: already cancelled, not starting the process");
                Complete();
                return;
            }

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                // If the process never starts, none of the three completion signals can
                // ever arrive, so the caller would wait on this analyzer forever. Report
                // it as a failed file instead.
                Fail("Could not start pfc-tool.exe: " + ex.Message);
                Complete();
            }
        }

        public void Cancel()
        {
            Debug.Print("Cancel FileAnalyzer");
            Cancelled = true;

            // Claim the right to start the process before Go() does. If this wins --
            // state was still StateInitial -- Go() will see StateCancelled when it runs
            // (now or later) and skip Process.Start() entirely; there is nothing to kill
            // because nothing was ever started. If this loses, Go() already claimed
            // StateStarted, so the process is genuinely running (or about to be) and
            // Kill() is the real, effective cancellation.
            if (Interlocked.CompareExchange(ref state, StateCancelled, StateInitial) != StateInitial)
            {
                Kill();
            }
        }

        private void Fail(string message)
        {
            Debug.Print("Fail FileAnalyzer: " + message);
            AppendError(message);
            Failed = true;
            Kill();
        }

        private void AppendError(string message)
        {
            lock (errorsLock)
            {
                Errors.AppendLine(message);
            }
        }

        private void Kill()
        {
            // The process reference is cleared only after completion, but a stream
            // callback can still arrive afterwards and call Fail, so read it once.
            IPfcToolProcess p = process;
            if (p == null)
                return;

            p.Kill();
        }

        private void OnErrorLine(string line)
        {
            if (line.Length > 0)
                AppendError(line);
        }

        private void OnOutputEnded()
        {
            // A signal delivered twice must not stand in for one that never arrives.
            if (Interlocked.Exchange(ref stdoutEnded, 1) != 0)
                return;

            SignalArrived();
        }

        private void OnErrorEnded()
        {
            // A signal delivered twice must not stand in for one that never arrives.
            if (Interlocked.Exchange(ref stderrEnded, 1) != 0)
                return;

            // Waiting for this matters: the grid shows the first 255 characters of
            // Errors when a file fails, and completing before stderr has drained can
            // truncate the very message explaining the failure.
            SignalArrived();
        }

        private void OnExited(int code)
        {
            // A signal delivered twice must not stand in for one that never arrives.
            if (Interlocked.Exchange(ref processExited, 1) != 0)
                return;

            // Exit is only one of the three signals; the readers may still have lines in
            // flight, and whichever signal lands last does the completing.
            exitCode = code;
            SignalArrived();
        }

        /// <summary>
        /// Records one of the three completion signals. The last one to arrive finishes
        /// the analysis, so no thread ever waits on another.
        /// </summary>
        private void SignalArrived()
        {
            if (Interlocked.Decrement(ref signalsOutstanding) == 0)
            {
                Complete();
            }
        }

        /// <summary>
        /// Finishes the analysis exactly once, on whichever thread delivered the last
        /// signal.
        /// </summary>
        private void Complete()
        {
            // Two signals can land at the same moment on different threads; only the
            // first caller gets to finish the file.
            if (Interlocked.CompareExchange(ref completionRaised, 1, 0) != 0)
                return;

            Validate();

            AnalysisComplete.Invoke(this);

            // Clear the reference before disposing so a late stream callback that calls
            // Fail -> Kill sees null rather than a disposed process.
            IPfcToolProcess p = process;
            process = null;
            if (p != null)
            {
                try { p.Dispose(); }
                catch { /* nothing useful left to do at this point */ }
            }
        }

        private void Validate()
        {
            if (exitCode.HasValue && exitCode.Value != 0)
            {
                Fail("pfc-tool.exe exit code: " + exitCode.Value);
            }

            if (!Failed && !Cancelled)
            {
                if (Result == null)
                {
                    // No PageCount line was ever parsed, so there is nothing to report.
                    // Reading Result.PageCount here used to throw on a thread pool
                    // thread, which takes the whole application down.
                    Fail("No page count was reported.");
                }
                else if (Result.PageCount == 0)
                {
                    Fail("Page count is zero.");
                }
                else if (Result.Pages.Count != Result.PageCount)
                {
                    Fail("Page count mismatch. Expected=" + Result.PageCount + " Received=" + Result.Pages.Count);
                }
            }
        }

        private void OnOutputLine(string data)
        {
            var line = PfcToolProtocol.Parse(data);

            switch (line.Kind)
            {
                case PfcToolLineKind.Blank:
                    // Blank line -- nothing to parse, and not a protocol violation.
                    break;

                case PfcToolLineKind.Header:
                    Result = new TPCFile(Filename, line.PageCount, line.BookmarkCount);
                    ProgressChanged.Invoke(this, 0, line.PageCount);
                    break;

                case PfcToolLineKind.Page:
                    if (Result == null)
                    {
                        Fail("Page spec came before page count");
                        return;
                    }

                    Result.AddPage(line.PageNumber, line.WidthInches, line.HeightInches, line.ColorMode);

                    DateTime now = clock();
                    if (lastProgress == null || (now - lastProgress) > progressInterval)
                    {
                        lastProgress = now;
                        ProgressChanged.Invoke(this, line.PageNumber, Result.PageCount);
                    }
                    break;

                default:
                    Fail("Text was not in an expected format: " + data);
                    return;
            }
        }
    }
}
