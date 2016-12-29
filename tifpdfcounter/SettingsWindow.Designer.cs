﻿/*
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
            this.btnSetDefault = new System.Windows.Forms.Button();
            this.txtColorSensitivity = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.tbColorSensitivity = new System.Windows.Forms.TrackBar();
            this.cbColorAnalysis = new System.Windows.Forms.CheckBox();
            this.lblAppName = new System.Windows.Forms.Label();
            this.lblAppVersion = new System.Windows.Forms.Label();
            this.lblAppWebSite = new System.Windows.Forms.LinkLabel();
            this.cbDuplicateFiles = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbColorSensitivity)).BeginInit();
            this.SuspendLayout();
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(133, 264);
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
            this.btnCancel.Location = new System.Drawing.Point(214, 264);
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
            this.groupBox1.Controls.Add(this.btnSetDefault);
            this.groupBox1.Controls.Add(this.txtColorSensitivity);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.tbColorSensitivity);
            this.groupBox1.Controls.Add(this.cbColorAnalysis);
            this.groupBox1.Location = new System.Drawing.Point(13, 80);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(12);
            this.groupBox1.Size = new System.Drawing.Size(276, 117);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            // 
            // btnSetDefault
            // 
            this.btnSetDefault.Location = new System.Drawing.Point(167, 27);
            this.btnSetDefault.Name = "btnSetDefault";
            this.btnSetDefault.Size = new System.Drawing.Size(75, 23);
            this.btnSetDefault.TabIndex = 10;
            this.btnSetDefault.Text = "Set Default";
            this.btnSetDefault.UseVisualStyleBackColor = true;
            this.btnSetDefault.Click += new System.EventHandler(this.btnSetDefault_Click);
            // 
            // txtColorSensitivity
            // 
            this.txtColorSensitivity.Location = new System.Drawing.Point(105, 29);
            this.txtColorSensitivity.Name = "txtColorSensitivity";
            this.txtColorSensitivity.Size = new System.Drawing.Size(56, 20);
            this.txtColorSensitivity.TabIndex = 9;
            this.txtColorSensitivity.TextChanged += new System.EventHandler(this.txtColorSensitivity_TextChanged);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(232, 89);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(29, 13);
            this.label4.TabIndex = 8;
            this.label4.Text = "High";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 89);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(27, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Low";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 32);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(84, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Color Sensitivity:";
            // 
            // tbColorSensitivity
            // 
            this.tbColorSensitivity.Location = new System.Drawing.Point(15, 57);
            this.tbColorSensitivity.Maximum = 100;
            this.tbColorSensitivity.Name = "tbColorSensitivity";
            this.tbColorSensitivity.Size = new System.Drawing.Size(246, 45);
            this.tbColorSensitivity.TabIndex = 5;
            this.tbColorSensitivity.TickFrequency = 10;
            this.tbColorSensitivity.Value = 75;
            this.tbColorSensitivity.Scroll += new System.EventHandler(this.tbColorSensitivity_Scroll);
            // 
            // cbColorAnalysis
            // 
            this.cbColorAnalysis.AutoSize = true;
            this.cbColorAnalysis.Location = new System.Drawing.Point(15, 0);
            this.cbColorAnalysis.Name = "cbColorAnalysis";
            this.cbColorAnalysis.Size = new System.Drawing.Size(130, 17);
            this.cbColorAnalysis.TabIndex = 3;
            this.cbColorAnalysis.Text = "Perform Color Analysis";
            this.cbColorAnalysis.UseVisualStyleBackColor = true;
            this.cbColorAnalysis.CheckedChanged += new System.EventHandler(this.cbColorAnalysis_CheckedChanged);
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
            // cbDuplicateFiles
            // 
            this.cbDuplicateFiles.AutoSize = true;
            this.cbDuplicateFiles.Location = new System.Drawing.Point(28, 212);
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
            this.ClientSize = new System.Drawing.Size(301, 299);
            this.Controls.Add(this.cbDuplicateFiles);
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
            ((System.ComponentModel.ISupportInitialize)(this.tbColorSensitivity)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.CheckBox cbColorAnalysis;
        private System.Windows.Forms.CheckBox cbDuplicateFiles;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TrackBar tbColorSensitivity;
        private System.Windows.Forms.TextBox txtColorSensitivity;
        private System.Windows.Forms.Button btnSetDefault;
	}
}
