namespace TIFPDFCounter
{
    /// <summary>
    /// What to ask pfc-tool.exe for, and where to find it. The GUI builds this from
    /// Settings.Current and hands it down: Settings shows a MessageBox on load failure,
    /// so it cannot live in a UI-free assembly, and nothing here reads it.
    /// </summary>
    public sealed class AnalysisOptions
    {
        /// <summary>
        /// A bare relative filename, resolved against the process working directory.
        /// That works for the GUI because ProFile Counter.exe and pfc-tool.exe ship side
        /// by side in app\x64\$(Configuration)\. A test host has a different working
        /// directory, which is why ToolPath is overridable at all.
        /// </summary>
        public const string DefaultToolPath = "pfc-tool.exe";

        public AnalysisOptions()
        {
            ToolPath = DefaultToolPath;
        }

        public string ToolPath { get; set; }

        /// <summary>
        /// Check whether the page is in colour (true), or get the page size only (false).
        /// </summary>
        public bool PerformColorAnalysis { get; set; }

        /// <summary>
        /// How far from gray a colour can be before it counts as colour. 0.02 is very
        /// strict; 0.25 allows for JPEG artifacts.
        /// </summary>
        public decimal ColorThreshold { get; set; }

        /// <summary>
        /// Check pixels exhaustively (true), or look at the image colorspace only (false).
        /// </summary>
        public bool CheckImagePixels { get; set; }
    }
}
