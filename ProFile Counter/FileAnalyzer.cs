using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;

namespace ProFileCounter
{
    public class FileAnalyzer
    {
        Process process;
        TPCFile result;

        bool cancelled;

        string fileName;
        string error;

        public delegate void ProgressChangedEventHandler(int completed, int total);
        public delegate void AnalysisCompleteEventHandler(TPCFile result, string error);
        public event ProgressChangedEventHandler ProgressChanged;
        public event AnalysisCompleteEventHandler AnalysisComplete;

        public static float ColorThreshold = 0.25f;

        public bool Cancelled { get { return cancelled; } }

        public FileAnalyzer(string fileName)
        {
            this.fileName = fileName;
            cancelled = false;
            process = new Process();
            process.StartInfo.FileName = @"mupdf.exe";
            string args = null;

            if (Settings.UserSettings1.PerformColorAnalysis)
                args = string.Format(@"""{0}"" {1}", fileName, ColorThreshold.ToString());
            else
                args = string.Format(@"""{0}""", fileName);
            
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
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            Debug.WriteLine("FileAnalyzer started");
        }

        public void Stop()
        {
            Debug.WriteLine("Stopping FileAnalyzer");
            try { process.Kill(); }
            catch (Exception) { }
            cancelled = true;
        }

        void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
                error += e.Data + "\n";
        }

        void process_Exited(object sender, EventArgs e)
        {
            process.WaitForExit();
            Debug.WriteLine("mupdf exited, Exit Code: " + process.ExitCode);
            if (process.ExitCode != 0)
                result = null;
            else if (Settings.UserSettings1.CheckForDuplicateFiles)
            {
                while (true)
                {
                    try
                    {
                        Debug.WriteLine("Taking file hash");
                        result.ComputeHash();
                    }
                    catch (Exception ex)
                    {
                        var dlgResult = MessageBox.Show(fileName + "\n\n" + ex.Message, "Problem", MessageBoxButtons.RetryCancel);
                        if (dlgResult == DialogResult.Retry)
                            continue;
                        if (dlgResult == DialogResult.Cancel)
                            this.cancelled = true;
                    }
                    break;
                }
            }
            AnalysisComplete.Invoke(this.result, error);
            process.Dispose();
        }

        void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data) && !this.cancelled)
            {
                System.Diagnostics.Debug.WriteLine(e.Data);
                string[] line = e.Data.Split(' ');
                if (line.Length == 1)
                {
                    string[] firstLine = line[0].Split('=');
                    if (firstLine[0] == "PageCount")
                    {
                        int pageCount = int.Parse(firstLine[1]);
                        result = new TPCFile(this.fileName, pageCount);
                        ProgressChanged.Invoke(0, pageCount);
                    }
                    else
                    {
                        Debug.WriteLine("Error in FileAnalyzer: PageCount");
                    }
                }
                if (line.Length >= 2)
                {
                    int pageNumber = -1;
                    float width = -1.0f;
                    float height = -1.0f;
                    ColorMode colorMode = ColorMode.Unknown;

                    if (line[0].Split('=')[0] == "Page")
                    {
                        pageNumber = int.Parse(line[0].Split('=')[1]);
                    }
                    if (line[1].Split('=')[0] == "Size")
                    {
                        width = float.Parse(line[1].Split('=')[1].Split(',')[0]) / 72.0f;
                        height = float.Parse(line[1].Split('=')[1].Split(',')[1]) / 72.0f;
                    }
                    if (line.Length == 3 && line[2].Split('=')[0] == "Color")
                    {
                        switch (int.Parse(line[2].Split('=')[1]))
                        {
                            case 0:
                                colorMode = ColorMode.BW;
                                break;
                            case 1:
                                colorMode = ColorMode.Color;
                                break;
                            default:
                                colorMode = ColorMode.Unknown;
                                break;
                        }
                    }
                    result.Pages[pageNumber] = new TPCFilePage(width, height, colorMode);
                    ProgressChanged.Invoke(pageNumber + 1, result.Pages.Length);
                }
            }
        }
    }
}
