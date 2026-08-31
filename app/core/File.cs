using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;

namespace TIFPDFCounter
{
    public class TPCFile
    {
        private string md5hash;
        private List<TPCFilePage> pages;

        public TPCFile(string fileName, int pageCount, int bookmarkCount)
        {
            Filename = fileName;
            pages = new List<TPCFilePage>(pageCount);
            try
            {
                FileSize = new FileInfo(Filename).Length;
            }
            catch
            {
                FileSize = 0;
            }

            PageCount = pageCount;
            BookmarkCount = bookmarkCount;
        }

        public string Filename { get; private set; }
        public ReadOnlyCollection<TPCFilePage> Pages { get { return pages.AsReadOnly(); } }
        public long FileSize { get; private set; }
        public int PageCount { get; private set; }
        public int BookmarkCount { get; private set; }

        public void AddPage(int pageNumber, decimal width, decimal height, ColorMode cm)
        {
            AddPage(new TPCFilePage(this, pageNumber, width, height, cm));
        }

        public void AddPage(TPCFilePage page)
        {
            pages.Add(page);
        }

        public string MD5Hash
        {
            get
            {
                if (md5hash == null)
                {
                    ComputeHash();
                }
                return md5hash;
            }
        }

        void ComputeHash()
        {
            var hex = new System.Text.StringBuilder();
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(Filename))
            {
                var hash = md5.ComputeHash(stream);
                foreach (byte b in hash)
                    hex.AppendFormat("{0:x2}", b);
            }
            this.md5hash = hex.ToString();
        }
    }
}
