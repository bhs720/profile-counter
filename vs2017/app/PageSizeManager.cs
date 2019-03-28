using System;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace TIFPDFCounter
{
	public partial class PageSizeManager : Form
	{
        /// <summary>
        /// Return variable for edited page sizes
        /// </summary>
        public List<PageSize> PageSizes { get; private set; }

		public PageSizeManager()
		{
			InitializeComponent();
		}
		
		public PageSizeManager(List<PageSize> pageSizes)
		{
			InitializeComponent();
            PutPageSizesIntoDataGridView(pageSizes);
        }

        private void PutPageSizesIntoDataGridView(List<PageSize> pageSizes)
        {
            dgvManager.Rows.Clear();

            foreach (var pageSize in pageSizes)
            {
                int rowIndex = dgvManager.Rows.Add(pageSize.Active, pageSize.Name);
                dgvManager.Rows[rowIndex].Tag = pageSize;
            }
        }

        private List<PageSize> GetPageSizesFromDataGridView()
        {
            var pageSizes = new List<PageSize>(dgvManager.Rows.Count);
            foreach (DataGridViewRow dgvr in dgvManager.Rows)
            {
                var ps = (PageSize)dgvr.Tag;
                ps.Active = (bool)dgvr.Cells["active"].Value;
                pageSizes.Add(ps);
            }

            return pageSizes;
        }

		private void BtnNewClick(object sender, System.EventArgs e)
		{
			using (var pse = new PageSizeEditor())
			{
				pse.ShowDialog(this);
				if (pse.DialogResult == DialogResult.OK)
				{
					int rowIndex = dgvManager.Rows.Add(true, pse.PageSize.Name);
					dgvManager.Rows[rowIndex].Tag = pse.PageSize;
				}
			}
		}

		private void BtnEditClick(object sender, EventArgs e)
		{
			if (dgvManager.SelectedRows.Count == 0)
				return;
			var editingRow = dgvManager.SelectedRows[0];
            EditPageSize(editingRow);
		}

        private void EditPageSize(DataGridViewRow editingRow)
        {
            var editingPageSize = (editingRow.Tag as PageSize).Clone();
            using (var pse = new PageSizeEditor(editingPageSize))
            {
                pse.ShowDialog(this);
                if (pse.DialogResult == DialogResult.OK)
                {
                    var editedPageSize = pse.PageSize;
                    editingRow.Tag = editedPageSize;
                    editingRow.Cells["Name"].Value = editedPageSize.Name;
                }
            }
        }

		private void BtnDeleteClick(object sender, EventArgs e)
		{
			if (dgvManager.SelectedRows.Count == 0)
				return;
			dgvManager.Rows.Remove(dgvManager.SelectedRows[0]);
		}

		private void BtnOKClick(object sender, EventArgs e)
		{
            PageSizes = GetPageSizesFromDataGridView();
			DialogResult = DialogResult.OK;
			Close();
		}

		private void BumpRow(DataGridViewRow dgvr, int indexOffset)
		{
			var pageSize = dgvr.Tag as PageSize;
			int newIndex = dgvr.Index + indexOffset;
			dgvManager.Rows.Remove(dgvr);
			newIndex = Math.Min(dgvManager.RowCount, newIndex);
			newIndex = Math.Max(0, newIndex);
			
			dgvManager.Rows.Insert(newIndex, pageSize.Active, pageSize.Name);
			dgvManager.Rows[newIndex].Tag = pageSize;
			dgvManager.Rows[newIndex].Selected = true;
		}

        private void BtnUpClick(object sender, EventArgs e)
		{
			BumpRow(dgvManager.SelectedRows[0], -1);
		}

        private void BtnDownClick(object sender, EventArgs e)
		{
			BumpRow(dgvManager.SelectedRows[0], 1);
		}

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            var dr = MessageBox.Show("This will delete all of your page sizes and load the default standard sizes.", "Are you sure?", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
            if (dr == DialogResult.OK)
            {
                PutPageSizesIntoDataGridView(Settings.DefaultPageSizes);
            }   
        }

        private void dgvManager_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                EditPageSize(dgvManager.Rows[e.RowIndex]);
            }
        }
	}
}
