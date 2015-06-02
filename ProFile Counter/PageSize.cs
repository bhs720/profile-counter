using System;

namespace ProFileCounter
{
	public class PageSize
	{
        public PageSize()
        {
            Width = new Dimension();
            Height = new Dimension();
        }

        public PageSize(string name, float width, float height, float variance, bool active = true)
        {
            this.Name = name;
            this.Width = new Dimension(width, width - width * variance, width + width * variance);
            this.Height = new Dimension(height, height - height * variance, height + height * variance);
            this.Variance = variance;
            this.Active = active;
        }

        public PageSize(string name, Dimension width, Dimension height, float variance, bool active = true)
        {
            this.Name = name;
            this.Width = width;
            this.Height = height;
            this.Variance = variance;
            this.Active = active;
        }

        public Dimension Width { get; set; }
        public Dimension Height { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public float Variance { get; set; }

		public bool IsMatch(float width, float height)
		{
			if (Width.Compare(width) && Height.Compare(height))
				return true;
			
			// Swap orientation and compare again
			if (Width.Compare(height) && Height.Compare(width))
				return true;
			
			return false;
		}
		
		public PageSize Clone()
		{
            return new PageSize(this.Name, this.Width.Clone(), this.Height.Clone(), this.Variance, this.Active);
		}
	}
}
