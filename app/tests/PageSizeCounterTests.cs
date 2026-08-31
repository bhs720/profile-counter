using Xunit;

namespace TIFPDFCounter.Tests
{
    public class PageSizeCounterTests
    {
        private static TPCFilePage Page(decimal width, decimal height, ColorMode mode)
        {
            var file = new TPCFile(@"C:\does\not\exist.pdf", pageCount: 1, bookmarkCount: 0);
            return new TPCFilePage(file, pageNumber: 1, width: width, height: height, colorMode: mode);
        }

        [Fact]
        public void IsMatch_SortsAMatchingPageIntoTheColourBucketAndReturnsTrue()
        {
            var counter = new PageSizeCounter(new PageSize("ANSI-A", 8m, 9m, 10m, 12m));

            Assert.True(counter.IsMatch(Page(8.5m, 11m, ColorMode.Color)));
            Assert.True(counter.IsMatch(Page(8.5m, 11m, ColorMode.BW)));
            Assert.True(counter.IsMatch(Page(8.5m, 11m, ColorMode.Unknown)));

            Assert.Single(counter.ColorPages);
            Assert.Single(counter.BlackPages);
            Assert.Single(counter.UnknownPages);
            Assert.Equal(3, counter.AllPages.Count);
        }

        [Fact]
        public void IsMatch_ReturnsFalseAndCountsNothingForANonMatchingPage()
        {
            var counter = new PageSizeCounter(new PageSize("ANSI-A", 8m, 9m, 10m, 12m));

            Assert.False(counter.IsMatch(Page(24m, 36m, ColorMode.Color)));
            Assert.Empty(counter.AllPages);
        }

        [Fact]
        public void ANullPageSizeIsTheCatchAllCounter()
        {
            // MainForm.DoPageSizeCount appends new PageSizeCounter(null) as the last
            // bucket so that every page lands somewhere.
            var counter = new PageSizeCounter(null);

            Assert.Equal("Unknown", counter.Name);
            Assert.True(counter.IsMatch(Page(999m, 999m, ColorMode.BW)));
        }
    }
}
