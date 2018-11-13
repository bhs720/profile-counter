using System;

namespace TIFPDFCounter
{
	public class PageSize
	{
        public PageSize()
        {
            Name = "";
        }

        public PageSize(string name, decimal minWidth, decimal maxWidth, decimal minHeight, decimal maxHeight, bool active = true)
        {
            this.Name = name ?? "";
            this.MinWidth = minWidth;
            this.MinHeight = minHeight;
            this.MaxWidth = maxWidth;
            this.MaxHeight = maxHeight;
            this.Active = active;
        }
        
        public decimal MinWidth { get; set; }
        public decimal MinHeight { get; set; }
        public decimal MaxWidth { get; set; }
        public decimal MaxHeight { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }

		public bool IsMatch(decimal width, decimal height)
		{
            if (width >= MinWidth && width <= MaxWidth && height >= MinHeight && height <= MaxHeight)
            {
                return true;
            }

            if (height >= MinWidth && height <= MaxWidth && width >= MinHeight && width <= MaxHeight)
            {
                return true;
            }

			return false;
		}
		
		public PageSize Clone()
		{
            return new PageSize(Name.Clone() as string, MinWidth, MaxWidth, MinHeight, MaxHeight, Active);
		}
	}
}
