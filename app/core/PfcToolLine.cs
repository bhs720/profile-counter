namespace TIFPDFCounter
{
    public enum PfcToolLineKind
    {
        /// <summary>Nothing to parse, and not a protocol violation.</summary>
        Blank,
        /// <summary>PageCount=# BookmarkCount=#</summary>
        Header,
        /// <summary>Page=# Size=#.#,#.# Color=#</summary>
        Page,
        /// <summary>A protocol violation. The file fails.</summary>
        Unrecognized
    }

    /// <summary>
    /// One line of pfc-tool.exe's stdout, parsed. Sizes are already converted from the
    /// PDF points the tool prints into the inches the rest of the application uses.
    /// </summary>
    public sealed class PfcToolLine
    {
        private PfcToolLine() { }

        public PfcToolLineKind Kind { get; private set; }
        public int PageCount { get; private set; }
        public int BookmarkCount { get; private set; }
        public int PageNumber { get; private set; }
        public decimal WidthInches { get; private set; }
        public decimal HeightInches { get; private set; }
        public ColorMode ColorMode { get; private set; }

        internal static PfcToolLine Blank()
        {
            return new PfcToolLine { Kind = PfcToolLineKind.Blank };
        }

        internal static PfcToolLine Unrecognized()
        {
            return new PfcToolLine { Kind = PfcToolLineKind.Unrecognized };
        }

        internal static PfcToolLine Header(int pageCount, int bookmarkCount)
        {
            return new PfcToolLine
            {
                Kind = PfcToolLineKind.Header,
                PageCount = pageCount,
                BookmarkCount = bookmarkCount
            };
        }

        internal static PfcToolLine Page(int pageNumber, decimal widthInches, decimal heightInches, ColorMode colorMode)
        {
            return new PfcToolLine
            {
                Kind = PfcToolLineKind.Page,
                PageNumber = pageNumber,
                WidthInches = widthInches,
                HeightInches = heightInches,
                ColorMode = colorMode
            };
        }
    }
}
