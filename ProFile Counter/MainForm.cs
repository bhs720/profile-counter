using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Linq;

namespace ProFileCounter
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
				if (filename == tpcFile.FileName)
					throw new Exception("The file is already in the list.");
			}
		}

		protected override void OnDragDrop(DragEventArgs drgevent)
		{
			var dropData = drgevent.Data.GetData(DataFormats.FileDrop) as string[];
			if (dropData == null || dropData.Length < 1)
				return;

            dropFiles = GatherFileList(dropData);

			// Have to get out of this function to free the user's mouse
            timer1.Enabled = true;
			timer1.Start();
		}

        List<string> dropFiles;
		void Timer1Tick(object sender, EventArgs e)
		{
			timer1.Enabled = false;
            if (dropFiles == null || dropFiles.Count == 0)
                return;
            List<TPCFile> tpcFiles;
			using (var progressWindow = new ProgressWindow(dropFiles))
            {
                progressWindow.ShowDialog(this);
                tpcFiles = progressWindow.TPCFiles;
            }

            int totalPageCount = 0;
            foreach (var tpcFile in tpcFiles)
            {
                totalPageCount += tpcFile.Pages.Length;
                string fileName = Path.GetFileName(tpcFile.FileName);
                for (int i = 0; i < tpcFile.Pages.Length; i++)
                {
                    double width = Math.Round(tpcFile.Pages[i].Width, 2);
                    double height = Math.Round(tpcFile.Pages[i].Height, 2);
                    var size = string.Concat(width, " ", "\u00d7", " ", height);
                    int rowIndex = dgvFilePages.Rows.Add(fileName, i + 1, tpcFile.Pages[i].ColorMode, size);
                    dgvFilePages.Rows[rowIndex].Tag = tpcFile.Pages[i];
                    fileName = null;
                }
            }

            LoadedFiles.AddRange(tpcFiles);
            DoPageSizeCount();
            if (Settings.UserSettings1.CheckForDuplicateFiles)
            {
                var matches =
                    from file1 in LoadedFiles
                    join file2 in LoadedFiles on file1.MD5Hash equals file2.MD5Hash into hashGroup
                    select new { Hash = file1.MD5Hash, Files = hashGroup };

                var matchingFilesReport = new System.Text.StringBuilder();
                
                foreach (var match in matches.Distinct())
                {
                    if (match.Files.Count() > 1)
                    {
                        matchingFilesReport.AppendLine("The following files produced the same hash: " + match.Hash.ToString());
                        foreach (var file in match.Files)
                        {
                            matchingFilesReport.AppendLine(file.FileName);
                        }
                        matchingFilesReport.AppendLine();
                    }
                }
                if (!string.IsNullOrWhiteSpace(matchingFilesReport.ToString()))
                {
                    using (var reportWindow = new MatchingFilesReportWindow(matchingFilesReport.ToString()))
                    {
                        reportWindow.ShowDialog(this);
                    }
                }
            }
		}

        private List<string> GatherFileList(string[] dropItems)
        {
            var dropFiles = new List<string>();
            foreach (var dropItem in dropItems)
            {
                try
                {
                    var attr = File.GetAttributes(dropItem);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        foreach (var file in Directory.EnumerateFiles(dropItem, "*.*", SearchOption.AllDirectories))
                        {
                            CheckFileIsInList(file);
                            dropFiles.Add(file);
                        }
                    }
                        
                    else
                    {
                        CheckFileIsInList(dropItem);
                        dropFiles.Add(dropItem);
                    }
                        
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    var result = MessageBox.Show(dropItem + "\n\n" + ex.Message + "\n\nContinue loading other files?", "Problem", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                    if (result == DialogResult.OK)
                        continue;

                    return dropFiles;
                }
            }
            return dropFiles;
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
                totalPageCount += file.Pages.Length;
            }
            lblTotalPages.Text = "Total Pages: " +  totalPageCount.ToString();
		}
		
		void MainFormFormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
		{
            Settings.UserSettings1.PDFColorThreshold = FileAnalyzer.ColorThreshold;
            Settings.UserSettings1.PageStore = MasterPageList;
            Settings.UserSettings1.WindowState = WindowState;
            if (WindowState == FormWindowState.Normal)
            {
                Settings.UserSettings1.WindowSize = Size;
                Settings.UserSettings1.WindowLocation = Location;
            }
            Settings.Save();
		}
		
		void MainFormLoad(object sender, System.EventArgs e)
		{
            Settings.Load();
            FileAnalyzer.ColorThreshold = Settings.UserSettings1.PDFColorThreshold;
            Size = Settings.UserSettings1.WindowSize;
            Location = Settings.UserSettings1.WindowLocation; 
            WindowState = Settings.UserSettings1.WindowState;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            MasterPageList = Settings.UserSettings1.PageStore;
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
                if (rowIndex == -1)
                    return;
				if (!dgvCounters.Rows[rowIndex].Selected)
				{
					dgvCounters.ClearSelection();
					dgvCounters.Rows[rowIndex].Selected = true;
				}
				ctxPages.Show(dgvCounters.PointToScreen(e.Location));
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
	}
}
