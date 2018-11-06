using System;

namespace TIFPDFCounter
{
	public class Dimension
	{
        public Dimension() { }

        public Dimension(float min, float max)
        {
            this.Min = min;
            this.Max = max;
        }

        public float Min { get; set; }
        public float Max { get; set; }

		public bool Compare(float value)
		{
			if (value >= this.Min && value <= this.Max)
				return true;
			return false;
		}

		public Dimension Clone()
		{
			return new Dimension(this.Min, this.Max);
		}
	}
}
