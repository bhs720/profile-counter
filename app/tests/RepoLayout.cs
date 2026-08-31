using System;
using System.IO;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// Finds the things the integration tests need by walking up from the test
    /// assembly's own directory. The test project deliberately does not output next to
    /// pfc-tool.exe -- the installer packages from that directory -- so the path has to
    /// be discovered rather than assumed.
    /// </summary>
    internal static class RepoLayout
    {
        /// <summary>Full path to pfc-tool.exe, or null when it has not been built.</summary>
        public static string PfcToolPath { get; private set; }

        /// <summary>Full path to the sample file directory, or null when it is absent.</summary>
        public static string TestFilesDirectory { get; private set; }

        static RepoLayout()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (PfcToolPath == null)
                {
                    // Release first: it is what the installer packages and what a
                    // developer is most likely to have built deliberately.
                    string release = Path.Combine(dir.FullName, @"app\x64\Release\pfc-tool.exe");
                    string debug = Path.Combine(dir.FullName, @"app\x64\Debug\pfc-tool.exe");

                    if (File.Exists(release))
                        PfcToolPath = release;
                    else if (File.Exists(debug))
                        PfcToolPath = debug;
                }

                if (TestFilesDirectory == null)
                {
                    string candidate = Path.Combine(dir.FullName, "test files");
                    if (Directory.Exists(candidate))
                        TestFilesDirectory = candidate;
                }

                if (PfcToolPath != null && TestFilesDirectory != null)
                    return;

                dir = dir.Parent;
            }
        }
    }
}
