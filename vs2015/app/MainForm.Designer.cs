/*
 * Created by SharpDevelop.
 * User: bsmith
 * Date: 5/19/2014
 * Time: 3:46 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
namespace TIFPDFCounter
{
	partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.dgvFilePages = new System.Windows.Forms.DataGridView();
            this.dgvCounters = new System.Windows.Forms.DataGridView();
            this.colPageSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colColorCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPageCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.ctxPages = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.highlightPagesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnManage = new System.Windows.Forms.Button();
            this.btnClearList = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblTotalFiles = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblTotalPages = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSelected = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblUpdate = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnSettings = new System.Windows.Forms.Button();
            this.dgvPagesFilename = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgvPagesPageNumber = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgvPagesPageColor = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgvPagesPageSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFilePages)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCounters)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.ctxPages.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvFilePages
            // 
            this.dgvFilePages.AllowUserToAddRows = false;
            this.dgvFilePages.AllowUserToDeleteRows = false;
            this.dgvFilePages.AllowUserToResizeRows = false;
            this.dgvFilePages.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFilePages.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvFilePages.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFilePages.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dgvPagesFilename,
            this.dgvPagesPageNumber,
            this.dgvPagesPageColor,
            this.dgvPagesPageSize});
            this.dgvFilePages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFilePages.Location = new System.Drawing.Point(0, 0);
            this.dgvFilePages.Name = "dgvFilePages";
            this.dgvFilePages.ReadOnly = true;
            this.dgvFilePages.RowHeadersVisible = false;
            this.dgvFilePages.RowTemplate.Height = 16;
            this.dgvFilePages.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFilePages.ShowCellErrors = false;
            this.dgvFilePages.ShowEditingIcon = false;
            this.dgvFilePages.ShowRowErrors = false;
            this.dgvFilePages.Size = new System.Drawing.Size(336, 205);
            this.dgvFilePages.TabIndex = 0;
            this.dgvFilePages.SelectionChanged += new System.EventHandler(this.dgvFilePages_SelectionChanged);
            // 
            // dgvCounters
            // 
            this.dgvCounters.AllowUserToAddRows = false;
            this.dgvCounters.AllowUserToDeleteRows = false;
            this.dgvCounters.AllowUserToResizeRows = false;
            this.dgvCounters.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvCounters.CausesValidation = false;
            this.dgvCounters.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.dgvCounters.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCounters.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPageSize,
            this.colColorCount,
            this.colPageCount});
            this.dgvCounters.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvCounters.Location = new System.Drawing.Point(0, 0);
            this.dgvCounters.Name = "dgvCounters";
            this.dgvCounters.ReadOnly = true;
            this.dgvCounters.RowHeadersVisible = false;
            this.dgvCounters.RowTemplate.Height = 16;
            this.dgvCounters.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvCounters.ShowCellErrors = false;
            this.dgvCounters.ShowEditingIcon = false;
            this.dgvCounters.ShowRowErrors = false;
            this.dgvCounters.Size = new System.Drawing.Size(218, 205);
            this.dgvCounters.TabIndex = 11;
            this.dgvCounters.MouseDown += new System.Windows.Forms.MouseEventHandler(this.DgvCountersMouseDown);
            // 
            // colPageSize
            // 
            this.colPageSize.FillWeight = 70F;
            this.colPageSize.HeaderText = "Page Size";
            this.colPageSize.Name = "colPageSize";
            this.colPageSize.ReadOnly = true;
            this.colPageSize.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // colColorCount
            // 
            this.colColorCount.FillWeight = 30F;
            this.colColorCount.HeaderText = "Color";
            this.colColorCount.Name = "colColorCount";
            this.colColorCount.ReadOnly = true;
            this.colColorCount.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // colPageCount
            // 
            this.colPageCount.FillWeight = 30F;
            this.colPageCount.HeaderText = "Count";
            this.colPageCount.Name = "colPageCount";
            this.colPageCount.ReadOnly = true;
            this.colPageCount.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(8, 8);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(7);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dgvFilePages);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.dgvCounters);
            this.splitContainer1.Size = new System.Drawing.Size(558, 205);
            this.splitContainer1.SplitterDistance = 336;
            this.splitContainer1.TabIndex = 12;
            // 
            // ctxPages
            // 
            this.ctxPages.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.highlightPagesToolStripMenuItem});
            this.ctxPages.Name = "ctxPages";
            this.ctxPages.ShowImageMargin = false;
            this.ctxPages.Size = new System.Drawing.Size(115, 26);
            // 
            // highlightPagesToolStripMenuItem
            // 
            this.highlightPagesToolStripMenuItem.Name = "highlightPagesToolStripMenuItem";
            this.highlightPagesToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.highlightPagesToolStripMenuItem.Text = "Select Pages";
            this.highlightPagesToolStripMenuItem.Click += new System.EventHandler(this.HighlightPagesToolStripMenuItemClick);
            // 
            // btnManage
            // 
            this.btnManage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnManage.Location = new System.Drawing.Point(425, 219);
            this.btnManage.Name = "btnManage";
            this.btnManage.Size = new System.Drawing.Size(141, 23);
            this.btnManage.TabIndex = 13;
            this.btnManage.Text = "Manage Page Sizes";
            this.btnManage.UseVisualStyleBackColor = true;
            this.btnManage.Click += new System.EventHandler(this.BtnManageClick);
            // 
            // btnClearList
            // 
            this.btnClearList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClearList.Location = new System.Drawing.Point(8, 219);
            this.btnClearList.Name = "btnClearList";
            this.btnClearList.Size = new System.Drawing.Size(75, 23);
            this.btnClearList.TabIndex = 15;
            this.btnClearList.Text = "Clear List";
            this.btnClearList.UseVisualStyleBackColor = true;
            this.btnClearList.Click += new System.EventHandler(this.BtnClearListClick);
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.Timer1Tick);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblTotalFiles,
            this.lblTotalPages,
            this.lblSelected,
            this.lblUpdate});
            this.statusStrip1.Location = new System.Drawing.Point(1, 247);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(572, 24);
            this.statusStrip1.TabIndex = 21;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblTotalFiles
            // 
            this.lblTotalFiles.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.lblTotalFiles.Name = "lblTotalFiles";
            this.lblTotalFiles.Size = new System.Drawing.Size(76, 19);
            this.lblTotalFiles.Text = "Total Files: 0";
            // 
            // lblTotalPages
            // 
            this.lblTotalPages.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.lblTotalPages.Name = "lblTotalPages";
            this.lblTotalPages.Size = new System.Drawing.Size(84, 19);
            this.lblTotalPages.Text = "Total Pages: 0";
            // 
            // lblSelected
            // 
            this.lblSelected.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.lblSelected.Name = "lblSelected";
            this.lblSelected.Size = new System.Drawing.Size(67, 19);
            this.lblSelected.Text = "Selected: 0";
            // 
            // lblUpdate
            // 
            this.lblUpdate.Name = "lblUpdate";
            this.lblUpdate.Size = new System.Drawing.Size(330, 19);
            this.lblUpdate.Spring = true;
            this.lblUpdate.Text = "Update Link";
            this.lblUpdate.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.Location = new System.Drawing.Point(344, 219);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(75, 23);
            this.btnSettings.TabIndex = 22;
            this.btnSettings.Text = "Settings";
            this.btnSettings.UseVisualStyleBackColor = true;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // dgvPagesFilename
            // 
            this.dgvPagesFilename.HeaderText = "Filename";
            this.dgvPagesFilename.Name = "dgvPagesFilename";
            this.dgvPagesFilename.ReadOnly = true;
            this.dgvPagesFilename.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dgvPagesPageNumber
            // 
            this.dgvPagesPageNumber.FillWeight = 25F;
            this.dgvPagesPageNumber.HeaderText = "Page";
            this.dgvPagesPageNumber.Name = "dgvPagesPageNumber";
            this.dgvPagesPageNumber.ReadOnly = true;
            this.dgvPagesPageNumber.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dgvPagesPageColor
            // 
            this.dgvPagesPageColor.FillWeight = 25F;
            this.dgvPagesPageColor.HeaderText = "Color";
            this.dgvPagesPageColor.Name = "dgvPagesPageColor";
            this.dgvPagesPageColor.ReadOnly = true;
            this.dgvPagesPageColor.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dgvPagesPageSize
            // 
            this.dgvPagesPageSize.FillWeight = 50F;
            this.dgvPagesPageSize.HeaderText = "Size";
            this.dgvPagesPageSize.Name = "dgvPagesPageSize";
            this.dgvPagesPageSize.ReadOnly = true;
            this.dgvPagesPageSize.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(574, 272);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.btnClearList);
            this.Controls.Add(this.btnManage);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(590, 240);
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(1);
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "ProFile Counter";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormFormClosing);
            this.Load += new System.EventHandler(this.MainFormLoad);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFilePages)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCounters)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ctxPages.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
		private System.Windows.Forms.DataGridViewTextBoxColumn colPageSize;
		private System.Windows.Forms.DataGridViewTextBoxColumn colPageCount;
		private System.Windows.Forms.DataGridView dgvCounters;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.DataGridView dgvFilePages;
		private System.Windows.Forms.ContextMenuStrip ctxPages;
		private System.Windows.Forms.ToolStripMenuItem highlightPagesToolStripMenuItem;
		private System.Windows.Forms.Button btnManage;
        private System.Windows.Forms.Button btnClearList;
		private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colColorCount;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblTotalFiles;
        private System.Windows.Forms.ToolStripStatusLabel lblTotalPages;
        private System.Windows.Forms.ToolStripStatusLabel lblSelected;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.ToolStripStatusLabel lblUpdate;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgvPagesFilename;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgvPagesPageNumber;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgvPagesPageColor;
        private System.Windows.Forms.DataGridViewTextBoxColumn dgvPagesPageSize;
    }
}
