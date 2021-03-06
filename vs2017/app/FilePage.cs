﻿using System;

namespace TIFPDFCounter
{
	public enum ColorMode { Unknown, BW, Color };
	
	public class TPCFilePage
	{
		public TPCFilePage(TPCFile parent, int pageNumber, decimal width, decimal height, ColorMode colorMode)
		{
            ParentFile = parent;
            PageNumber = pageNumber;
            Width = width;
            Height = height;
            ColorMode = colorMode;
		}
		
        public TPCFile ParentFile { get; private set; }
        public int PageNumber { get; private set; }
        public decimal Width { get; private set; }
        public decimal Height { get; private set; }
        public ColorMode ColorMode { get; private set; }
	}
}
