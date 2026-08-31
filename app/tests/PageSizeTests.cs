using Xunit;

namespace TIFPDFCounter.Tests
{
    public class PageSizeTests
    {
        private static PageSize AnsiA()
        {
            return new PageSize("ANSI-A", minWidth: 8m, maxWidth: 9m, minHeight: 10m, maxHeight: 12m);
        }

        [Fact]
        public void IsMatch_AcceptsPortrait()
        {
            Assert.True(AnsiA().IsMatch(8.5m, 11m));
        }

        [Fact]
        public void IsMatch_AcceptsLandscape()
        {
            // IsMatch tests both orientations, so a rotated page still buckets correctly.
            Assert.True(AnsiA().IsMatch(11m, 8.5m));
        }

        [Fact]
        public void IsMatch_RejectsASizeOutsideBothOrientations()
        {
            Assert.False(AnsiA().IsMatch(24m, 36m));
        }

        [Fact]
        public void IsMatch_IsInclusiveAtTheBounds()
        {
            Assert.True(AnsiA().IsMatch(8m, 10m));
            Assert.True(AnsiA().IsMatch(9m, 12m));
        }

        [Fact]
        public void Clone_CopiesEveryField()
        {
            var original = new PageSize("ARCH-D", 23m, 25m, 35m, 37m, active: false);
            var copy = original.Clone();

            Assert.Equal(original.Name, copy.Name);
            Assert.Equal(original.MinWidth, copy.MinWidth);
            Assert.Equal(original.MaxWidth, copy.MaxWidth);
            Assert.Equal(original.MinHeight, copy.MinHeight);
            Assert.Equal(original.MaxHeight, copy.MaxHeight);
            Assert.Equal(original.Active, copy.Active);
        }
    }
}
