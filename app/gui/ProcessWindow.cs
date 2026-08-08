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
            maxProcesses = Math.Max(Environment.ProcessorCount - 1, 1);

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

                var dgvr = GetRow(filename);
                dgvr.Cells["Status"].Value = "Processing";
                
                var analyzer = new FileAnalyzer(filename, Settings.Current.PerformColorAnalysis, Settings.Current.ColorThreshold, Settings.Current.CheckImagePixels);
                // save a reference to the DataGridViewRow in the FileAnalyzer.Tag
                // this is used later for progress updates
                analyzer.Tag = dgvr;

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

        private void ScrollToFirstProcessingRow()
        {
            if (grid.Rows.Count > 0)
            {
                var dgvr = grid.Rows.Cast<DataGridViewRow>().DefaultIfEmpty(null).FirstOrDefault(r =>
                {
                    string cellValue = (string)r.Cells["Status"].Value;
                    return cellValue == "Processing" || cellValue == "Queued";
                });
                if (dgvr != null && dgvr.Index >= 0 && dgvr.Index < grid.Rows.Count)
                {
                    grid.FirstDisplayedScrollingRowIndex = dgvr.Index;
                }
            }
        }

        private void Analyzer_ProgressChanged(FileAnalyzer instance, int completed, int total)
        {
            Utility.InvokeIfRequired(this, () =>
            {
                var dgvr = (DataGridViewRow)instance.Tag;
                dgvr.Cells["Progress"].Value = completed * 100 / total;
            });
        }

        private void Analyzer_AnalysisComplete(FileAnalyzer instance)
        {
            Utility.InvokeIfRequired(this, () =>
            {   
                instance.ProgressChanged -= Analyzer_ProgressChanged;
                instance.AnalysisComplete -= Analyzer_AnalysisComplete;
                runningProcesses.Remove(instance);
                var dgvr = (DataGridViewRow)instance.Tag;

                if (instance.Cancelled)
                {
                    dgvr.Cells["Status"].Value = "Cancelled";
                }
                else if (instance.Failed)
                {
                    string errorMessage = "Failed: " + instance.Errors.ToString(0, Math.Min(instance.Errors.Length, 255));
                    dgvr.Cells["Status"].Value = errorMessage;
                    dgvr.DefaultCellStyle.BackColor = Color.DarkRed;
                    dgvr.DefaultCellStyle.ForeColor = Color.White;
                    dgvr.DefaultCellStyle.SelectionBackColor = Color.Red;
                    dgvr.DefaultCellStyle.SelectionForeColor = Color.White;
                    failedFiles.Add(instance);
                }
                else
                {
                    System.Diagnostics.Debug.Assert(instance.Result != null);
                    completedFiles.Add(instance.Result);
                    grid.Rows.Remove(dgvr);
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
            System.Diagnostics.Debug.Print("Cancel batch");
            processQueue.Clear();
            foreach (var proc in runningProcesses.ToList())
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
        
        private async void grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                string fileName = (string)grid.Rows[e.RowIndex].Tag;
                if (e.ColumnIndex == grid.Columns["Folder"].Index)
                {
                    try
                    {
                        // need to release the user's mouse
                        await Task.Run(() =>
                        {
                            using (var proc = new System.Diagnostics.Process())
                            {
                                proc.StartInfo.FileName = "explorer.exe";
                                proc.StartInfo.Arguments = "/select,\"" + fileName + "\"";
                                proc.StartInfo.ErrorDialog = true;
                                proc.Start();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to start explorer.exe\n\n" + ex.Message);
                    }
                }
                else if (e.ColumnIndex == grid.Columns["Filename"].Index)
                {
                    try
                    {
                        // need to release the user's mouse
                        await Task.Run(() =>
                        {
                            using (var proc = new System.Diagnostics.Process())
                            {
                                proc.StartInfo.FileName = fileName;
                                proc.StartInfo.ErrorDialog = true;
                                proc.Start();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to open " + fileName + "\n\n" + ex.Message);
                    }
                }
            }
        }
    }
}
