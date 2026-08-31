using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TIFPDFCounter
{
    /// <summary>
    /// The pfc-tool.exe command line and stdout protocol, in one place and with no
    /// state. The producer side is app/pfc-tool/main.c; if you change one, change the
    /// other.
    /// <code>
    /// pfc-tool.exe "&lt;filename&gt;" &lt;colorThreshold|-1&gt; &lt;checkPixels 0|1&gt;
    ///
    /// PageCount=&lt;n&gt; BookmarkCount=&lt;n&gt;
    /// Page=&lt;pageNum&gt; Size=&lt;widthPt&gt;,&lt;heightPt&gt; Color=&lt;-1|0|1|2&gt;
    /// </code>
    /// </summary>
    public static class PfcToolProtocol
    {
        private static readonly Regex HeaderPattern =
            new Regex(@"^PageCount=(\d+) BookmarkCount=(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Note that Size is unsigned. A page whose fz_load_page threw once reported
        /// fz_empty_rect's inverted sentinel as Size=-4294967296.000000,..., and this
        /// pattern is what makes such a line a protocol violation -- and so a failed
        /// file -- rather than a plausible number entering the page totals.
        /// </summary>
        private static readonly Regex PagePattern =
            new Regex(@"^Page=(\d+) Size=([\d\.]+),([\d\.]+) Color=(-?\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// A page count above this is treated as a protocol violation rather than a
        /// number. TPCFile passes the count to a List capacity, and an implausible one
        /// throws OutOfMemoryException on the stdout reader thread, where nothing catches
        /// it and the process dies. The bound is arbitrary but far above any real
        /// document -- the largest file in the problem-file corpus is in the low
        /// thousands of pages, and a capacity of a million allocates about 8 MB rather
        /// than throwing.
        /// </summary>
        private const int MaxPlausiblePageCount = 1000000;

        /// <summary>
        /// Builds the command line. The threshold must be formatted culture-invariantly:
        /// pfc-tool.exe parses it with the C locale, and a comma-decimal culture would
        /// otherwise emit "0,25", which the tool rejects.
        /// </summary>
        public static string FormatArguments(string filename, bool checkColor, decimal colorThreshold, bool checkPixels)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\" {1} {2}",
                filename,
                checkColor ? colorThreshold.ToString(CultureInfo.InvariantCulture) : "-1",
                checkPixels ? "1" : "0");
        }

        /// <summary>
        /// Parses one line of stdout. End-of-stream is not a line and is not passed
        /// here -- the process seam signals it separately.
        /// </summary>
        public static PfcToolLine Parse(string line)
        {
            if (string.IsNullOrEmpty(line))
                return PfcToolLine.Blank();

            var header = HeaderPattern.Match(line);
            if (header.Success)
            {
                int pageCount, bookmarkCount;
                if (!TryParseInt32(header.Groups[1].Value, out pageCount) ||
                    !TryParseInt32(header.Groups[2].Value, out bookmarkCount))
                {
                    return PfcToolLine.Unrecognized();
                }

                if (pageCount > MaxPlausiblePageCount)
                    return PfcToolLine.Unrecognized();

                return PfcToolLine.Header(pageCount, bookmarkCount);
            }

            var page = PagePattern.Match(line);
            if (page.Success)
            {
                // pfc-tool.exe prints sizes with the C locale, so they must be parsed
                // culture-invariantly. Parsing under CurrentCulture, where a
                // comma-decimal culture reads the '.' in "612.000000" as a group
                // separator, would return 612000000 -- every page size inflated by 10^6.
                int pageNumber, color;
                decimal widthPt, heightPt;
                if (!TryParseInt32(page.Groups[1].Value, out pageNumber) ||
                    !TryParseDecimal(page.Groups[2].Value, out widthPt) ||
                    !TryParseDecimal(page.Groups[3].Value, out heightPt) ||
                    !TryParseInt32(page.Groups[4].Value, out color))
                {
                    // The regex admits digit strings the type it feeds cannot actually
                    // represent -- e.g. a damaged PDF's garbage-but-finite MediaBox
                    // prints as a huge %f that overflows decimal, or a malformed
                    // multi-dot value like "1.2.3" that matches [\d\.]+ but is not a
                    // number at all. A line that matches the pattern but cannot be
                    // converted must fail the file, not throw out of Parse -- an
                    // uncaught exception here propagates out of FileAnalyzer.OnOutputLine
                    // on a thread pool thread and takes the whole process down.
                    return PfcToolLine.Unrecognized();
                }

                return PfcToolLine.Page(pageNumber, widthPt / 72m, heightPt / 72m, ToColorMode(color));
            }

            return PfcToolLine.Unrecognized();
        }

        private static bool TryParseInt32(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Maps the protocol's Color field: 0 is black and white, 1 and 2 are colour,
        /// and anything else -- including -1, meaning analysis was skipped or failed --
        /// is unknown.
        /// </summary>
        public static ColorMode ToColorMode(int color)
        {
            switch (color)
            {
                case 0:
                    return ColorMode.BW;
                case 1:
                case 2:
                    return ColorMode.Color;
                default:
                    return ColorMode.Unknown;
            }
        }
    }
}
