using System;
using System.Collections.Generic;

namespace TIFPDFCounter
{
	public class PageSizeCounter
	{
        public List<TPCFilePage> ColorPages { get; private set; }
        public List<TPCFilePage> BlackPages { get; private set; }
        public List<TPCFilePage> UnknownPages { get; private set; }
        public string Name { get; private set; }
        public PageSize PageSize { get; private set; }

        public PageSizeCounter(PageSize pageSize)
		{
            PageSize = pageSize;
            Name = pageSize?.Name ?? "Unknown";
            ColorPages = new List<TPCFilePage>();
            BlackPages = new List<TPCFilePage>();
            UnknownPages = new List<TPCFilePage>();
		}

        public bool IsMatch(TPCFilePage tpcFilePage)
        {
            // null page size means this is the "unknown" page size counter
            if (PageSize == null || PageSize.IsMatch(tpcFilePage.Width, tpcFilePage.Height))
            {
                switch (tpcFilePage.ColorMode)
                {
                    case ColorMode.BW:
                        BlackPages.Add(tpcFilePage);
                        break;
                    case ColorMode.Color:
                        ColorPages.Add(tpcFilePage);
                        break;
                    case ColorMode.Unknown:
                        UnknownPages.Add(tpcFilePage);
                        break;
                    default:
                        break;
                }

                return true;
            }

            return false;
        }

        public List<TPCFilePage> AllPages
        {
            get
            {
                var allPages = new List<TPCFilePage>();
                allPages.AddRange(BlackPages);
                allPages.AddRange(ColorPages);
                allPages.AddRange(UnknownPages);
                return allPages;
            }
        }
	}
}
