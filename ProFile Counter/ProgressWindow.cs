/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 12/16/2014
 * Time: 19:49
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.ComponentModel;

namespace ProFileCounter
{
	public partial class ProgressWindow : Form
	{
        List<string> fileList;
        int fileNumber;
        FileAnalyzer analyzer;
        List<TPCFile> tpcFiles;

        public List<TPCFile> TPCFiles { get { return this.tpcFiles; } }

		public ProgressWindow()
		{
			InitializeComponent();
		}

        public ProgressWindow(List<string> fileList)
        {
            InitializeComponent();
            this.fileList = fileList;
            fileNumber = 0;
            tpcFiles = new List<TPCFile>();
            lblFileCount.Text = "";
            lblPageCount.Text = "";
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            btnCancel.Text = "Cancelling...";
            btnCancel.Enabled = false;
            analyzer.Stop();
        }

        private void ProgressWindow_Load(object sender, EventArgs e)
        {
            
        }

        void Analyze()
        {
            pbFile.Value = (fileNumber + 1) * 100 / fileList.Count;
            lblFileCount.Text = string.Format("{0} / {1}", fileNumber + 1, fileList.Count);
            analyzer = new FileAnalyzer(fileList[fileNumber]);
            analyzer.ProgressChanged += analyzer_ProgressChanged;
            analyzer.AnalysisComplete += analyzer_AnalysisComplete;
            analyzer.Go();
        }

        void analyzer_AnalysisComplete(TPCFile result, string error)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    analyzer.ProgressChanged -= analyzer_ProgressChanged;
                    analyzer.AnalysisComplete -= analyzer_AnalysisComplete;
                    if (analyzer.Cancelled)
                    {
                        this.Close();
                        return;
                    }
                    if (result == null)
                        MessageBox.Show("Could not open this file: \n" + fileList[fileNumber] + "\n" + error, "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                        tpcFiles.Add(result);
                    Debug.WriteLine("Analysis complete error: " + error);
                    fileNumber++;
                    
                    if (fileNumber == fileList.Count)
                        this.Close();
                    else
                        Analyze();
                });
            }
        }

        void analyzer_ProgressChanged(int completed, int total)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate
                {
                    pbPage.Value = completed * 100 / total;
                    lblPageCount.Text = string.Format("{0} / {1}", completed, total);
                });
            }
        }

        private void ProgressWindow_Shown(object sender, EventArgs e)
        {
            Analyze();
        }
	}
}
