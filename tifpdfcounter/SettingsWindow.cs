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

namespace TIFPDFCounter
{
	/// <summary>
	/// Description of Settings.
	/// </summary>
	public partial class SettingsWindow : Form
	{
		public SettingsWindow()
		{
			InitializeComponent();
        }

        private void SettingsWindow_Shown(object sender, EventArgs e)
        {
            lblAppName.Text = Application.ProductName;
            lblAppVersion.Text = "Version " + Application.ProductVersion;
            lblAppWebSite.Text = "https://bhs720.github.io/profile-counter";

            txtColorSensitivity.Text = Math.Round((100 - FileAnalyzer.ColorThreshold * 100)).ToString();
            cbColorAnalysis.Checked = Settings.UserSettings1.PerformColorAnalysis;
            txtColorSensitivity.Enabled = cbColorAnalysis.Checked;
            tbColorSensitivity.Enabled = cbColorAnalysis.Checked;
            cbDuplicateFiles.Checked = Settings.UserSettings1.CheckForDuplicateFiles;
        }

		void SettingsFormClosing(object sender, FormClosingEventArgs e)
		{
            if (DialogResult == DialogResult.OK)
            {
                if (cbColorAnalysis.Checked)
                {
                    try
                    {
                        int value = int.Parse(txtColorSensitivity.Text);
                        float valueF = (100 - value) / (float)100;
                        FileAnalyzer.ColorThreshold = valueF;
                        Settings.UserSettings1.PDFColorThreshold = valueF;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        e.Cancel = true;
                    }
                }

                Settings.UserSettings1.CheckForDuplicateFiles = cbDuplicateFiles.Checked;
                Settings.UserSettings1.PerformColorAnalysis = cbColorAnalysis.Checked;
                Settings.Save();
            }
		}
		void BtnOKClick(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			Close();
		}

		void LinkLabel1LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start(lblAppWebSite.Text);
		}

        private void cbColorAnalysis_CheckedChanged(object sender, EventArgs e)
        {
            btnSetDefault.Enabled = cbColorAnalysis.Checked;
            txtColorSensitivity.Enabled = cbColorAnalysis.Checked;
            tbColorSensitivity.Enabled = cbColorAnalysis.Checked;
        }

        private void txtColorSensitivity_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int value = int.Parse(txtColorSensitivity.Text);
                if (value < 0 || value > 100)
                    throw new Exception("You must enter a number between 0 and 100.");
                tbColorSensitivity.Value = value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtColorSensitivity.Text = tbColorSensitivity.Value.ToString();
            }
        }

        private void tbColorSensitivity_Scroll(object sender, EventArgs e)
        {
            txtColorSensitivity.Text = tbColorSensitivity.Value.ToString();
        }

        private void btnSetDefault_Click(object sender, EventArgs e)
        {
            txtColorSensitivity.Text = "75";
        }
	}
}
