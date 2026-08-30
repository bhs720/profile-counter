namespace TIFPDFCounter
{
    /// <summary>
    /// One file in a batch, identified by position. Every <see cref="AnalysisBatch"/>
    /// event carries the item, which is how a caller maps a result back to whatever it
    /// is displaying -- a grid row, in the GUI's case. This replaces a Tag property on
    /// FileAnalyzer, and does not depend on the filenames in a batch being distinct.
    /// </summary>
    public sealed class BatchItem
    {
        internal BatchItem(int index, string filename)
        {
            Index = index;
            Filename = filename;
        }

        public int Index { get; private set; }
        public string Filename { get; private set; }
    }
}
