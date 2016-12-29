using System;

namespace TIFPDFCounter
{
	public enum ColorMode { Unknown, BW, Color };
	
	public class TPCFilePage
	{
		readonly float width;
		readonly float height;
		readonly ColorMode colorMode;
		
		public TPCFilePage(float width, float height, ColorMode colorMode)
		{
			this.width = width;
			this.height = height;
			this.colorMode = colorMode;
		}
		
		public float Width { get { return width; } }
		public float Height { get { return height; } }
		public ColorMode ColorMode { get { return colorMode; } }
	}
}
