using System;
using System.Drawing;
using System.Windows.Forms;

namespace ProFileCounter
{
	public partial class PageSizeEditor : Form
	{
		public PageSizeEditor()
		{
			InitializeComponent();
		}
		
		public PageSizeEditor(PageSize pageSize)
		{
			InitializeComponent();
			PageSize = pageSize;
			
		}
		
		public PageSize PageSize { get; private set; }
		
		void TxtWidthTextChanged(object sender, System.EventArgs e)
		{
			decimal output;
			decimal variance;
			if (decimal.TryParse(txtWidth.Text, out output) &&
				decimal.TryParse(txtVariance.Text, out variance))
			{
				decimal min = output - output * variance;
				decimal max = output + output * variance;
				txtWidthMin.Text = min.ToString();
				txtWidthMax.Text = max.ToString();
			}
		}
		void TxtHeightTextChanged(object sender, System.EventArgs e)
		{
			decimal output;
			decimal variance;
			if (decimal.TryParse(txtHeight.Text, out output) &&
				decimal.TryParse(txtVariance.Text, out variance))
			{
				decimal min = output - output * variance;
				decimal max = output + output * variance;
				txtHeightMin.Text = min.ToString();
				txtHeightMax.Text = max.ToString();
			}
		}
		void BtnOKClick(object sender, System.EventArgs e)
		{
			float width, height, widthMin, widthMax, heightMin, heightMax, variance;
			if (string.IsNullOrEmpty(txtName.Text) ||
				!float.TryParse(txtWidth.Text, out width) ||
				!float.TryParse(txtHeight.Text, out height) ||
				!float.TryParse(txtWidthMin.Text, out widthMin) ||
				!float.TryParse(txtWidthMax.Text, out widthMax) ||
				!float.TryParse(txtHeightMin.Text, out heightMin) ||
				!float.TryParse(txtHeightMax.Text, out heightMax) ||
				!float.TryParse(txtVariance.Text, out variance))
				return;
			PageSize.Name = txtName.Text;
			PageSize.Width.Nominal = Math.Abs(width);
			PageSize.Width.Min = Math.Abs(widthMin);
			PageSize.Width.Max = Math.Abs(widthMax);
			PageSize.Height.Nominal = Math.Abs(height);
			PageSize.Height.Min = Math.Abs(heightMin);
			PageSize.Height.Max = Math.Abs(heightMax);
			PageSize.Variance = variance;
			PageSize.Active = true;
			DialogResult = DialogResult.OK;
			Close();
		}
		void PageSizeEditorLoad(object sender, System.EventArgs e)
		{
			if (PageSize != null)
			{
				txtName.Text = PageSize.Name;
				txtWidth.Text = PageSize.Width.Nominal.ToString();
				txtHeight.Text = PageSize.Height.Nominal.ToString();
				txtWidthMin.Text = PageSize.Width.Min.ToString();
				txtWidthMax.Text = PageSize.Width.Max.ToString();
				txtHeightMin.Text = PageSize.Height.Min.ToString();
				txtHeightMax.Text = PageSize.Height.Max.ToString();
				txtVariance.Text = PageSize.Variance.ToString();
			}
			else
			{
				PageSize = new PageSize();
				PageSize.Variance = 0.04f;
				txtVariance.Text = PageSize.Variance.ToString();
			}
			txtWidth.TextChanged += TxtWidthTextChanged;
			txtHeight.TextChanged += TxtHeightTextChanged;
			txtVariance.TextChanged += TxtVarianceTextChanged;
		}
		void TxtVarianceTextChanged(object sender, System.EventArgs e)
		{
			TxtWidthTextChanged(null, null);
			TxtHeightTextChanged(null, null);
		}
	}
}
