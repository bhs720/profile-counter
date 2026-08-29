using Xunit;

namespace TIFPDFCounter.Tests
{
    public class UtilityTests
    {
        [Theory]
        [InlineData(0L, "0 B")]
        [InlineData(1023L, "1023 B")]
        [InlineData(1024L, "1 KB")]
        // 1536 is 1.5 KB, but BytesToString divides a long by 1024 repeatedly, so the
        // fraction is truncated away before it is ever formatted. That is what ships and
        // what users are used to reading. This test exists to stop a well-meaning
        // "correction" from changing the file sizes in the grid.
        [InlineData(1536L, "1 KB")]
        [InlineData(1048576L, "1 MB")]
        [InlineData(1073741824L, "1 GB")]
        public void BytesToString_FormatsWithTruncatingIntegerDivision(long bytes, string expected)
        {
            Assert.Equal(expected, Utility.BytesToString(bytes));
        }

        [Fact]
        public void TryGetFileLength_ReturnsTrueAndLengthForAnExistingFile()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllBytes(path, new byte[123]);

                long length;
                Assert.True(Utility.TryGetFileLength(path, out length));
                Assert.Equal(123L, length);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
