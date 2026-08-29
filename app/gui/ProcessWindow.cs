using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public partial class ProcessWindow : Form
    {
        public IReadOnlyList<TPCFile> Results { get { return batch.Results; } }

        private readonly AnalysisBatch batch;
        private readonly DataGridViewRow[] rows;
        private bool batchFinished;
        private bool batchCancelled;

        public ProcessWindow()
        {
            InitializeComponent();
        }

        public ProcessWindow(List<string> filenames) : this()
        {
            var options = new AnalysisOptions
            {
                PerformColorAnalysis = Settings.Current.PerformColorAnalysis,
                ColorThreshold = Settings.Current.ColorThreshold,
                CheckImagePixels = Settings.Current.CheckImagePixels
            };

            batch = new AnalysisBatch(filenames, options, new PfcToolProcessFactory());
            rows = new DataGridViewRow[batch.Items.Count];

            batch.FileStarted += Batch_FileStarted;
            batch.FileProgress += Batch_FileProgress;
            batch.FileCompleted += Batch_FileCompleted;
            batch.BatchFinished += Batch_BatchFinished;

            int colIndex = grid.Columns.Add(new DataGridViewProgressColumn());
            grid.Columns[colIndex].Name = "Progress";
            grid.Columns[colIndex].HeaderText = "Progress";
        }

        private void Batch_FileStarted(BatchItem item)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                rows[item.Index].Cells["Status"].Value = "Processing";
            });
        }

        private void Batch_FileProgress(BatchItem item, int completed, int total)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                var dgvr = rows[item.Index];

                // Posting rather than blocking means a progress update can arrive after
                // the file finished and its row was removed. Nothing to draw in that case.
                if (dgvr.DataGridView == null || total <= 0)
                    return;

                dgvr.Cells["Progress"].Value = completed * 100 / total;
            });
        }

        private void Batch_FileCompleted(BatchItem item, FileAnalyzer analyzer)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                var dgvr = rows[item.Index];

                if (analyzer.Cancelled)
                {
                    dgvr.Cells["Status"].Value = "Cancelled";
                }
                else if (analyzer.Failed)
                {
                    string errorMessage = "Failed: " + analyzer.Errors.ToString(0, Math.Min(analyzer.Errors.Length, 255));
                    dgvr.Cells["Status"].Value = errorMessage;
                    dgvr.DefaultCellStyle.BackColor = Color.DarkRed;
                    dgvr.DefaultCellStyle.ForeColor = Color.White;
                    dgvr.DefaultCellStyle.SelectionBackColor = Color.Red;
                    dgvr.DefaultCellStyle.SelectionForeColor = Color.White;
                }
                else
                {
                    System.Diagnostics.Debug.Assert(analyzer.Result != null);
                    grid.Rows.Remove(dgvr);
                }

                ScrollToFirstProcessingRow();
            });
        }

        private void Batch_BatchFinished()
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                batchFinished = true;

                if (batchCancelled || batch.Failures.Count == 0)
                {
                    Close();
                }
                else
                {
                    Text = "Processing finished with errors";
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
                if (dgvr != null && dgvr.Index >= 0 && dgvr.Index < grid.Rows.Count)
                {
                    grid.FirstDisplayedScrollingRowIndex = dgvr.Index;
                }
            }
        }

        private void ProcessWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!batchFinished)
            {
                e.Cancel = true;
                batchCancelled = true;
                batch.Cancel();
            }
        }

        private void ProcessWindow_Load(object sender, EventArgs e)
        {
            foreach (var item in batch.Items)
            {
                string fileSize;
                long fileLength;
                if (Utility.TryGetFileLength(item.Filename, out fileLength))
                {
                    fileSize = Utility.BytesToString(fileLength);
                }
                else
                {
                    fileSize = "Unknown";
                }

                string folder = System.IO.Path.GetDirectoryName(item.Filename);
                string fileName = System.IO.Path.GetFileName(item.Filename);
                string extension = System.IO.Path.GetExtension(item.Filename);
                int rowIndex = grid.Rows.Add(folder, fileName, extension, fileSize, "Queued", 0);
                grid.Rows[rowIndex].Tag = item.Filename;
                rows[item.Index] = grid.Rows[rowIndex];
            }

            batch.Start();
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
