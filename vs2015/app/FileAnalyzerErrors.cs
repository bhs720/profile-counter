using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public partial class FileAnalyzerErrors : Form
    {

        public FileAnalyzerErrors()
        {
            InitializeComponent();
            InitializeErrorTable();
        }

        private void InitializeErrorTable()
        {
            dataGridView1.Columns.Add("Directory", "Directory");
            dataGridView1.Columns.Add("Filename", "Filename");
            dataGridView1.Columns.Add("Extension", "Extension");
            dataGridView1.Columns.Add("File Size", "File Size");
            dataGridView1.Columns.Add("Info", "Info");
        }

        public void PushError(string filename, string error)
        {
            string dir, name, ext, sizeFmt;
            try
            {
                dir = System.IO.Path.GetDirectoryName(filename);
            }
            catch (Exception ex)
            {
                dir = ex.Message;
            }

            try
            {
                name = System.IO.Path.GetFileName(filename);
            }
            catch (Exception ex)
            {
                name = ex.Message;
            }

            try
            {
                ext = System.IO.Path.GetExtension(filename);
            }
            catch (Exception ex)
            {
                ext = ex.Message;
            }

            try
            {
                decimal size = new System.IO.FileInfo(filename).Length;
                string[] units = new string[] {"Bytes", "KB", "MB", "GB", "TB"};
                int i = 0;
                while (size > 1024 && i < units.Length - 1)
                {
                    size /= 1024;
                    i++;
                }
                sizeFmt = string.Format("{0} {1}", Math.Round(size, 2), units[i]);
            }
            catch (Exception ex)
            {
                sizeFmt = ex.Message;
            }


            int rowIndex = dataGridView1.Rows.Add(dir, name, ext, sizeFmt, error);
            dataGridView1.Rows[rowIndex].Tag = filename;
        }

        public bool HasErrors
        {
            get
            {
                return dataGridView1.Rows.Count > 0;
            }
        }

        private void FileAnalyzerErrors_Load(object sender, EventArgs e)
        {
            
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                try
                {
                    string filename = dataGridView1.Rows[e.RowIndex].Tag as string;
                    System.Diagnostics.Process.Start(filename);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
