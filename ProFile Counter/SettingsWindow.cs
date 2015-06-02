/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 12/24/2014
 * Time: 5:00 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProFileCounter
{
	/// <summary>
	/// Description of Settings.
	/// </summary>
	public partial class SettingsWindow : Form
	{
		public SettingsWindow()
		{
			InitializeComponent();
            textBox1.Text = "Some images may appear to be black and white, but actually contain color information. Set the threshold to a value between 0 and 1, which represents how colorful an image can be and still be considered B&W.\r\n\r\nEnter 0 to disable.\r\n\r\nRecommended setting is between 0.15 and 0.25.";
        }

        private void SettingsWindow_Shown(object sender, EventArgs e)
        {
            lblAppName.Text = Application.ProductName;
            lblAppVersion.Text = "Version " + Application.ProductVersion;
            lblAppWebSite.Text = "https://bhs720.github.io/profile-counter";

            txtPdfColorTolerance.Text = FileAnalyzer.ColorThreshold.ToString();
            cbColorAnalysis.Checked = Settings.UserSettings1.PerformColorAnalysis;
            txtPdfColorTolerance.Enabled = cbColorAnalysis.Checked;
            cbDuplicateFiles.Checked = Settings.UserSettings1.CheckForDuplicateFiles;
            cbUpdates.Checked = Settings.UserSettings1.CheckForProgramUpdates;
        }

		void SettingsFormClosing(object sender, FormClosingEventArgs e)
		{
			if (DialogResult == DialogResult.OK && txtPdfColorTolerance.Enabled)
			{
				try
				{  
                    float value = float.Parse(txtPdfColorTolerance.Text);
                    if (value < 0.0f || value > 1.0f)
						throw new Exception();
                    FileAnalyzer.ColorThreshold = value;
                    Settings.UserSettings1.PDFColorThreshold = value;
				}
				catch (Exception)
				{
					MessageBox.Show("Value must be a number between zero and one.", "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
					e.Cancel = true;
                    return;
				}
			}

            Settings.UserSettings1.CheckForProgramUpdates = cbUpdates.Checked;
            Settings.UserSettings1.CheckForDuplicateFiles = cbDuplicateFiles.Checked;
            Settings.UserSettings1.PerformColorAnalysis = cbColorAnalysis.Checked;
            Settings.Save();
		}
		void BtnOKClick(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			Close();
		}

		void BtnDefaultClick(object sender, EventArgs e)
		{
			txtPdfColorTolerance.Text = "0.25";
		}

		void LinkLabel1LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start(lblAppWebSite.Text);
		}

        private void cbColorAnalysis_CheckedChanged(object sender, EventArgs e)
        {
            if (cbColorAnalysis.Checked)
                txtPdfColorTolerance.Enabled = true;
            else
                txtPdfColorTolerance.Enabled = false;
        }

        

	}
}
