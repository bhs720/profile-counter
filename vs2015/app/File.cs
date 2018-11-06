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
        string fileName;
        TPCFilePage[] pages;
        string md5hash;

		public TPCFile(string fileName, int pageCount)
		{
            this.fileName = fileName;
            pages = new TPCFilePage[pageCount];
		}

        public string FileName { get { return fileName; } }
        public TPCFilePage[] Pages { get { return pages; } }
        public long FileSize { get { return new FileInfo(fileName).Length; } }
        
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
            {
                using (var stream = File.OpenRead(fileName))
                {
                    var hash = md5.ComputeHash(stream);
                    foreach (byte b in hash)
                        hex.AppendFormat("{0:x2}", b);
                }
            }
            this.md5hash = hex.ToString();
        }
	}
}
