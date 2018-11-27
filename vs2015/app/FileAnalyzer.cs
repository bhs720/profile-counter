using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public class FileAnalyzer
    {
        private Process process;
        private DateTime? lastProgress;
        private TimeSpan progressInterval = new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 0, milliseconds: 500);

        public delegate void ProgressChangedEventHandler(FileAnalyzer instance, int completed, int total);
        public delegate void AnalysisCompleteEventHandler(FileAnalyzer instance);
        public event ProgressChangedEventHandler ProgressChanged;
        public event AnalysisCompleteEventHandler AnalysisComplete;

        public string Filename { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Failed { get; private set; }
        public TPCFile Result { get; private set; }
        public StringBuilder Errors { get; private set; }

        public FileAnalyzer(string filename, bool checkColor, decimal colorThreshold, bool checkPixels)
        {
            Filename = filename;
            Cancelled = false;
            Errors = new StringBuilder();
            process = new Process();
            process.StartInfo.FileName = @"mupdf.exe";

            string args = string.Format("\"{0}\" {1} {2}", filename, (checkColor ? colorThreshold.ToString() : "-1"), (checkPixels ? "1" : "0"));
            Debug.Print("mupdf args: {0}", args);
            
            process.StartInfo.Arguments = Encoding.Default.GetString(Encoding.UTF8.GetBytes(args));
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += process_OutputDataReceived;
            process.ErrorDataReceived += process_ErrorDataReceived;
            process.Exited += process_Exited;
        }

        public void Go()
        {
            process.Start();
            if (!process.HasExited)
            {
                process.PriorityClass = ProcessPriorityClass.Idle;
            }
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            
            Debug.Print("FileAnalyzer started");
        }

        public void Cancel()
        {
            Debug.Print("Cancel FileAnalyzer");
            Result = null;
            Cancelled = true;
            Kill();
        }

        private void Fail(string message)
        {
            Debug.Print("Fail FileAnalyzer: " + message);
            Errors.AppendLine(message);
            Result = null;
            Failed = true;
            Kill();
        }

        private void Kill()
        {
            Debug.Print("Kill FileAnalyzer - already exited? {0}", process?.HasExited);
            if (process != null && !process.HasExited)
            {
                try { process.Kill(); }
                catch { }
            }
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                Errors.AppendLine(e.Data);
        }

        private void process_Exited(object sender, EventArgs e)
        {
            if (process != null)
            {
                // Wait to receive all of stdout
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Fail("Mupdf exit code: " + process.ExitCode);
                }

                if (!Failed && !Cancelled && Result.Pages.Count != Result.PageCount)
                {
                    Fail("Page count mismatch. Expected=" + Result.PageCount + " Received=" + Result.Pages.Count);
                }

                process.Dispose();
                process = null;
            }

            AnalysisComplete.Invoke(this);
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !Failed && !Cancelled)
            {
                Debug.Print(e.Data);
                
                var matchPageCount = Regex.Match(e.Data, @"^PageCount=(\d+)$");
                var matchPageSpec = Regex.Match(e.Data, @"^Page=(\d+) Size=([\d\.]+),([\d\.]+) Color=(-?\d+)$");
                
                if (matchPageCount.Success && matchPageCount.Groups.Count == 2)
                {
                    int pageCount = Convert.ToInt32(matchPageCount.Groups[1].Value);
                    Result = new TPCFile(Filename, pageCount);
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
