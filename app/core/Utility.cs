using System;

namespace TIFPDFCounter
{
    public static class Utility
    {
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

        /// <summary>
        /// Describes a finished batch's failures for the process window's title bar,
        /// e.g. "34 of 512 files failed". The plural agrees with <paramref name="total"/>,
        /// not <paramref name="failed"/>, so only a one-file batch reads "1 of 1 file failed".
        /// </summary>
        public static string DescribeBatchFailures(int failed, int total)
        {
            return string.Format(
                "{0} of {1} file{2} failed",
                failed, total, total == 1 ? "" : "s");
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
