/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 6/10/2014
 * Time: 11:53 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace TIFPDFCounter
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
            this.lblName = new System.Windows.Forms.Label();
            this.lblWidth = new System.Windows.Forms.Label();
            this.lblHeight = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.txtHeightMin = new System.Windows.Forms.TextBox();
            this.txtWidthMin = new System.Windows.Forms.TextBox();
            this.txtWidthMax = new System.Windows.Forms.TextBox();
            this.txtHeightMax = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(79, 8);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(146, 22);
            this.txtName.TabIndex = 1;
            // 
            // lblName
            // 
            this.lblName.Location = new System.Drawing.Point(12, 6);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(48, 23);
            this.lblName.TabIndex = 0;
            this.lblName.Text = "Name:";
            this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblWidth
            // 
            this.lblWidth.Location = new System.Drawing.Point(12, 57);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(61, 23);
            this.lblWidth.TabIndex = 2;
            this.lblWidth.Text = "Width (in)";
            this.lblWidth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHeight
            // 
            this.lblHeight.Location = new System.Drawing.Point(12, 83);
            this.lblHeight.Name = "lblHeight";
            this.lblHeight.Size = new System.Drawing.Size(61, 23);
            this.lblHeight.TabIndex = 7;
            this.lblHeight.Text = "Height (in)";
            this.lblHeight.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(150, 121);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 11;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(69, 121);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 10;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOKClick);
            // 
            // txtHeightMin
            // 
            this.txtHeightMin.Location = new System.Drawing.Point(79, 85);
            this.txtHeightMin.Name = "txtHeightMin";
            this.txtHeightMin.Size = new System.Drawing.Size(70, 22);
            this.txtHeightMin.TabIndex = 8;
            // 
            // txtWidthMin
            // 
            this.txtWidthMin.Location = new System.Drawing.Point(79, 59);
            this.txtWidthMin.Name = "txtWidthMin";
            this.txtWidthMin.Size = new System.Drawing.Size(70, 22);
            this.txtWidthMin.TabIndex = 4;
            // 
            // txtWidthMax
            // 
            this.txtWidthMax.Location = new System.Drawing.Point(155, 59);
            this.txtWidthMax.Name = "txtWidthMax";
            this.txtWidthMax.Size = new System.Drawing.Size(70, 22);
            this.txtWidthMax.TabIndex = 6;
            // 
            // txtHeightMax
            // 
            this.txtHeightMax.Location = new System.Drawing.Point(155, 85);
            this.txtHeightMax.Name = "txtHeightMax";
            this.txtHeightMax.Size = new System.Drawing.Size(70, 22);
            this.txtHeightMax.TabIndex = 9;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(79, 43);
            this.label1.Name = "label1";
            this.label1.Padding = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.label1.Size = new System.Drawing.Size(33, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Min.";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(152, 43);
            this.label3.Name = "label3";
            this.label3.Padding = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.label3.Size = new System.Drawing.Size(34, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Max.";
            // 
            // PageSizeEditor
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(243, 158);
            this.ControlBox = false;
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtWidthMax);
            this.Controls.Add(this.txtHeightMax);
            this.Controls.Add(this.txtWidthMin);
            this.Controls.Add(this.txtHeightMin);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblHeight);
            this.Controls.Add(this.lblWidth);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.txtName);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
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
		private System.Windows.Forms.TextBox txtHeightMin;
		private System.Windows.Forms.TextBox txtWidthMin;
		private System.Windows.Forms.TextBox txtWidthMax;
		private System.Windows.Forms.TextBox txtHeightMax;
        private System.Windows.Forms.TextBox txtName;
		private System.Windows.Forms.Label lblName;
		private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.Label lblHeight;
		private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
	}
}
