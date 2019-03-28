using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public static class Utility
    {
        public static void InvokeIfRequired(Control ctrl, MethodInvoker action)
        {
            if (ctrl.InvokeRequired)
            {
                ctrl.Invoke(action);
            }
            else
            {
                action();
            }
        }

        public static string BytesToString(long byteCount)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int order = 0;
            while (byteCount >= 1024 && order < suffix.Length - 1)
            {
                order++;
                byteCount /= 1024;
            }

            return string.Format("{0:0.##} {1}", byteCount, suffix[order]);
        }

        public static bool TryGetFileLength(string fileName, out long fileLength)
        {
            int retry = 0;
            while (true)
            {
                try
                {
                    fileLength = (new System.IO.FileInfo(fileName)).Length;
                    return true;
                }
                catch
                {
                    if (retry == 5)
                    {
                        fileLength = 0;
                        return false;
                    }
                        
                    retry++;
                    System.Threading.Thread.Sleep(100);
                    continue;
                }
            }
        }
    }
}
