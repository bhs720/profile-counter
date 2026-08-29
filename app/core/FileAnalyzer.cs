using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TIFPDFCounter
{
    /// <summary>
    /// Reads the stdout of pfc-tool.exe and provides a <see cref="Result"/>
    /// </summary>
    public class FileAnalyzer
    {
        private Process process;

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
        /// streams have signalled end-of-file. <see cref="Process.Exited"/> can fire
        /// before the asynchronous readers have delivered their final lines, so exit
        /// alone is not enough.
        /// <para>
        /// These are set from several different threads: <see cref="Process.Exited"/>
        /// and the two stream readers each arrive on a thread pool thread. Nothing here
        /// blocks waiting for the others -- whichever signal lands last performs the
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
        /// </summary>
        private int completionRaised;

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

        /// <summary>
        /// To be used by the caller for tracking purposes. <see cref="FileAnalyzer"/> does not read or modify this object.
        /// </summary>
        public Object Tag { get; set; }

        public FileAnalyzer(string filename, bool checkColor, decimal colorThreshold, bool checkPixels)
        {
            Filename = filename;
            Errors = new StringBuilder();
            process = new Process();
            process.StartInfo.FileName = @"pfc-tool.exe";

            // pfc-tool.exe parses the threshold with the C locale, so it must be formatted
            // culture-invariantly -- a comma-decimal culture would otherwise emit "0,25",
            // which the tool rejects.
            string args = string.Format(CultureInfo.InvariantCulture, "\"{0}\" {1} {2}", filename, (checkColor ? colorThreshold.ToString(CultureInfo.InvariantCulture) : "-1"), (checkPixels ? "1" : "0"));
            Debug.Print("pfc-tool.exe {0}", args);

            // Pass the arguments through unchanged. This used to be re-encoded as
            // Encoding.Default.GetString(Encoding.UTF8.GetBytes(args)), which corrupted
            // every non-ASCII filename: .NET hands Arguments to CreateProcessW as UTF-16,
            // so mangling the string first simply put mojibake on the command line.
            // pfc-tool.exe now reads the real UTF-16 command line through wmain and
            // converts it to the UTF-8 that mupdf expects.
            process.StartInfo.Arguments = args;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // pfc-tool.exe writes UTF-8: filenames reach mupdf as UTF-8 and come back
            // inside its error text. Decoding that as the ANSI code page turned a
            // copyright sign in a failing path into "?" in the results grid, which reads
            // as a second fault rather than as the one being reported.
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += process_OutputDataReceived;
            process.ErrorDataReceived += process_ErrorDataReceived;
            process.Exited += process_Exited;
            process.EnableRaisingEvents = true;
        }

        public void Go()
        {
            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
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
            Kill();
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
            Process p = process;
            if (p == null)
                return;

            try
            {
                if (!p.HasExited)
                {
                    Debug.Print("Kill FileAnalyzer");
                    p.Kill();
                }
            }
            catch { /* swallow -- the process may have exited or been disposed already */ }
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                // End of stderr. Waiting for this matters: the grid shows the first 255
                // characters of Errors when a file fails, and completing before stderr
                // has drained can truncate the very message explaining the failure.
                SignalArrived();
                return;
            }

            if (e.Data.Length > 0)
                AppendError(e.Data);
        }

        private void process_Exited(object sender, EventArgs e)
        {
            // Exit is only one of the three signals; the readers may still have lines in
            // flight, and whichever signal lands last does the completing.
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
            // Fail -> Kill sees null rather than a disposed Process.
            Process p = process;
            process = null;
            if (p != null)
            {
                try { p.Close(); p.Dispose(); }
                catch { /* nothing useful left to do at this point */ }
            }
        }

        private void Validate()
        {
            int exitCode = 0;
            try
            {
                Process p = process;
                if (p != null)
                    exitCode = p.ExitCode;
            }
            catch { /* treated as a clean exit; the checks below still apply */ }

            if (exitCode != 0)
            {
                Fail("pfc-tool.exe exit code: " + exitCode);
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

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                // End of stdout. Only null means end-of-file; an empty line is ordinary
                // data. Treating both alike would signal completion twice and could
                // finish the file before stderr had drained.
                SignalArrived();
            }
            else
            {
                var line = PfcToolProtocol.Parse(e.Data);

                switch (line.Kind)
                {
                    case PfcToolLineKind.Blank:
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

                        if (lastProgress == null || (DateTime.Now - lastProgress) > progressInterval)
                        {
                            lastProgress = DateTime.Now;
                            ProgressChanged.Invoke(this, line.PageNumber, Result.PageCount);
                        }
                        break;

                    default:
                        Fail("Text was not in an expected format: " + e.Data);
                        return;
                }
            }
        }
    }
}
