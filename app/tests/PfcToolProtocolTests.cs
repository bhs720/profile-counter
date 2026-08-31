using System;
using System.Globalization;
using System.Threading;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class PfcToolProtocolTests
    {
        /// <summary>
        /// Runs an action with CurrentCulture set to a comma-decimal culture. Both the
        /// argument formatting and the size parsing were culture bugs once: German
        /// formatting emitted "0,25", which pfc-tool.exe rejects, and Convert.ToDecimal
        /// read the '.' in "612.000000" as a group separator and returned 612000000 --
        /// every page size inflated by a million.
        /// </summary>
        private static void InCommaDecimalCulture(Action action)
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                action();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Fact]
        public void Parse_ReadsTheHeaderLine()
        {
            var line = PfcToolProtocol.Parse("PageCount=12 BookmarkCount=3");

            Assert.Equal(PfcToolLineKind.Header, line.Kind);
            Assert.Equal(12, line.PageCount);
            Assert.Equal(3, line.BookmarkCount);
        }

        [Fact]
        public void Parse_ConvertsPointsToInches()
        {
            var line = PfcToolProtocol.Parse("Page=1 Size=612.000000,792.000000 Color=0");

            Assert.Equal(PfcToolLineKind.Page, line.Kind);
            Assert.Equal(1, line.PageNumber);
            Assert.Equal(8.5m, line.WidthInches);
            Assert.Equal(11m, line.HeightInches);
        }

        [Fact]
        public void Parse_ConvertsPointsToInchesUnderACommaDecimalCulture()
        {
            InCommaDecimalCulture(() =>
            {
                var line = PfcToolProtocol.Parse("Page=1 Size=612.000000,792.000000 Color=0");

                Assert.Equal(PfcToolLineKind.Page, line.Kind);
                Assert.Equal(8.5m, line.WidthInches);
                Assert.Equal(11m, line.HeightInches);
            });
        }

        [Theory]
        [InlineData(0, ColorMode.BW)]
        [InlineData(1, ColorMode.Color)]
        [InlineData(2, ColorMode.Color)]
        [InlineData(-1, ColorMode.Unknown)]
        [InlineData(99, ColorMode.Unknown)]
        public void ToColorMode_MapsTheProtocolValues(int value, ColorMode expected)
        {
            Assert.Equal(expected, PfcToolProtocol.ToColorMode(value));
        }

        [Fact]
        public void Parse_ReadsTheColourFieldOfAPageLine()
        {
            Assert.Equal(ColorMode.Color, PfcToolProtocol.Parse("Page=1 Size=612.0,792.0 Color=2").ColorMode);
            Assert.Equal(ColorMode.Unknown, PfcToolProtocol.Parse("Page=1 Size=612.0,792.0 Color=-1").ColorMode);
        }

        [Fact]
        public void Parse_TreatsAnEmptyLineAsBlankRatherThanAViolation()
        {
            Assert.Equal(PfcToolLineKind.Blank, PfcToolProtocol.Parse("").Kind);
            Assert.Equal(PfcToolLineKind.Blank, PfcToolProtocol.Parse(null).Kind);
        }

        [Fact]
        public void Parse_RejectsGarbage()
        {
            Assert.Equal(PfcToolLineKind.Unrecognized, PfcToolProtocol.Parse("error: cannot open file").Kind);
            Assert.Equal(PfcToolLineKind.Unrecognized, PfcToolProtocol.Parse("PageCount=12").Kind);
            Assert.Equal(PfcToolLineKind.Unrecognized, PfcToolProtocol.Parse("Page=1 Size=612.0 Color=0").Kind);
        }

        [Fact]
        public void Parse_RejectsANegativeSize()
        {
            // A page whose fz_load_page threw kept fz_empty_rect's inverted sentinel
            // bounds, so the tool reported Size=-4294967296.000000,-4294967296.000000.
            // The Size= pattern is unsigned on purpose, which makes that a protocol
            // violation rather than a plausible number quietly entering the totals.
            var line = PfcToolProtocol.Parse("Page=1 Size=-4294967296.000000,-4294967296.000000 Color=-1");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_RejectsAPageSizeThatOverflowsDecimal()
        {
            // pfc-tool.exe prints fz_rect floats with %f; a damaged PDF with a
            // garbage-but-finite MediaBox can print a width larger than decimal.MaxValue.
            // The regex still matches it, so this must fail the file rather than let
            // decimal.Parse throw out of Parse and take the process down.
            var line = PfcToolProtocol.Parse(
                "Page=1 Size=340282346638528859811704183484516925440.000000,792.000000 Color=0");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_RejectsAMultiDotPageSize()
        {
            // "1.2.3" matches the Size pattern's [\d\.]+ but is not a valid number.
            var line = PfcToolProtocol.Parse("Page=1 Size=1.2.3,792.000000 Color=0");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_RejectsAPageCountThatOverflowsInt32()
        {
            var line = PfcToolProtocol.Parse("PageCount=99999999999 BookmarkCount=0");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_RejectsAnImplausiblePageCount()
        {
            // PageCount=2000000000 parses as an int, and TPCFile then does
            // new List<TPCFilePage>(pageCount), which throws OutOfMemoryException on the
            // stdout reader thread -- taking the process down and losing the whole batch.
            // Treat an implausible count as a protocol violation so it fails the file,
            // which is what every other malformed line does.
            var line = PfcToolProtocol.Parse("PageCount=2000000000 BookmarkCount=0");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_AcceptsALargeButPlausiblePageCount()
        {
            var line = PfcToolProtocol.Parse("PageCount=1000000 BookmarkCount=0");

            Assert.Equal(PfcToolLineKind.Header, line.Kind);
            Assert.Equal(1000000, line.PageCount);
        }

        [Fact]
        public void FormatArguments_QuotesTheFilenameAndEmitsTheThreshold()
        {
            string args = PfcToolProtocol.FormatArguments(@"C:\files\a b.pdf", checkColor: true, colorThreshold: 0.25m, checkPixels: true);

            Assert.Equal("\"C:\\files\\a b.pdf\" 0.25 1", args);
        }

        [Fact]
        public void FormatArguments_EmitsAPointDecimalUnderACommaDecimalCulture()
        {
            InCommaDecimalCulture(() =>
            {
                string args = PfcToolProtocol.FormatArguments(@"C:\a.pdf", checkColor: true, colorThreshold: 0.25m, checkPixels: false);

                Assert.Equal("\"C:\\a.pdf\" 0.25 0", args);
            });
        }

        [Fact]
        public void FormatArguments_EmitsMinusOneWhenColourAnalysisIsOff()
        {
            string args = PfcToolProtocol.FormatArguments(@"C:\a.pdf", checkColor: false, colorThreshold: 0.25m, checkPixels: true);

            Assert.Equal("\"C:\\a.pdf\" -1 1", args);
        }
    }
}
