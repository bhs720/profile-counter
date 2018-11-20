using System;
using System.Drawing;
using System.Windows.Forms;

namespace TIFPDFCounter
{
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
            
            txtColorSensitivity.Text = Math.Round((100 - Settings.Instance.ColorThreshold * 100)).ToString();
            chkColorAnalysis.Checked = Settings.Instance.PerformColorAnalysis;
            groupBox1.Enabled = chkColorAnalysis.Checked;
            chkDuplicateFiles.Checked = Settings.Instance.CheckForDuplicateFiles;
            chkImagePixels.Checked = Settings.Instance.CheckImagePixels;
            chkUpdates.Checked = Settings.Instance.CheckForProgramUpdates;
        }

		void SettingsFormClosing(object sender, FormClosingEventArgs e)
		{
            if (DialogResult == DialogResult.OK)
            {
                if (chkColorAnalysis.Checked)
                {
                    try
                    {
                        int sensitivity = int.Parse(txtColorSensitivity.Text);
                        decimal threshold = (100 - sensitivity) / (decimal)100;
                        Settings.Instance.ColorThreshold = threshold;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        e.Cancel = true;
                    }
                }

                Settings.Instance.CheckImagePixels = chkImagePixels.Checked;
                Settings.Instance.CheckForDuplicateFiles = chkDuplicateFiles.Checked;
                Settings.Instance.PerformColorAnalysis = chkColorAnalysis.Checked;
                Settings.Instance.CheckForProgramUpdates = chkUpdates.Checked;
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
            using (var proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.FileName = lblAppWebSite.Text;
                proc.StartInfo.ErrorDialog = true;
                proc.Start();
            }
		}

        private void cbColorAnalysis_CheckedChanged(object sender, EventArgs e)
        {
            groupBox1.Enabled = chkColorAnalysis.Checked;
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

        private void button1_Click(object sender, EventArgs e)
        {
            txtColorSensitivity.Text = "75";
        }
    }
}
