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

        [Theory]
        [InlineData(34, 512, "34 of 512 files failed")]
        [InlineData(1, 512, "1 of 512 files failed")]
        [InlineData(512, 512, "512 of 512 files failed")]
        // The plural agrees with the total, not the failure count, so a single failure
        // in a multi-file batch still reads "files". Only a one-file batch is singular.
        [InlineData(1, 1, "1 of 1 file failed")]
        public void DescribeBatchFailures_PluralizesOnTheTotal(int failed, int total, string expected)
        {
            Assert.Equal(expected, Utility.DescribeBatchFailures(failed, total));
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
