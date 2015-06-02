/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 6/10/2014
 * Time: 11:53 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace ProFileCounter
{
	partial class PageSizeEditor
	{
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		
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
			this.txtName = new System.Windows.Forms.TextBox();
			this.txtWidth = new System.Windows.Forms.TextBox();
			this.txtHeight = new System.Windows.Forms.TextBox();
			this.lblName = new System.Windows.Forms.Label();
			this.lblWidth = new System.Windows.Forms.Label();
			this.lblHeight = new System.Windows.Forms.Label();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnOK = new System.Windows.Forms.Button();
			this.txtHeightMin = new System.Windows.Forms.TextBox();
			this.txtWidthMin = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.txtWidthMax = new System.Windows.Forms.TextBox();
			this.txtHeightMax = new System.Windows.Forms.TextBox();
			this.txtVariance = new System.Windows.Forms.TextBox();
			this.lblVariance = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// txtName
			// 
			this.txtName.Location = new System.Drawing.Point(79, 8);
			this.txtName.Name = "txtName";
			this.txtName.Size = new System.Drawing.Size(292, 20);
			this.txtName.TabIndex = 0;
			// 
			// txtWidth
			// 
			this.txtWidth.Location = new System.Drawing.Point(79, 34);
			this.txtWidth.Name = "txtWidth";
			this.txtWidth.Size = new System.Drawing.Size(70, 20);
			this.txtWidth.TabIndex = 1;
			// 
			// txtHeight
			// 
			this.txtHeight.Location = new System.Drawing.Point(79, 60);
			this.txtHeight.Name = "txtHeight";
			this.txtHeight.Size = new System.Drawing.Size(70, 20);
			this.txtHeight.TabIndex = 2;
			// 
			// lblName
			// 
			this.lblName.Location = new System.Drawing.Point(12, 6);
			this.lblName.Name = "lblName";
			this.lblName.Size = new System.Drawing.Size(48, 23);
			this.lblName.TabIndex = 3;
			this.lblName.Text = "Name:";
			this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// lblWidth
			// 
			this.lblWidth.Location = new System.Drawing.Point(12, 32);
			this.lblWidth.Name = "lblWidth";
			this.lblWidth.Size = new System.Drawing.Size(61, 23);
			this.lblWidth.TabIndex = 4;
			this.lblWidth.Text = "Width (in)";
			this.lblWidth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// lblHeight
			// 
			this.lblHeight.Location = new System.Drawing.Point(12, 58);
			this.lblHeight.Name = "lblHeight";
			this.lblHeight.Size = new System.Drawing.Size(61, 23);
			this.lblHeight.TabIndex = 5;
			this.lblHeight.Text = "Height (in)";
			this.lblHeight.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// btnCancel
			// 
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(296, 97);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 9;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// btnOK
			// 
			this.btnOK.Location = new System.Drawing.Point(215, 97);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(75, 23);
			this.btnOK.TabIndex = 8;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.BtnOKClick);
			// 
			// txtHeightMin
			// 
			this.txtHeightMin.Location = new System.Drawing.Point(190, 60);
			this.txtHeightMin.Name = "txtHeightMin";
			this.txtHeightMin.Size = new System.Drawing.Size(70, 20);
			this.txtHeightMin.TabIndex = 5;
			// 
			// txtWidthMin
			// 
			this.txtWidthMin.Location = new System.Drawing.Point(190, 34);
			this.txtWidthMin.Name = "txtWidthMin";
			this.txtWidthMin.Size = new System.Drawing.Size(70, 20);
			this.txtWidthMin.TabIndex = 4;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(155, 32);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(29, 23);
			this.label1.TabIndex = 18;
			this.label1.Text = "Min:";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(155, 58);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(29, 23);
			this.label2.TabIndex = 19;
			this.label2.Text = "Min:";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(266, 58);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(33, 23);
			this.label3.TabIndex = 23;
			this.label3.Text = "Max:";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(266, 32);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(33, 23);
			this.label4.TabIndex = 22;
			this.label4.Text = "Max:";
			this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// txtWidthMax
			// 
			this.txtWidthMax.Location = new System.Drawing.Point(301, 34);
			this.txtWidthMax.Name = "txtWidthMax";
			this.txtWidthMax.Size = new System.Drawing.Size(70, 20);
			this.txtWidthMax.TabIndex = 6;
			// 
			// txtHeightMax
			// 
			this.txtHeightMax.Location = new System.Drawing.Point(301, 60);
			this.txtHeightMax.Name = "txtHeightMax";
			this.txtHeightMax.Size = new System.Drawing.Size(70, 20);
			this.txtHeightMax.TabIndex = 7;
			// 
			// txtVariance
			// 
			this.txtVariance.Location = new System.Drawing.Point(79, 97);
			this.txtVariance.Name = "txtVariance";
			this.txtVariance.Size = new System.Drawing.Size(38, 20);
			this.txtVariance.TabIndex = 3;
			// 
			// lblVariance
			// 
			this.lblVariance.Location = new System.Drawing.Point(12, 95);
			this.lblVariance.Name = "lblVariance";
			this.lblVariance.Size = new System.Drawing.Size(61, 23);
			this.lblVariance.TabIndex = 25;
			this.lblVariance.Text = "Variance";
			this.lblVariance.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// PageSizeEditor
			// 
			this.AcceptButton = this.btnOK;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(392, 135);
			this.ControlBox = false;
			this.Controls.Add(this.lblVariance);
			this.Controls.Add(this.txtVariance);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.txtWidthMax);
			this.Controls.Add(this.txtHeightMax);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.txtWidthMin);
			this.Controls.Add(this.txtHeightMin);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.lblHeight);
			this.Controls.Add(this.lblWidth);
			this.Controls.Add(this.lblName);
			this.Controls.Add(this.txtHeight);
			this.Controls.Add(this.txtWidth);
			this.Controls.Add(this.txtName);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "PageSizeEditor";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Edit Page Size";
			this.Load += new System.EventHandler(this.PageSizeEditorLoad);
			this.ResumeLayout(false);
			this.PerformLayout();

		}
		private System.Windows.Forms.TextBox txtVariance;
		private System.Windows.Forms.Label lblVariance;
		private System.Windows.Forms.TextBox txtHeightMin;
		private System.Windows.Forms.TextBox txtWidthMin;
		private System.Windows.Forms.TextBox txtWidthMax;
		private System.Windows.Forms.TextBox txtHeightMax;
		private System.Windows.Forms.TextBox txtName;
		private System.Windows.Forms.TextBox txtWidth;
		private System.Windows.Forms.TextBox txtHeight;
		private System.Windows.Forms.Label lblName;
		private System.Windows.Forms.Label lblWidth;
		private System.Windows.Forms.Label lblHeight;
		
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnOK;
	}
}
