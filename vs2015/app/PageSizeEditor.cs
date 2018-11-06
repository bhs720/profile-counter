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
                txtWidthMin.Text = PageSize.Width.Min.ToString();
                txtWidthMax.Text = PageSize.Width.Max.ToString();
                txtHeightMin.Text = PageSize.Height.Min.ToString();
                txtHeightMax.Text = PageSize.Height.Max.ToString();
            }
            else
            {
                PageSize = new PageSize();
            }
        }
		
		void BtnOKClick(object sender, System.EventArgs e)
		{
			float widthMin, widthMax, heightMin, heightMax;
            try
            {
                widthMin = float.Parse(txtWidthMin.Text);
                widthMax = float.Parse(txtWidthMax.Text);
                heightMin = float.Parse(txtHeightMin.Text);
                heightMax = float.Parse(txtHeightMax.Text);
                if (widthMin < 0 || widthMax < 0 || heightMin < 0 || heightMax < 0)
                {
                    throw new Exception("Page dimensions must be a positive number.");
                }
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    throw new Exception("Name cannot be blank.");
                }
                PageSize.Name = txtName.Text;
                PageSize.Width.Min = widthMin;
                PageSize.Width.Max = widthMax;
                PageSize.Height.Min = heightMin;
                PageSize.Height.Max = heightMax;
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
