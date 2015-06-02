/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 6/10/2014
 * Time: 5:30 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace ProFileCounter
{
	partial class PageSizeManager
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
			this.dgvManager = new System.Windows.Forms.DataGridView();
			this.active = new System.Windows.Forms.DataGridViewCheckBoxColumn();
			this.name = new System.Windows.Forms.DataGridViewTextBoxColumn();
			this.btnCancel = new System.Windows.Forms.Button();
			this.btnOK = new System.Windows.Forms.Button();
			this.btnAdd = new System.Windows.Forms.Button();
			this.btnDelete = new System.Windows.Forms.Button();
			this.btnEdit = new System.Windows.Forms.Button();
			this.btnUp = new System.Windows.Forms.Button();
			this.btnDown = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.dgvManager)).BeginInit();
			this.SuspendLayout();
			// 
			// dgvManager
			// 
			this.dgvManager.AllowUserToAddRows = false;
			this.dgvManager.AllowUserToDeleteRows = false;
			this.dgvManager.AllowUserToResizeColumns = false;
			this.dgvManager.AllowUserToResizeRows = false;
			this.dgvManager.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
			| System.Windows.Forms.AnchorStyles.Left) 
			| System.Windows.Forms.AnchorStyles.Right)));
			this.dgvManager.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
			this.dgvManager.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
			this.dgvManager.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dgvManager.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
			this.active,
			this.name});
			this.dgvManager.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
			this.dgvManager.Location = new System.Drawing.Point(8, 8);
			this.dgvManager.Margin = new System.Windows.Forms.Padding(7);
			this.dgvManager.MultiSelect = false;
			this.dgvManager.Name = "dgvManager";
			this.dgvManager.RowHeadersVisible = false;
			this.dgvManager.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
			this.dgvManager.Size = new System.Drawing.Size(481, 244);
			this.dgvManager.TabIndex = 0;
			// 
			// active
			// 
			this.active.FillWeight = 15F;
			this.active.HeaderText = "Active?";
			this.active.MinimumWidth = 24;
			this.active.Name = "active";
			this.active.Resizable = System.Windows.Forms.DataGridViewTriState.False;
			this.active.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
			this.active.ToolTipText = "Should the page size be used when counting pages?";
			// 
			// name
			// 
			this.name.FillWeight = 85F;
			this.name.HeaderText = "Name";
			this.name.MinimumWidth = 24;
			this.name.Name = "name";
			this.name.ReadOnly = true;
			this.name.Resizable = System.Windows.Forms.DataGridViewTriState.False;
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.btnCancel.Location = new System.Drawing.Point(414, 262);
			this.btnCancel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 7);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 1;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			// 
			// btnOK
			// 
			this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnOK.Location = new System.Drawing.Point(333, 262);
			this.btnOK.Margin = new System.Windows.Forms.Padding(3, 3, 3, 7);
			this.btnOK.Name = "btnOK";
			this.btnOK.Size = new System.Drawing.Size(75, 23);
			this.btnOK.TabIndex = 2;
			this.btnOK.Text = "OK";
			this.btnOK.UseVisualStyleBackColor = true;
			this.btnOK.Click += new System.EventHandler(this.BtnOKClick);
			// 
			// btnAdd
			// 
			this.btnAdd.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnAdd.Location = new System.Drawing.Point(8, 262);
			this.btnAdd.Margin = new System.Windows.Forms.Padding(3, 3, 3, 7);
			this.btnAdd.Name = "btnAdd";
			this.btnAdd.Size = new System.Drawing.Size(75, 23);
			this.btnAdd.TabIndex = 3;
			this.btnAdd.Text = "New...";
			this.btnAdd.UseVisualStyleBackColor = true;
			this.btnAdd.Click += new System.EventHandler(this.BtnNewClick);
			// 
			// btnDelete
			// 
			this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnDelete.Location = new System.Drawing.Point(170, 262);
			this.btnDelete.Margin = new System.Windows.Forms.Padding(3, 3, 3, 7);
			this.btnDelete.Name = "btnDelete";
			this.btnDelete.Size = new System.Drawing.Size(75, 23);
			this.btnDelete.TabIndex = 5;
			this.btnDelete.Text = "Delete";
			this.btnDelete.UseVisualStyleBackColor = true;
			this.btnDelete.Click += new System.EventHandler(this.BtnDeleteClick);
			// 
			// btnEdit
			// 
			this.btnEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
			this.btnEdit.Location = new System.Drawing.Point(89, 262);
			this.btnEdit.Margin = new System.Windows.Forms.Padding(3, 3, 3, 7);
			this.btnEdit.Name = "btnEdit";
			this.btnEdit.Size = new System.Drawing.Size(75, 23);
			this.btnEdit.TabIndex = 4;
			this.btnEdit.Text = "Edit...";
			this.btnEdit.UseVisualStyleBackColor = true;
			this.btnEdit.Click += new System.EventHandler(this.BtnEditClick);
			// 
			// btnUp
			// 
			this.btnUp.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.btnUp.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnUp.Location = new System.Drawing.Point(495, 96);
			this.btnUp.Name = "btnUp";
			this.btnUp.Size = new System.Drawing.Size(23, 23);
			this.btnUp.TabIndex = 6;
			this.btnUp.Text = "▲";
			this.btnUp.UseVisualStyleBackColor = true;
			this.btnUp.Click += new System.EventHandler(this.BtnUpClick);
			// 
			// btnDown
			// 
			this.btnDown.Anchor = System.Windows.Forms.AnchorStyles.Right;
			this.btnDown.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnDown.Location = new System.Drawing.Point(495, 125);
			this.btnDown.Name = "btnDown";
			this.btnDown.Size = new System.Drawing.Size(23, 23);
			this.btnDown.TabIndex = 7;
			this.btnDown.Text = "▼";
			this.btnDown.UseVisualStyleBackColor = true;
			this.btnDown.Click += new System.EventHandler(this.BtnDownClick);
			// 
			// PageSizeManager
			// 
			this.AcceptButton = this.btnOK;
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(524, 293);
			this.ControlBox = false;
			this.Controls.Add(this.btnDown);
			this.Controls.Add(this.btnUp);
			this.Controls.Add(this.btnEdit);
			this.Controls.Add(this.btnDelete);
			this.Controls.Add(this.btnAdd);
			this.Controls.Add(this.btnOK);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.dgvManager);
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(480, 200);
			this.Name = "PageSizeManager";
			this.Padding = new System.Windows.Forms.Padding(1);
			this.ShowInTaskbar = false;
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Manage Page Sizes";
			((System.ComponentModel.ISupportInitialize)(this.dgvManager)).EndInit();
			this.ResumeLayout(false);

		}
		private System.Windows.Forms.Button btnUp;
		private System.Windows.Forms.Button btnDown;
		private System.Windows.Forms.DataGridView dgvManager;
		private System.Windows.Forms.DataGridViewCheckBoxColumn active;
		private System.Windows.Forms.DataGridViewTextBoxColumn name;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.Button btnOK;
		private System.Windows.Forms.Button btnAdd;
		private System.Windows.Forms.Button btnDelete;
		private System.Windows.Forms.Button btnEdit;
	}
}
