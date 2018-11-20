using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace TIFPDFCounter
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            lblUpdate.Text = "";
            AcceptedFiles = new List<TPCFile>();
        }
        
        List<TPCFile> AcceptedFiles { get; set; }
        
        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data.GetDataPresent(DataFormats.FileDrop))
                drgevent.Effect = DragDropEffects.Copy;
            base.OnDragEnter(drgevent);
        }
        
        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            var dropData = drgevent.Data.GetData(DataFormats.FileDrop) as string[];
            if (dropData == null || dropData.Length < 1)
                return;

            dropFiles = DiscoverFiles(dropData);

            var duplicates = AcceptedFiles.Select(x => x.Filename).Intersect(dropFiles).ToList();
            if (duplicates.Count > 0)
            {
                MessageBox.Show(duplicates.Count + " files will be skipped because they are already in the list.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            dropFiles = dropFiles.Except(duplicates).ToList();

            timer1.Enabled = true;
            timer1.Start();
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

        List<string> dropFiles;
        void Timer1Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            timer1.Enabled = false;
            
            if (dropFiles == null || dropFiles.Count == 0)
                return;                

            List<TPCFile> processedFiles;
            using (var processWindow = new ProcessWindow(dropFiles))
            {
                AllowDrop = false;
                processWindow.ShowDialog(this);
                processedFiles = processWindow.Results;
                AllowDrop = true;
            }

            // add this batch to LoadedFiles
            AcceptedFiles.AddRange(processedFiles);

            if (Settings.Instance.CheckForDuplicateFiles)
            {
                var matchingFiles = AcceptedFiles
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

            foreach (var p in processedFiles
                .OrderBy(f => f.Filename, new NaturalStringComparer())
                .SelectMany(f => f.Pages))
            {
                string filename = p.PageNumber == 1 ? Path.GetFileName(p.ParentFile.Filename) : "";
                string pageNumber = p.PageNumber.ToString();
                string pageColor = p.ColorMode.ToString();
                string pageSize = string.Format("{0:F2} \u00d7 {1:F2}", p.Width, p.Height);
                int rowIndex = dgvFilePages.Rows.Add(filename, pageNumber, pageColor, pageSize);
                dgvFilePages.Rows[rowIndex].Tag = p;
            }

            DoPageSizeCount();
        }
        
        void DoPageSizeCount()
        {
            dgvCounters.Rows.Clear();

            var activePageSizes = Settings.Instance.PageStore.Where(p => p.Active == true).Select(ps => new PageSizeCounter(ps)).ToList();

            // Add a catch-all "Uknown" page size
            activePageSizes.Add(new PageSizeCounter(null));

            var pagesToCompare = new Queue<TPCFilePage>(AcceptedFiles.SelectMany(af => af.Pages));

            while (pagesToCompare.Count > 0)
            {
                var page = pagesToCompare.Dequeue();
                var match = activePageSizes.First(aps => aps.IsMatch(page));
            }
            
            foreach (var aps in activePageSizes)
            {
                if (aps.BlackPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(aps.Name, "BW", aps.BlackPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = aps.BlackPages;
                }
                if (aps.ColorPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(aps.Name, "Color", aps.ColorPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = aps.ColorPages;
                }
                if (aps.UnknownPages.Count > 0)
                {
                    int rowIndex = dgvCounters.Rows.Add(aps.Name, "Unknown", aps.UnknownPages.Count);
                    dgvCounters.Rows[rowIndex].Tag = aps.UnknownPages;
                }
            }

            lblTotalFiles.Text = "Total Files: " + AcceptedFiles.Count.ToString();
            lblTotalPages.Text = "Total Pages: " + AcceptedFiles.Sum(af => af.PageCount).ToString();
        }

        void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Instance.WindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                Settings.Instance.WindowSize = Size;
                Settings.Instance.WindowLocation = Location;
            }
            Settings.Save();
        }

        async void MainFormLoad(object sender, EventArgs e)
        {
            Settings.Load();
            Size = Settings.Instance.WindowSize;
            Location = Settings.Instance.WindowLocation;
            WindowState = Settings.Instance.WindowState;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            
            if (Settings.Instance.CheckForProgramUpdates)
                await CheckForProgramUpdates();
        }

        async Task CheckForProgramUpdates()
        {
            try
            {
                string json = "";
                using (var webClient = new WebClient())
                {
                    webClient.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                    json = await webClient.DownloadStringTaskAsync(Settings.Instance.AppUpdateJsonUrl);
                }
                dynamic obj = JsonConvert.DeserializeObject<dynamic>(json);
                string latestVersion = obj.version;
                string downloadLink = obj.link;

                if (CompareVersions(latestVersion, Application.ProductVersion))
                {
                    Debug.Print("Post update: " + (string)obj.link);
                    lblUpdate.Text = "Update available";
                    lblUpdate.IsLink = true;
                    lblUpdate.Click += delegate
                    {
                        using (var proc = new Process())
                        {
                            proc.StartInfo.FileName = downloadLink;
                            proc.StartInfo.ErrorDialog = true;
                            proc.Start();
                        }
                    };
                }
                else
                {
                    Debug.Print("No update available");
                }
            }
            catch (Exception ex)
            {
                // swallow the exception
                Debug.Print("Update check failed: " + ex.Message);
            }
        }

        /// <summary>
        /// return true if latestVersion is greater than currentVersion
        /// </summary>
        /// <param name="latestVersion"></param>
        /// <param name="currentVersion"></param>
        /// <returns></returns>
        bool CompareVersions(string latestVersion, string currentVersion)
        {
            Debug.Print("LatestVersion=" + latestVersion + " CurrentVersion=" + currentVersion);
            var cvSplit = currentVersion.Split('.');
            var lvSplit = latestVersion.Split('.');
            for (int i = 0; i < cvSplit.Length && i < lvSplit.Length; i++)
            {
                int cv = Convert.ToInt32(cvSplit[i]);
                int lv = Convert.ToInt32(lvSplit[i]);
                if (lv > cv)
                {
                    return true;
                }
            }

            return false;
        }

        void BtnManageClick(object sender, EventArgs e)
        {
            using (var psm = new PageSizeManager(Settings.Instance.PageStore))
            {
                psm.ShowDialog(this);
                if (psm.DialogResult == DialogResult.OK)
                {
                    Settings.Instance.PageStore = psm.PageSizes;
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
            AcceptedFiles.Clear();
        }

        void HighlightPagesToolStripMenuItemClick(object sender, EventArgs e)
        {
            var tagsToSelect = dgvCounters
                .SelectedRows
                .Cast<DataGridViewRow>()
                .SelectMany(dgvr => dgvr.Tag as List<TPCFilePage>)
                .ToList();

            dgvFilePages.ClearSelection();

            dgvFilePages.Rows
                .Cast<DataGridViewRow>()
                .Where(dgvr => tagsToSelect.Contains(dgvr.Tag as TPCFilePage)).ToList()
                .ForEach(dgvr => dgvr.Selected = true);
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
