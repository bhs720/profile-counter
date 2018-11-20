using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public partial class ProcessWindow : Form
    {
        public List<TPCFile> Results { get { return completedFiles; } }

        private readonly Queue<string> processQueue;
        private readonly List<FileAnalyzer> runningProcesses;
        private readonly List<TPCFile> completedFiles;
        private readonly List<FileAnalyzer> failedFiles;
        private readonly int maxProcesses;
        private bool batchInProgress;
        private bool batchCancelled;

        public ProcessWindow()
        {
            InitializeComponent();
        }

        public ProcessWindow(List<string> filenames) : this()
        {
            processQueue = new Queue<string>(filenames);
            runningProcesses = new List<FileAnalyzer>();
            completedFiles = new List<TPCFile>();
            failedFiles = new List<FileAnalyzer>();
            maxProcesses = Environment.ProcessorCount;

            int colIndex = grid.Columns.Add(new DataGridViewProgressColumn());
            grid.Columns[colIndex].Name = "Progress";
            grid.Columns[colIndex].HeaderText = "Progress";
        }

        private void NextFile()
        {
            while (runningProcesses.Count < maxProcesses && processQueue.Count > 0)
            {
                batchInProgress = true;
                string filename = processQueue.Dequeue();
                var analyzer = new FileAnalyzer(filename, Settings.Instance.PerformColorAnalysis, Settings.Instance.ColorThreshold, Settings.Instance.CheckImagePixels);
                GetRow(filename).Cells["Status"].Value = "Processing";
                
                runningProcesses.Add(analyzer);
                analyzer.ProgressChanged += Analyzer_ProgressChanged;
                analyzer.AnalysisComplete += Analyzer_AnalysisComplete;
                analyzer.Go();
            }
        }

        private DataGridViewRow GetRow(string filePath)
        {
            return grid.Rows.Cast<DataGridViewRow>()
                .First(r => ((string)r.Tag).Equals(filePath));
        }

        private void Analyzer_AnalysisComplete(FileAnalyzer instance)
        {
            Utility.InvokeIfRequired(this, () =>
            {
                runningProcesses.Remove(instance);
                instance.ProgressChanged -= Analyzer_ProgressChanged;
                instance.AnalysisComplete -= Analyzer_AnalysisComplete;
                
                if (instance.Cancelled)
                {
                    GetRow(instance.Filename).Cells["Status"].Value = "Cancelled";
                }
                else if (instance.Failed)
                {
                    string errorMessage = "Failed: " + instance.Errors.ToString(0, Math.Min(instance.Errors.Length, 255));
                    var dgvr = GetRow(instance.Filename);
                    dgvr.Cells["Status"].Value = errorMessage;
                    dgvr.DefaultCellStyle.BackColor = Color.DarkRed;
                    dgvr.DefaultCellStyle.ForeColor = Color.White;
                    dgvr.DefaultCellStyle.SelectionBackColor = Color.Red;
                    dgvr.DefaultCellStyle.SelectionForeColor = Color.White;
                    failedFiles.Add(instance);
                }
                else
                {
                    completedFiles.Add(instance.Result);
                    grid.Rows.Remove(GetRow(instance.Filename));
                }

                ScrollToFirstProcessingRow();

                if (processQueue.Count == 0 && runningProcesses.Count == 0)
                {
                    FinishBatch();
                }
                else
                {
                    NextFile();
                }
            });
        }

        private void ScrollToFirstProcessingRow()
        {
            if (grid.Rows.Count > 0)
            {
                var dgvr = grid.Rows.Cast<DataGridViewRow>().DefaultIfEmpty(null).FirstOrDefault(r =>
                {
                    string cellValue = (string)r.Cells["Status"].Value;
                    return cellValue == "Processing" || cellValue == "Queued";
                });
                if (dgvr != null)
                {
                    grid.FirstDisplayedScrollingRowIndex = dgvr.Index;
                }
            }
        }

        private void FinishBatch()
        {
            batchInProgress = false;
            if (batchCancelled || failedFiles.Count == 0)
            {
                Close();
            }
            else
            {
                Text = "Processing finished with errors";
            }
        }

        private void CancelBatch()
        {
            processQueue.Clear();
            foreach (var proc in runningProcesses)
            {
                proc.Cancel();
            }

            batchInProgress = false;
            batchCancelled = true;
        }

        private void ProcessWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (batchInProgress)
            {
                e.Cancel = true;
                CancelBatch();
            }
        }

        private void Analyzer_ProgressChanged(FileAnalyzer instance, int completed, int total)
        {
            Utility.InvokeIfRequired(this, () =>
            {
                GetRow(instance.Filename).Cells["Progress"].Value = completed * 100 / total;
            });
        }

        private void ProcessWindow_Load(object sender, EventArgs e)
        {
            foreach (var filePath in processQueue)
            {
                string fileSize;
                long fileLength;
                if (Utility.TryGetFileLength(filePath, out fileLength))
                {
                    fileSize = Utility.BytesToString(fileLength);
                }
                else
                {
                    fileSize = "Unknown";
                }

                string folder = System.IO.Path.GetDirectoryName(filePath);
                string fileName = System.IO.Path.GetFileName(filePath);
                string extension = System.IO.Path.GetExtension(filePath);
                int rowIndex = grid.Rows.Add(folder, fileName, extension, fileSize, "Queued", 0);
                grid.Rows[rowIndex].Tag = filePath;
            }

            NextFile();
        }

        private void grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                string fileName = grid.Rows[e.RowIndex].Tag as string;
                if (e.ColumnIndex == grid.Columns["Folder"].Index)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + fileName + "\"");
                    }
                    catch { /* swallow */ }
                    
                }
                else if (e.ColumnIndex == grid.Columns["Filename"].Index)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(fileName);
                    }
                    catch { /* swallow */ }
                    
                }
            }
        }

        
    }
}
