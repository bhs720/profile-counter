using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace TIFPDFCounter
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            MasterPageList = new List<PageSize>();
            LoadedFiles = new List<TPCFile>();
            PageCounters = new List<PageCounter>();
        }

        List<PageSize> MasterPageList { get; set; }
        List<PageCounter> PageCounters { get; set; }
        List<TPCFile> LoadedFiles { get; set; }
        
        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data.GetDataPresent(DataFormats.FileDrop))
                drgevent.Effect = DragDropEffects.Copy;
            base.OnDragEnter(drgevent);
        }

        public void CheckFileIsInList(string filename)
        {
            foreach (var tpcFile in LoadedFiles)
            {
                if (tpcFile.Filename.Equals(filename))
                    throw new Exception("The file is already in the list.");
            }
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            var dropData = drgevent.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropData == null || dropData.Length < 1)
                return;

            dropFiles = DiscoverFiles(dropData);

            var duplicates = LoadedFiles.Select(x => x.Filename).Intersect(dropFiles).ToList();
            if (duplicates.Count > 0)
            {
                MessageBox.Show(duplicates.Count + " files will be skipped because they are already in the list.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            dropFiles = dropFiles.Except(duplicates).ToList();

            timer1.Enabled = true;
            timer1.Start();
        }

        List<string> dropFiles;
        void Timer1Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            timer1.Enabled = false;
            
            if (dropFiles == null || dropFiles.Count == 0)
                return;                

            List<TPCFile> processedFiles;
            using (var progressWindow = new ProgressWindow(dropFiles))
            {
                progressWindow.ShowDialog(this);
                processedFiles = progressWindow.TPCFiles;
            }

            // add this batch to LoadedFiles
            LoadedFiles.AddRange(processedFiles);

            if (Settings.Instance.CheckForDuplicateFiles)
            {
                var matchingFiles = LoadedFiles
                    .GroupBy(x => x.FileSize).Where(x => x.Count() > 1).SelectMany(x => x)
                    .GroupBy(x => x.MD5Hash).Where(x => x.Count() > 1);

                if (matchingFiles.Count() > 0)
                {
                    using (var reportWindow = new MatchingFilesReportWindow(matchingFiles))
                    {
                        reportWindow.ShowDialog(this);
                    }
                }
            }

            foreach (var file in processedFiles)
            {
                string fileName = Path.GetFileName(file.Filename);
                for (int i = 0; i < file.Pages.Count; i++)
                {
                    decimal width = Math.Round(file.Pages[i].Width, 2);
                    decimal height = Math.Round(file.Pages[i].Height, 2);
                    string size = string.Format("{0} \u00d7 {1}", width, height);
                    int rowIndex = dgvFilePages.Rows.Add(fileName, i + 1, file.Pages[i].ColorMode, size);
                    dgvFilePages.Rows[rowIndex].Tag = file.Pages[i];
                    fileName = null;
                }
            }
            
            DoPageSizeCount();
        }



        void UpdateUI()
        {

        }

        private List<string> DiscoverFiles(string[] dropItems)
        {
            var discoveredFiles = new List<string>();
            foreach (var dropItem in dropItems)
            {
                while (true)
                {
                    try
                    {
                        var attr = File.GetAttributes(dropItem);
                        if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            foreach (var file in Directory.EnumerateFiles(dropItem, "*.*", SearchOption.AllDirectories))
                            {
                                discoveredFiles.Add(file);
                            }
                        }
                        else
                        {
                            discoveredFiles.Add(dropItem);
                        }
                    }
                    catch (Exception ex)
                    {
                        var dr = MessageBox.Show("Failed to discover item: " + dropItem + "\n" + ex.Message, ex.GetType().ToString(), MessageBoxButtons.AbortRetryIgnore);
                        if (dr == DialogResult.Abort)
                        {
                            return discoveredFiles;
                        }
                        else if (dr == DialogResult.Retry)
                        {
                            continue;
                        }
                        else if (dr == DialogResult.Ignore)
                        {
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }

                    break;
                }
                
            }

            discoveredFiles.Sort(new NaturalStringComparer());
            
            return discoveredFiles;
        }

        void DoPageSizeCount()
        {
            dgvCounters.Rows.Clear();
            PageCounters.Clear();

            foreach (var pageSize in MasterPageList)
            {
                if (pageSize.Active)
                    PageCounters.Add(new PageCounter(pageSize));
            }

            var uknownPageSizeCounter = new PageCounter(null);

            foreach (DataGridViewRow dgvr in dgvFilePages.Rows)
            {
                var tpcFilePage = dgvr.Tag as TPCFilePage;

                bool match = false;
                foreach (var pageCounter in PageCounters)
                {
                    if (pageCounter.CountIfMatched(tpcFilePage))
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    uknownPageSizeCounter.CountIfMatched(tpcFilePage);
                }
            }

            dgvCounters.Rows.Clear();
            PageCounters.Insert(0, uknownPageSizeCounter);
            foreach (var pageCounter in PageCounters)
            {
                if (pageCounter.BWPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(pageCounter.Name, "BW", pageCounter.BWPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = pageCounter.BWPages;
                }
                if (pageCounter.ColorPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(pageCounter.Name, "Color", pageCounter.ColorPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = pageCounter.ColorPages;
                }
                if (pageCounter.UnknownColorPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(pageCounter.Name, "Unknown", pageCounter.UnknownColorPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = pageCounter.UnknownColorPages;
                }
            }
            lblTotalFiles.Text = "Total Files: " + LoadedFiles.Count.ToString();
            int totalPageCount = 0;
            foreach (var file in LoadedFiles)
            {
                totalPageCount += file.Pages.Count;
            }
            lblTotalPages.Text = "Total Pages: " + totalPageCount.ToString();
        }

        void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Instance.PageStore = MasterPageList;
            Settings.Instance.WindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                Settings.Instance.WindowSize = Size;
                Settings.Instance.WindowLocation = Location;
            }
            Settings.Save();
        }

        void MainFormLoad(object sender, System.EventArgs e)
        {
            Settings.Load();
            Size = Settings.Instance.WindowSize;
            Location = Settings.Instance.WindowLocation;
            WindowState = Settings.Instance.WindowState;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            MasterPageList = Settings.Instance.PageStore;
        }

        void BtnManageClick(object sender, EventArgs e)
        {
            using (var psm = new PageSizeManager(MasterPageList))
            {
                psm.ShowDialog(this);
                if (psm.DialogResult == DialogResult.OK)
                {
                    MasterPageList.Clear();
                    MasterPageList = psm.PageSizes;
                }
            }
            DoPageSizeCount();
        }

        void BtnClearListClick(object sender, EventArgs e)
        {
            lblTotalPages.Text = "Total Pages: 0";
            lblTotalFiles.Text = "Total Files: 0";
            lblSelected.Text = "Selected: 0";
            dgvFilePages.Rows.Clear();
            dgvCounters.Rows.Clear();
            LoadedFiles.Clear();
        }

        void HighlightPagesToolStripMenuItemClick(object sender, EventArgs e)
        {
            dgvFilePages.ClearSelection();
            var tagsToSelect = new List<TPCFilePage>();
            foreach (DataGridViewRow counterRow in dgvCounters.SelectedRows)
            {
                tagsToSelect.AddRange(counterRow.Tag as List<TPCFilePage>);
            }
            foreach (DataGridViewRow row in dgvFilePages.Rows)
            {
                for (int i = 0; i < tagsToSelect.Count; i++)
                {
                    if ((row.Tag as TPCFilePage).Equals(tagsToSelect[i]))
                    {
                        row.Selected = true;
                        tagsToSelect.RemoveAt(i);
                    }
                }
            }
        }

        void DgvCountersMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int rowIndex = dgvCounters.HitTest(e.X, e.Y).RowIndex;
                if (rowIndex >= 0)
                {
                    if (!dgvCounters.Rows[rowIndex].Selected)
                    {
                        dgvCounters.ClearSelection();
                        dgvCounters.Rows[rowIndex].Selected = true;
                    }
                    ctxPages.Show(dgvCounters.PointToScreen(e.Location));
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1:
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        private void dgvFilePages_SelectionChanged(object sender, EventArgs e)
        {
            lblSelected.Text = "Selected: " + dgvFilePages.SelectedRows.Count;
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            using (var settingsWindow = new SettingsWindow())
            {
                var result = settingsWindow.ShowDialog(this);
            }
        }

        [SuppressUnmanagedCodeSecurity]
        internal static class SafeNativeMethods
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            public static extern int StrCmpLogicalW(string psz1, string psz2);
        }

        public sealed class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                return SafeNativeMethods.StrCmpLogicalW(a, b);
            }
        }

        public sealed class NaturalFileInfoNameComparer : IComparer<FileInfo>
        {
            public int Compare(FileInfo a, FileInfo b)
            {
                return SafeNativeMethods.StrCmpLogicalW(a.Name, b.Name);
            }
        }
    }

    
}
