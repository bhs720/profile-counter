using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace ProFileCounter
{
	public class TPCFile
	{
        string fileName;
        TPCFilePage[] pages;
        string md5Hash;

		public TPCFile(string fileName, int pageCount)
		{
            this.fileName = fileName;
            pages = new TPCFilePage[pageCount];
		}

        public void ComputeHash()
        {
            //throw new Exception("Testing hash exception");
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
            md5Hash = hex.ToString();
        }

        public string FileName { get { return this.fileName; } }
        public TPCFilePage[] Pages { get { return this.pages; } }
        public string MD5Hash { get { return this.md5Hash; } }
	}
}
