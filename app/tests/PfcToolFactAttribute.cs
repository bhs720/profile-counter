using Xunit;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// A Fact that reports as skipped, rather than failing, when the native analyzer has
    /// not been built. The native half needs Visual Studio with the v142 C++ toolset and
    /// takes minutes; `dotnet test` after a C# change must not depend on it.
    /// </summary>
    public sealed class PfcToolFactAttribute : FactAttribute
    {
        public PfcToolFactAttribute()
        {
            if (RepoLayout.PfcToolPath == null)
            {
                Skip = "pfc-tool.exe has not been built. Run: msbuild app\\pfc-tool\\pfc-tool.sln /p:Configuration=Release /p:Platform=x64";
            }
            else if (RepoLayout.TestFilesDirectory == null)
            {
                Skip = "The 'test files' directory was not found.";
            }
        }
    }
}
