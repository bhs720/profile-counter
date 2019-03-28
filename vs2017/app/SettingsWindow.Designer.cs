/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 12/24/2014
 * Time: 5:00 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace TIFPDFCounter
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
            this.button1 = new System.Windows.Forms.Button();
            this.chkImagePixels = new System.Windows.Forms.CheckBox();
            this.txtColorSensitivity = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbColorSensitivity = new System.Windows.Forms.TrackBar();
            this.chkColorAnalysis = new System.Windows.Forms.CheckBox();
            this.lblAppName = new System.Windows.Forms.Label();
            this.lblAppVersion = new System.Windows.Forms.Label();
            this.lblAppWebSite = new System.Windows.Forms.LinkLabel();
            this.chkDuplicateFiles = new System.Windows.Forms.CheckBox();
            this.chkUpdates = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbColorSensitivity)).BeginInit();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(143, 311);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOKClick);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(224, 311);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
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
            this.panel1.TabIndex = 5;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.button1);
            this.groupBox1.Controls.Add(this.chkImagePixels);
            this.groupBox1.Controls.Add(this.txtColorSensitivity);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tbColorSensitivity);
            this.groupBox1.Location = new System.Drawing.Point(12, 79);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(12);
            this.groupBox1.Size = new System.Drawing.Size(284, 145);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(146, 56);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(54, 22);
            this.button1.TabIndex = 3;
            this.button1.Text = "Default";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // chkImagePixels
            // 
            this.chkImagePixels.AutoSize = true;
            this.chkImagePixels.Location = new System.Drawing.Point(15, 28);
            this.chkImagePixels.Name = "chkImagePixels";
            this.chkImagePixels.Size = new System.Drawing.Size(123, 17);
            this.chkImagePixels.TabIndex = 0;
            this.chkImagePixels.Text = "Check image pixels";
            this.chkImagePixels.UseVisualStyleBackColor = true;
            // 
            // txtColorSensitivity
            // 
            this.txtColorSensitivity.Location = new System.Drawing.Point(113, 56);
            this.txtColorSensitivity.Name = "txtColorSensitivity";
            this.txtColorSensitivity.Size = new System.Drawing.Size(27, 22);
            this.txtColorSensitivity.TabIndex = 2;
            this.txtColorSensitivity.TextChanged += new System.EventHandler(this.txtColorSensitivity_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(232, 81);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(32, 13);
            this.label4.TabIndex = 5;
            this.label4.Text = "High";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 81);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(28, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Low";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 59);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(92, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Color Sensitivity:";
            // 
            // tbColorSensitivity
            // 
            this.tbColorSensitivity.Location = new System.Drawing.Point(15, 95);
            this.tbColorSensitivity.Maximum = 100;
            this.tbColorSensitivity.Name = "tbColorSensitivity";
            this.tbColorSensitivity.Size = new System.Drawing.Size(249, 45);
            this.tbColorSensitivity.TabIndex = 6;
            this.tbColorSensitivity.TickFrequency = 10;
            this.tbColorSensitivity.Value = 75;
            this.tbColorSensitivity.Scroll += new System.EventHandler(this.tbColorSensitivity_Scroll);
            // 
            // chkColorAnalysis
            // 
            this.chkColorAnalysis.AutoSize = true;
            this.chkColorAnalysis.Location = new System.Drawing.Point(27, 79);
            this.chkColorAnalysis.Name = "chkColorAnalysis";
            this.chkColorAnalysis.Size = new System.Drawing.Size(141, 17);
            this.chkColorAnalysis.TabIndex = 0;
            this.chkColorAnalysis.Text = "Perform Color Analysis";
            this.chkColorAnalysis.UseVisualStyleBackColor = true;
            this.chkColorAnalysis.CheckedChanged += new System.EventHandler(this.cbColorAnalysis_CheckedChanged);
            // 
            // lblAppName
            // 
            this.lblAppName.Location = new System.Drawing.Point(67, 16);
            this.lblAppName.Name = "lblAppName";
            this.lblAppName.Size = new System.Drawing.Size(232, 15);
            this.lblAppName.TabIndex = 9;
            this.lblAppName.Text = "App Name";
            this.lblAppName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAppVersion
            // 
            this.lblAppVersion.Location = new System.Drawing.Point(67, 30);
            this.lblAppVersion.Name = "lblAppVersion";
            this.lblAppVersion.Size = new System.Drawing.Size(232, 15);
            this.lblAppVersion.TabIndex = 9;
            this.lblAppVersion.Text = "App Version";
            this.lblAppVersion.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAppWebSite
            // 
            this.lblAppWebSite.Location = new System.Drawing.Point(67, 46);
            this.lblAppWebSite.Name = "lblAppWebSite";
            this.lblAppWebSite.Size = new System.Drawing.Size(232, 15);
            this.lblAppWebSite.TabIndex = 9;
            this.lblAppWebSite.TabStop = true;
            this.lblAppWebSite.Text = "App Web Site";
            this.lblAppWebSite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel1LinkClicked);
            // 
            // chkDuplicateFiles
            // 
            this.chkDuplicateFiles.AutoSize = true;
            this.chkDuplicateFiles.Location = new System.Drawing.Point(27, 240);
            this.chkDuplicateFiles.Name = "chkDuplicateFiles";
            this.chkDuplicateFiles.Size = new System.Drawing.Size(150, 17);
            this.chkDuplicateFiles.TabIndex = 2;
            this.chkDuplicateFiles.Text = "Check for duplicate files";
            this.chkDuplicateFiles.UseVisualStyleBackColor = true;
            // 
            // chkUpdates
            // 
            this.chkUpdates.AutoSize = true;
            this.chkUpdates.Location = new System.Drawing.Point(27, 263);
            this.chkUpdates.Name = "chkUpdates";
            this.chkUpdates.Size = new System.Drawing.Size(177, 17);
            this.chkUpdates.TabIndex = 3;
            this.chkUpdates.Text = "Check for updates on startup";
            this.chkUpdates.UseVisualStyleBackColor = true;
            // 
            // SettingsWindow
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(311, 346);
            this.Controls.Add(this.chkColorAnalysis);
            this.Controls.Add(this.chkUpdates);
            this.Controls.Add(this.chkDuplicateFiles);
            this.Controls.Add(this.lblAppWebSite);
            this.Controls.Add(this.lblAppVersion);
            this.Controls.Add(this.lblAppName);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
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
            ((System.ComponentModel.ISupportInitialize)(this.tbColorSensitivity)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.CheckBox chkColorAnalysis;
        private System.Windows.Forms.CheckBox chkDuplicateFiles;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TrackBar tbColorSensitivity;
        private System.Windows.Forms.TextBox txtColorSensitivity;
        private System.Windows.Forms.CheckBox chkUpdates;
        private System.Windows.Forms.CheckBox chkImagePixels;
        private System.Windows.Forms.Button button1;
    }
}
