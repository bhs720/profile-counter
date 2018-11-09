using System;
using System.Collections.Generic;

namespace TIFPDFCounter
{
	public class PageCounter
	{	
		readonly PageSize pageSize;

        List<TPCFilePage> blackPages;
        List<TPCFilePage> colorPages;
        List<TPCFilePage> unknownColorPages;

		public PageCounter(PageSize pageSize)
		{
			this.pageSize = pageSize;
            blackPages = new List<TPCFilePage>();
            colorPages = new List<TPCFilePage>();
            unknownColorPages = new List<TPCFilePage>();
		}

        public bool CountIfMatched(TPCFilePage tpcFilePage)
        {
            // null page size means this is the "unknown" page size counter
            if (pageSize == null || pageSize.Matches(tpcFilePage.Width, tpcFilePage.Height))
            {
                switch (tpcFilePage.ColorMode)
                {
                    case ColorMode.BW:
                        blackPages.Add(tpcFilePage);
                        break;
                    case ColorMode.Color:
                        colorPages.Add(tpcFilePage);
                        break;
                    case ColorMode.Unknown:
                        unknownColorPages.Add(tpcFilePage);
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
                allPages.AddRange(blackPages);
                allPages.AddRange(colorPages);
                allPages.AddRange(unknownColorPages);
                return allPages;
            }
        }

        public List<TPCFilePage> ColorPages { get { return colorPages; } }
        public List<TPCFilePage> BWPages { get { return blackPages; } }
        public List<TPCFilePage> UnknownColorPages { get { return unknownColorPages; } }

		public string Name { get { return pageSize == null ? "Unknown" : pageSize.Name; } }

		public PageSize PageSize { get { return pageSize; } }
	}
}
