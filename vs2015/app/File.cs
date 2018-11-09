using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace TIFPDFCounter
{	
	public class TPCFile
	{
        string md5hash;

		public TPCFile(string fileName, int pageCount)
		{
            Filename = fileName;
            Pages = new List<TPCFilePage>(pageCount);
            try
            {
                FileSize = new FileInfo(Filename).Length;
            }
            catch
            {
                FileSize = 0;
            }
            
            PageCount = pageCount;
		}

        public string Filename { get; private set; }
        public List<TPCFilePage> Pages { get; private set; }
        public long FileSize { get; private set; }
        public int PageCount { get; private set; }

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
