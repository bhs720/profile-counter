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
                return PfcToolLine.Header(
                    Convert.ToInt32(header.Groups[1].Value, CultureInfo.InvariantCulture),
                    Convert.ToInt32(header.Groups[2].Value, CultureInfo.InvariantCulture));
            }

            var page = PagePattern.Match(line);
            if (page.Success)
            {
                // pfc-tool.exe prints sizes with the C locale, so they must be parsed
                // culture-invariantly. Convert.ToDecimal uses CurrentCulture, where a
                // comma-decimal culture reads the '.' in "612.000000" as a group
                // separator and returns 612000000 -- every page size inflated by 10^6.
                return PfcToolLine.Page(
                    Convert.ToInt32(page.Groups[1].Value, CultureInfo.InvariantCulture),
                    decimal.Parse(page.Groups[2].Value, CultureInfo.InvariantCulture) / 72m,
                    decimal.Parse(page.Groups[3].Value, CultureInfo.InvariantCulture) / 72m,
                    ToColorMode(Convert.ToInt32(page.Groups[4].Value, CultureInfo.InvariantCulture)));
            }

            return PfcToolLine.Unrecognized();
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
