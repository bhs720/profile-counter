using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace TIFPDFCounter
{
    /// <summary>
    /// Reads the stdout of mupdf.exe and provides a <see cref="Result"/>
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
        /// True when a line of <see cref="Process.StandardOutput"/> is null.
        /// This sentinel value is needed because <see cref="Process.Exited"/> may be called before 
        /// every line of <see cref="Process.StandardOutput"/> has been received asynchronously.
        /// </summary>
        private volatile bool lastOutputDataReceived;

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
            process.StartInfo.FileName = @"mupdf.exe";

            string args = string.Format("\"{0}\" {1} {2}", filename, (checkColor ? colorThreshold.ToString() : "-1"), (checkPixels ? "1" : "0"));
            Debug.Print("mupdf.exe {0}", args);

            process.StartInfo.Arguments = Encoding.Default.GetString(Encoding.UTF8.GetBytes(args));
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += process_OutputDataReceived;
            process.ErrorDataReceived += process_ErrorDataReceived;
            process.Exited += process_Exited;
            process.EnableRaisingEvents = true;
        }

        public void Go()
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            //Debug.Print("FileAnalyzer started");
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
            Errors.AppendLine(message);
            Failed = true;
            Kill();
        }

        private void Kill()
        {
            if (!process.HasExited)
            {
                Debug.Print("Kill FileAnalyzer");
                try { process.Kill(); }
                catch { /* swallow -- process may have exited already */ }
            }
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Errors.AppendLine(e.Data);
        }

        private void process_Exited(object sender, EventArgs e)
        {
            // Block and wait to receive all of stdout
            while (!lastOutputDataReceived)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (process.ExitCode != 0)
            {
                Fail("Mupdf exit code: " + process.ExitCode);
            }

            if (!Failed && !Cancelled)
            {
                if (Result.PageCount == 0)
                {
                    Fail("Page count is zero.");
                }
                else if (Result.Pages.Count != Result.PageCount)
                {
                    Fail("Page count mismatch. Expected=" + Result.PageCount + " Received=" + Result.Pages.Count);
                }
            }

            AnalysisComplete.Invoke(this);

            process.Close();
            process.Dispose();
            process = null;
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                // Final line of StandardOutput is null right before the process exits
                // This lets us know all of stdout has been received
                lastOutputDataReceived = true;
            }
            else
            {
                //Debug.Print(e.Data);

                // First line of program output gives:
                // PageCount=# BookmarkCount=#
                var matchPageCount = Regex.Match(e.Data, @"^PageCount=(\d+) BookmarkCount=(\d+)$");

                // Subsequent lines give:
                // Page=# Size=#.#,#.# Color=#
                var matchPageSpec = Regex.Match(e.Data, @"^Page=(\d+) Size=([\d\.]+),([\d\.]+) Color=(-?\d+)$");

                if (matchPageCount.Success && matchPageCount.Groups.Count == 3)
                {
                    int pageCount = Convert.ToInt32(matchPageCount.Groups[1].Value);
                    int bookmarkCount = Convert.ToInt32(matchPageCount.Groups[2].Value);
                    Result = new TPCFile(Filename, pageCount, bookmarkCount);
                    ProgressChanged.Invoke(this, 0, pageCount);
                }
                else if (matchPageSpec.Success && matchPageSpec.Groups.Count == 5)
                {
                    if (Result == null)
                    {
                        Fail("Page spec came before page count");
                        return;
                    }

                    int pageNumber = Convert.ToInt32(matchPageSpec.Groups[1].Value);
                    decimal width = Convert.ToDecimal(matchPageSpec.Groups[2].Value) / 72m; // Convert points to inches
                    decimal height = Convert.ToDecimal(matchPageSpec.Groups[3].Value) / 72m; // Convert points to inches
                    int color = Convert.ToInt32(matchPageSpec.Groups[4].Value);

                    ColorMode cm;
                    switch (color)
                    {
                        case 0:
                            cm = ColorMode.BW;
                            break;
                        case 1:
                        case 2:
                            cm = ColorMode.Color;
                            break;
                        default:
                            cm = ColorMode.Unknown;
                            break;
                    }

                    Result.AddPage(pageNumber, width, height, cm);

                    if (lastProgress == null || (DateTime.Now - lastProgress) > progressInterval)
                    {
                        lastProgress = DateTime.Now;
                        ProgressChanged.Invoke(this, pageNumber, Result.PageCount);
                    }
                }
                else
                {
                    Fail("Text was not in an expected format: " + e.Data);
                    return;
                }
            }
        }
    }
}
