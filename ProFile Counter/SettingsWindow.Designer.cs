/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 12/24/2014
 * Time: 5:00 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace ProFileCounter
{
	partial class SettingsWindow
	{
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.TextBox txtPdfColorTolerance;
        private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label lblAppName;
		private System.Windows.Forms.Label lblAppVersion;
		private System.Windows.Forms.LinkLabel lblAppWebSite;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsWindow));
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.cbColorAnalysis = new System.Windows.Forms.CheckBox();
            this.txtPdfColorTolerance = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lblAppName = new System.Windows.Forms.Label();
            this.lblAppVersion = new System.Windows.Forms.Label();
            this.lblAppWebSite = new System.Windows.Forms.LinkLabel();
            this.cbUpdates = new System.Windows.Forms.CheckBox();
            this.cbDuplicateFiles = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(133, 371);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOKClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(214, 371);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("panel1.BackgroundImage")));
            this.panel1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.panel1.Location = new System.Drawing.Point(13, 13);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(48, 48);
            this.panel1.TabIndex = 3;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.textBox1);
            this.groupBox1.Controls.Add(this.cbColorAnalysis);
            this.groupBox1.Controls.Add(this.txtPdfColorTolerance);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(13, 80);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(12);
            this.groupBox1.Size = new System.Drawing.Size(276, 196);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            // 
            // textBox1
            // 
            this.textBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox1.Location = new System.Drawing.Point(15, 24);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(246, 128);
            this.textBox1.TabIndex = 4;
            // 
            // cbColorAnalysis
            // 
            this.cbColorAnalysis.AutoSize = true;
            this.cbColorAnalysis.Location = new System.Drawing.Point(15, 0);
            this.cbColorAnalysis.Name = "cbColorAnalysis";
            this.cbColorAnalysis.Size = new System.Drawing.Size(158, 17);
            this.cbColorAnalysis.TabIndex = 3;
            this.cbColorAnalysis.Text = "Perform color analysis (slow)";
            this.cbColorAnalysis.UseVisualStyleBackColor = true;
            this.cbColorAnalysis.CheckedChanged += new System.EventHandler(this.cbColorAnalysis_CheckedChanged);
            // 
            // txtPdfColorTolerance
            // 
            this.txtPdfColorTolerance.Location = new System.Drawing.Point(127, 158);
            this.txtPdfColorTolerance.Name = "txtPdfColorTolerance";
            this.txtPdfColorTolerance.Size = new System.Drawing.Size(60, 20);
            this.txtPdfColorTolerance.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(12, 161);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(109, 23);
            this.label1.TabIndex = 0;
            this.label1.Text = "Color Threshold";
            // 
            // lblAppName
            // 
            this.lblAppName.Location = new System.Drawing.Point(67, 16);
            this.lblAppName.Name = "lblAppName";
            this.lblAppName.Size = new System.Drawing.Size(200, 15);
            this.lblAppName.TabIndex = 4;
            this.lblAppName.Text = "App Name";
            this.lblAppName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAppVersion
            // 
            this.lblAppVersion.Location = new System.Drawing.Point(67, 30);
            this.lblAppVersion.Name = "lblAppVersion";
            this.lblAppVersion.Size = new System.Drawing.Size(200, 15);
            this.lblAppVersion.TabIndex = 5;
            this.lblAppVersion.Text = "App Version";
            this.lblAppVersion.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAppWebSite
            // 
            this.lblAppWebSite.Location = new System.Drawing.Point(67, 46);
            this.lblAppWebSite.Name = "lblAppWebSite";
            this.lblAppWebSite.Size = new System.Drawing.Size(200, 15);
            this.lblAppWebSite.TabIndex = 6;
            this.lblAppWebSite.TabStop = true;
            this.lblAppWebSite.Text = "App Web Site";
            this.lblAppWebSite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1LinkClicked);
            // 
            // cbUpdates
            // 
            this.cbUpdates.AutoSize = true;
            this.cbUpdates.Location = new System.Drawing.Point(28, 323);
            this.cbUpdates.Name = "cbUpdates";
            this.cbUpdates.Size = new System.Drawing.Size(204, 17);
            this.cbUpdates.TabIndex = 2;
            this.cbUpdates.Text = "Check for program updates on startup";
            this.cbUpdates.UseVisualStyleBackColor = true;
            // 
            // cbDuplicateFiles
            // 
            this.cbDuplicateFiles.AutoSize = true;
            this.cbDuplicateFiles.Location = new System.Drawing.Point(28, 300);
            this.cbDuplicateFiles.Name = "cbDuplicateFiles";
            this.cbDuplicateFiles.Size = new System.Drawing.Size(139, 17);
            this.cbDuplicateFiles.TabIndex = 4;
            this.cbDuplicateFiles.Text = "Check for duplicate files";
            this.cbDuplicateFiles.UseVisualStyleBackColor = true;
            // 
            // SettingsWindow
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(301, 406);
            this.Controls.Add(this.cbDuplicateFiles);
            this.Controls.Add(this.cbUpdates);
            this.Controls.Add(this.lblAppWebSite);
            this.Controls.Add(this.lblAppVersion);
            this.Controls.Add(this.lblAppName);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.HelpButton = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsWindow";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SettingsFormClosing);
            this.Shown += new System.EventHandler(this.SettingsWindow_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.CheckBox cbColorAnalysis;
        private System.Windows.Forms.CheckBox cbUpdates;
        private System.Windows.Forms.CheckBox cbDuplicateFiles;
        private System.Windows.Forms.TextBox textBox1;
	}
}
