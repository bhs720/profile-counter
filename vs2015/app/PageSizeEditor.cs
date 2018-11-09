using System;
using System.Drawing;
using System.Windows.Forms;

namespace TIFPDFCounter
{
	public partial class PageSizeEditor : Form
	{
        public PageSize PageSize { get; private set; }

		public PageSizeEditor()
		{
			InitializeComponent();
		}
		
		public PageSizeEditor(PageSize pageSize)
		{
			InitializeComponent();
			PageSize = pageSize;
		}

        void PageSizeEditorLoad(object sender, System.EventArgs e)
        {
            if (PageSize != null)
            {
                txtName.Text = PageSize.Name;
                txtWidthMin.Text = PageSize.MinWidth.ToString();
                txtWidthMax.Text = PageSize.MaxWidth.ToString();
                txtHeightMin.Text = PageSize.MinHeight.ToString();
                txtHeightMax.Text = PageSize.MaxHeight.ToString();
            }
            else
            {
                PageSize = new PageSize("", 0, 0, 0, 0, true);
            }
        }
		
		void BtnOKClick(object sender, System.EventArgs e)
		{
			decimal widthMin, widthMax, heightMin, heightMax;
            try
            {
                widthMin = decimal.Parse(txtWidthMin.Text);
                widthMax = decimal.Parse(txtWidthMax.Text);
                heightMin = decimal.Parse(txtHeightMin.Text);
                heightMax = decimal.Parse(txtHeightMax.Text);
                if (widthMin < 0 || widthMax < 0 || heightMin < 0 || heightMax < 0)
                {
                    throw new Exception("Page dimensions must be a positive number.");
                }
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    throw new Exception("Name cannot be blank.");
                }
                PageSize.Name = txtName.Text;
                PageSize.MinWidth = widthMin;
                PageSize.MaxWidth = widthMax;
                PageSize.MinHeight = heightMin;
                PageSize.MaxHeight = heightMax;
                PageSize.Active = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
		}
	}
}
