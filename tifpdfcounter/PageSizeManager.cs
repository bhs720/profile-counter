﻿using System;
using System.Windows.Forms;
using System.Collections.Generic;

namespace TIFPDFCounter
{
	public partial class PageSizeManager : Form
	{
        public List<PageSize> PageSizes { get; private set; }

		public PageSizeManager()
		{
			InitializeComponent();
		}
		
		public PageSizeManager(List<PageSize> pageSizes)
		{
			InitializeComponent();
            LoadPageSizes(pageSizes);
		}

        private void LoadPageSizes(List<PageSize> pageSizes)
        {
            dgvManager.Rows.Clear();
            // Store all page sizes in datagridview
            foreach (var pageSize in pageSizes)
            {
                int rowIndex = dgvManager.Rows.Add(pageSize.Active, pageSize.Name);
                dgvManager.Rows[rowIndex].Tag = pageSize.Clone();
            }
        }

        private void SavePageSizes()
        {
            // Gather up the page sizs and put them into a List<PageSize> to return to the caller.
            PageSizes = new List<PageSize>();
            foreach (DataGridViewRow dgvr in dgvManager.Rows)
            {
                var page = dgvr.Tag as PageSize;
                page.Active = (bool)dgvr.Cells["active"].Value;
                PageSizes.Add(page);
            }
        }
				
		void BtnNewClick(object sender, System.EventArgs e)
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

		void BtnEditClick(object sender, System.EventArgs e)
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

		void BtnDeleteClick(object sender, System.EventArgs e)
		{
			if (dgvManager.SelectedRows.Count == 0)
				return;
			dgvManager.Rows.Remove(dgvManager.SelectedRows[0]);
		}

		void BtnOKClick(object sender, System.EventArgs e)
		{
            SavePageSizes();
			DialogResult = DialogResult.OK;
			Close();
		}

		void BumpRow(DataGridViewRow dgvr, int indexOffset)
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

		void BtnUpClick(object sender, System.EventArgs e)
		{
			BumpRow(dgvManager.SelectedRows[0], -1);
		}

		void BtnDownClick(object sender, System.EventArgs e)
		{
			BumpRow(dgvManager.SelectedRows[0], 1);
		}

        private void btnDefaults_Click(object sender, EventArgs e)
        {
            var dr = MessageBox.Show("This will delete all of your page sizes and load the default standard sizes.", "Are you sure?", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation);
            if (dr == DialogResult.OK)
            {
                LoadPageSizes(Settings.DefaultPageStore);
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
