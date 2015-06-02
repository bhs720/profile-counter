using System;

namespace ProFileCounter
{
	public class Dimension
	{
        public Dimension() { }

        public Dimension(float nominal, float min, float max)
        {
            this.Nominal = nominal;
            this.Min = min;
            this.Max = max;
        }

        public float Nominal { get; set; }
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
			return new Dimension(this.Nominal, this.Min, this.Max);
		}
	}
}
