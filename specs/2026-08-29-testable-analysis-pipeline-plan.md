# Testable Analysis Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract ProFile Counter's file-analysis pipeline out of the WinForms project into a UI-free class library, behind seams that let xUnit tests drive it without launching a real process or creating a window.

**Architecture:** A new `ProFileCounter.Core` assembly holds the value types, a pure stdout-protocol parser, a process seam (`IPfcToolProcess`), the `FileAnalyzer` that owns the three-signal completion handshake, and an `AnalysisBatch` that owns the bounded worker pool lifted out of `ProcessWindow`. A new xUnit project tests all of it; `ProcessWindow` keeps only grid painting and UI-thread marshalling.

**Tech Stack:** C# on .NET Framework 4.8, SDK-style projects, xUnit v2, `dotnet build` / `dotnet test`.

## Global Constraints

Copied verbatim from `specs/2026-08-29-testable-analysis-pipeline-design.md`. Every task's requirements implicitly include this section.

- **Namespace is `TIFPDFCounter` in both assemblies.** Namespaces span assemblies, so no `using` statement changes anywhere. Only duplicate *type names* collide — which affects exactly one type, `Utility`.
- **`app/core` outputs to `..\x64\$(Configuration)\`** with `AppendTargetFrameworkToOutputPath=false`. The path must be `$(ProjectDir)`-relative, never `$(SolutionDir)`-relative.
- **`app/tests` must NOT output to `app\x64\`.** Leave it on the default `bin\`, or the xunit assemblies land in the directory the installer packages from.
- **Neither new project sets `PlatformTarget`.** `dotnet test` on net48 may host at x86; an x64-marked core DLL would fail to load with `BadImageFormatException`.
- **No `.resx` files in the new projects.** The SDK's `EmbeddedResourceUseDependentUponConvention` produced wrong manifest names during the SDK conversion; stay out of that area.
- **The managed solution defines only `Debug|Any CPU` and `Release|Any CPU`.** New projects join those.
- **`Settings.cs` stays in the GUI** — it shows a `MessageBox` on load failure. Core never reads settings.
- **The test project references core only, never the GUI.**
- **`Utility.BytesToString`'s integer division stays as-is.** 1536 bytes displays as "1 KB". Lock it in with a test; do not correct it.
- Work happens on branch `feature/testable-analysis-pipeline`, PR into `develop`. Do not push.
- Every commit must leave `dotnet build app\ProFileCounter.sln` and `dotnet test app\ProFileCounter.sln` green.

## File Structure

**Created:**

| Path | Responsibility |
|---|---|
| `app/core/ProFileCounter.Core.csproj` | UI-free class library project |
| `app/core/Utility.cs` | `BytesToString`, `TryGetFileLength` |
| `app/core/PfcToolProtocol.cs` | Pure stdout-protocol parser + argument formatting |
| `app/core/PfcToolLine.cs` | Parsed-line value returned by the parser |
| `app/core/AnalysisOptions.cs` | Tool path + per-file analyzer configuration |
| `app/core/IPfcToolProcess.cs` | Process seam interface + factory interface |
| `app/core/PfcToolProcess.cs` | Real `System.Diagnostics.Process` implementation + factory |
| `app/core/AnalysisBatch.cs` | Bounded worker pool, lifted from `ProcessWindow` |
| `app/core/BatchItem.cs` | Index + filename pair carried by batch events |
| `app/gui/UiThread.cs` | WinForms half of the old `Utility` |
| `app/tests/ProFileCounter.Tests.csproj` | xUnit project |
| `app/tests/UtilityTests.cs` | `BytesToString` behaviour lock-in |
| `app/tests/PageSizeTests.cs` | `IsMatch` orientation rules |
| `app/tests/PageSizeCounterTests.cs` | Bucketing by size and colour |
| `app/tests/PageSizeXmlTests.cs` | Serialized XML shape after the assembly move |
| `app/tests/PfcToolProtocolTests.cs` | Parser + argument formatting |
| `app/tests/FakePfcToolProcess.cs` | Test double for the process seam |
| `app/tests/FileAnalyzerTests.cs` | Completion handshake and failure rules |
| `app/tests/AnalysisBatchTests.cs` | Pool invariants |
| `app/tests/RepoLayout.cs` | Locates `pfc-tool.exe` and `test files\` |
| `app/tests/PfcToolFactAttribute.cs` | Skips integration tests when the tool is unbuilt |
| `app/tests/PfcToolIntegrationTests.cs` | Protocol conformance against the real tool |

**Moved (use `git mv` to preserve history):** `FilePage.cs`, `File.cs`, `PageSize.cs`, `PageSizeCounter.cs`, `FileAnalyzer.cs` from `app/gui/` to `app/core/`.

**Modified:** `app/ProFileCounter.sln`, `app/gui/ProFile Counter.csproj`, `app/gui/Utility.cs` (becomes `UiThread.cs`), `app/gui/ProcessWindow.cs`, `app/gui/MainForm.cs`, `installer/script.iss`, `CLAUDE.md`.

---

### Task 1: Scaffold the core and test projects

**Files:**
- Create: `app/core/ProFileCounter.Core.csproj`
- Create: `app/tests/ProFileCounter.Tests.csproj`
- Create: `app/tests/ScaffoldTests.cs` (deleted again in Task 2)
- Modify: `app/ProFileCounter.sln`
- Modify: `app/gui/ProFile Counter.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: assembly `ProFileCounter.Core` at `app\x64\$(Configuration)\ProFileCounter.Core.dll`; test assembly `ProFileCounter.Tests` in `app\tests\bin\$(Configuration)\net48\`.

- [ ] **Step 1: Create the core project file**

Create `app/core/ProFileCounter.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>

    <!-- The namespace is shared with the GUI assembly on purpose: namespaces span
         assemblies, so moving a type here changes no using statement anywhere. -->
    <RootNamespace>TIFPDFCounter</RootNamespace>
    <AssemblyName>ProFileCounter.Core</AssemblyName>

    <!-- Lands beside ProFile Counter.exe and pfc-tool.exe, which have to sit side by
         side because FileAnalyzer launches the tool as a bare relative filename.
         Project-relative rather than $(SolutionDir)-relative so it does not depend on
         which solution builds it -- a $(SolutionDir)-relative path would relocate the
         output silently rather than failing. -->
    <OutputPath>..\x64\$(Configuration)\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>

    <!-- Deliberately no PlatformTarget. The GUI is x64 because it hosts WinForms and
         launches an x64 child; a managed library needs neither. dotnet test on net48
         may host the tests at x86, and an x64-marked assembly would then fail to load
         with BadImageFormatException. AnyCPU loads under either host. -->

    <WarningLevel>4</WarningLevel>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <DebugType>full</DebugType>
    <DebugSymbols>true</DebugSymbols>
    <CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <!-- Matches the GUI: Release has always shipped without a PDB. -->
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
  </PropertyGroup>

  <!-- The SDK does not add framework references for .NET Framework targets. -->
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml" />
  </ItemGroup>

  <ItemGroup>
    <!-- Supplies the net48 reference assemblies from NuGet, so the managed build does
         not require a Visual Studio targeting pack to be installed. -->
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the test project file**

Create `app/tests/ProFileCounter.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>TIFPDFCounter.Tests</RootNamespace>
    <AssemblyName>ProFileCounter.Tests</AssemblyName>
    <IsPackable>false</IsPackable>

    <!-- Deliberately NOT ..\x64\$(Configuration)\. The test project keeps the default
         bin\ output: the installer packages from app\x64\Release\, and the xunit and
         testhost assemblies must never land there. -->

    <!-- Deliberately no PlatformTarget; see ProFileCounter.Core.csproj. -->

    <WarningLevel>4</WarningLevel>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Xml" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\core\ProFileCounter.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Add a scaffold test that proves the harness runs**

Create `app/tests/ScaffoldTests.cs`:

```csharp
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class ScaffoldTests
    {
        [Fact]
        public void TestHarnessRuns()
        {
            Assert.True(true);
        }
    }
}
```

- [ ] **Step 4: Add both projects to the solution**

Run from the repo root:

```bash
dotnet sln app/ProFileCounter.sln add app/core/ProFileCounter.Core.csproj app/tests/ProFileCounter.Tests.csproj
```

Then open `app/ProFileCounter.sln` and confirm both new projects have all four config mappings (`Debug|Any CPU.ActiveCfg`, `Debug|Any CPU.Build.0`, `Release|Any CPU.ActiveCfg`, `Release|Any CPU.Build.0`). `dotnet sln add` writes these automatically; if any `.Build.0` line is missing, add it by hand — without it the project is listed but never built.

- [ ] **Step 5: Reference core from the GUI**

In `app/gui/ProFile Counter.csproj`, add a new `ItemGroup` immediately before the final `</Project>`:

```xml
  <ItemGroup>
    <!-- The analysis pipeline lives in a UI-free assembly so it can be tested without
         building this project. Both assemblies share the TIFPDFCounter namespace, so
         no using statement here changes. -->
    <ProjectReference Include="..\core\ProFileCounter.Core.csproj" />
  </ItemGroup>
```

- [ ] **Step 6: Verify the build and the test host**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Debug
```

Expected: PASS, three projects built.

Then:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: 1 passing test. **If this fails with `BadImageFormatException`**, the AnyCPU assumption in the Global Constraints is wrong for this SDK version — add `<PlatformTarget>x64</PlatformTarget>` to *both* new projects and a `app/tests/.runsettings` with `<TargetPlatform>x64</TargetPlatform>`, then record the correction in the design spec. Do not proceed until `dotnet test` is green.

- [ ] **Step 7: Confirm the output layout**

Run:

```bash
ls app/x64/Debug/ProFileCounter.Core.dll && ls app/tests/bin/Debug/net48/xunit.core.dll
```

Expected: both exist. Then confirm nothing from xunit leaked into the packaged directory:

```bash
ls app/x64/Debug/ | grep -i xunit || echo "clean: no xunit in app/x64"
```

Expected: `clean: no xunit in app/x64`.

- [ ] **Step 8: Commit**

```bash
git add app/core app/tests app/ProFileCounter.sln "app/gui/ProFile Counter.csproj"
git commit -F - <<'EOF'
Scaffold a core library and a test project

FileAnalyzer and the worker pool inside ProcessWindow cannot be tested today:
one is entangled with System.Diagnostics.Process, the other with a WinForms
Form. Nothing can be extracted until there is somewhere UI-free to extract it
to, and something that runs the tests.

ProFileCounter.Core outputs beside ProFile Counter.exe and pfc-tool.exe, which
have to sit side by side because the tool is launched as a bare relative
filename. Its OutputPath is project-relative for the same reason the GUI's is:
a $(SolutionDir)-relative path relocates output silently rather than failing.

The test project deliberately keeps the default bin\ output. The installer
packages from app\x64\Release\, and the xunit and testhost assemblies must
never land there.

Neither project sets PlatformTarget. The GUI is x64 because it hosts WinForms
and launches an x64 child; a managed library needs neither, and dotnet test on
net48 may host at x86, where an x64-marked assembly fails to load.

EOF
```

Append the `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>` trailer to this and every commit in this plan.

---

### Task 2: Move the value types and split Utility

**Files:**
- Move: `app/gui/FilePage.cs`, `app/gui/File.cs`, `app/gui/PageSize.cs`, `app/gui/PageSizeCounter.cs` → `app/core/`
- Create: `app/core/Utility.cs`
- Move: `app/gui/Utility.cs` → `app/gui/UiThread.cs` (class renamed)
- Modify: `app/gui/ProcessWindow.cs:89`, `app/gui/ProcessWindow.cs:104`
- Delete: `app/tests/ScaffoldTests.cs`
- Test: `app/tests/UtilityTests.cs`, `app/tests/PageSizeTests.cs`, `app/tests/PageSizeCounterTests.cs`, `app/tests/PageSizeXmlTests.cs`

**Interfaces:**
- Consumes: the core project from Task 1.
- Produces: `TIFPDFCounter.ColorMode`, `TPCFilePage`, `TPCFile`, `PageSize`, `PageSizeCounter` and `Utility.BytesToString(long)` / `Utility.TryGetFileLength(string, out long)` in `ProFileCounter.Core`. `TIFPDFCounter.UiThread.InvokeIfRequired(Control, MethodInvoker)` and `UiThread.BeginInvokeIfRequired(Control, MethodInvoker)` in the GUI.

- [ ] **Step 1: Write the failing tests**

Create `app/tests/UtilityTests.cs`:

```csharp
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
```

Note: there is deliberately no test for `TryGetFileLength` failing. It retries five times with a 100 ms sleep between attempts, so a missing-file test would take half a second to assert something uninteresting.

Create `app/tests/PageSizeTests.cs`:

```csharp
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
```

Create `app/tests/PageSizeCounterTests.cs`:

```csharp
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
```

Create `app/tests/PageSizeXmlTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class PageSizeXmlTests
    {
        /// <summary>
        /// Settings.Load fails closed: any unknown XML element or attribute is a hard
        /// error that silently falls back to defaults and throws away the user's
        /// configured page sizes. Serialization is by type shape rather than by
        /// assembly, so moving PageSize into ProFileCounter.Core should be inert -- this
        /// asserts that rather than assuming it, and will also catch a later property
        /// rename that would quietly discard everyone's settings.
        /// </summary>
        [Fact]
        public void PageSize_SerializesToTheExpectedElementNames()
        {
            var sizes = new List<PageSize>
            {
                new PageSize("ANSI-A [ 8.5 \u00D7 11 ]", 8m, 9m, 10m, 12m)
            };

            string xml;
            var serializer = new XmlSerializer(typeof(List<PageSize>));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, sizes);
                xml = writer.ToString();
            }

            Assert.Contains("<PageSize>", xml);
            Assert.Contains("<MinWidth>8</MinWidth>", xml);
            Assert.Contains("<MinHeight>10</MinHeight>", xml);
            Assert.Contains("<MaxWidth>9</MaxWidth>", xml);
            Assert.Contains("<MaxHeight>12</MaxHeight>", xml);
            Assert.Contains("<Name>ANSI-A [ 8.5 \u00D7 11 ]</Name>", xml);
            Assert.Contains("<Active>true</Active>", xml);
        }

        [Fact]
        public void PageSize_RoundTripsThroughXmlUnchanged()
        {
            var original = new PageSize("ARCH-D [ 24 \u00D7 36 ]", 23m, 25m, 35m, 37m, active: false);

            var serializer = new XmlSerializer(typeof(PageSize));
            string xml;
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, original);
                xml = writer.ToString();
            }

            PageSize restored;
            using (var reader = new StringReader(xml))
            {
                restored = (PageSize)serializer.Deserialize(reader);
            }

            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.MinWidth, restored.MinWidth);
            Assert.Equal(original.MaxWidth, restored.MaxWidth);
            Assert.Equal(original.MinHeight, restored.MinHeight);
            Assert.Equal(original.MaxHeight, restored.MaxHeight);
            Assert.Equal(original.Active, restored.Active);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: FAIL, compile errors — `The type or namespace name 'PageSize' could not be found`, same for `Utility`, `TPCFile`, `TPCFilePage`, `ColorMode`, `PageSizeCounter`.

- [ ] **Step 3: Move the value types**

Run from the repo root:

```bash
git mv app/gui/FilePage.cs app/core/FilePage.cs && git mv app/gui/File.cs app/core/File.cs && git mv app/gui/PageSize.cs app/core/PageSize.cs && git mv app/gui/PageSizeCounter.cs app/core/PageSizeCounter.cs
```

Do not edit the contents of any of these four files. `TPCFile`'s constructor keeps its `new FileInfo(...).Length` call — it already catches and degrades to 0.

- [ ] **Step 4: Create the core half of Utility**

Create `app/core/Utility.cs`:

```csharp
using System;

namespace TIFPDFCounter
{
    public static class Utility
    {
        public static string BytesToString(long byteCount)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int order = 0;
            while (byteCount >= 1024 && order < suffix.Length - 1)
            {
                order++;
                byteCount /= 1024;
            }

            return string.Format("{0:0.##} {1}", byteCount, suffix[order]);
        }

        public static bool TryGetFileLength(string fileName, out long fileLength)
        {
            int retry = 0;
            while (true)
            {
                try
                {
                    fileLength = (new System.IO.FileInfo(fileName)).Length;
                    return true;
                }
                catch
                {
                    if (retry == 5)
                    {
                        fileLength = 0;
                        return false;
                    }

                    retry++;
                    System.Threading.Thread.Sleep(100);
                    continue;
                }
            }
        }
    }
}
```

- [ ] **Step 5: Rename the GUI half**

Run:

```bash
git mv app/gui/Utility.cs app/gui/UiThread.cs
```

Then edit `app/gui/UiThread.cs`: rename the class from `Utility` to `UiThread`, and delete the `BytesToString` and `TryGetFileLength` methods (they now live in core). Keep `InvokeIfRequired` and `BeginInvokeIfRequired` **with their full comments verbatim** — the comment on `BeginInvokeIfRequired` documents a starvation bug and a stack overflow, and Task 6 depends on it. The `using` list can be trimmed to `System`, `System.ComponentModel` and `System.Windows.Forms`.

The `Utility` type name cannot exist in both assemblies. Renaming the GUI half is the cheaper side: two call sites.

- [ ] **Step 6: Update the two call sites**

In `app/gui/ProcessWindow.cs`, change `Utility.BeginInvokeIfRequired(this, () =>` to `UiThread.BeginInvokeIfRequired(this, () =>` at both line 89 and line 104.

Leave lines 186 and 188 alone — `Utility.TryGetFileLength` and `Utility.BytesToString` keep resolving, now to the core assembly.

- [ ] **Step 7: Delete the scaffold test**

```bash
git rm app/tests/ScaffoldTests.cs
```

- [ ] **Step 8: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS, all tests green.

- [ ] **Step 9: Verify no new build warnings**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 10: Commit**

```bash
git add -A app/core app/gui app/tests
git commit -F - <<'EOF'
Move the value types and the non-UI helpers into core

The analysis pipeline cannot move out of the WinForms project until the types
it produces have moved: TPCFile, TPCFilePage and ColorMode are what FileAnalyzer
builds, and PageSize and PageSizeCounter turn those pages into the customer-
facing totals. All five are UI-free already; they were only in the GUI project
because that is where the project was.

The namespace stays TIFPDFCounter in both assemblies, so no using statement
changes. Only duplicate type names would collide, which affects exactly one:
Utility. Its two non-UI helpers move to core keeping the name, and the WinForms
half becomes UiThread -- the cheaper rename, at two call sites.

TPCFile's constructor keeps its FileInfo hit. It already degrades to 0 in a
catch, so a test can construct one for a path that does not exist.

Tests come with the move rather than after it. BytesToString divides a long by
1024 repeatedly, so 1536 bytes reads as "1 KB" and not "1.5 KB"; the test says
so explicitly, because that is shipped behaviour users read in the grid rather
than a bug to correct. The PageSize XML tests exist because Settings.Load fails
closed -- an unknown element or attribute silently discards the user's
configured page sizes -- so the serialized shape surviving the assembly move is
verified rather than assumed.

EOF
```

---

### Task 3: Move FileAnalyzer into core unchanged

**Files:**
- Move: `app/gui/FileAnalyzer.cs` → `app/core/FileAnalyzer.cs`

**Interfaces:**
- Consumes: `TPCFile`, `ColorMode` from Task 2.
- Produces: `TIFPDFCounter.FileAnalyzer` in `ProFileCounter.Core`, with its current public surface unchanged: `FileAnalyzer(string, bool, decimal, bool)`, `Filename`, `Cancelled`, `Failed`, `Result`, `Errors`, `Tag`, `Go()`, `Cancel()`, `ProgressChanged`, `AnalysisComplete`.

This task is a pure file move with no edits, kept separate so the behaviour-changing work in Tasks 4–6 reviews as a readable diff rather than as a move tangled with a rewrite.

- [ ] **Step 1: Move the file**

```bash
git mv app/gui/FileAnalyzer.cs app/core/FileAnalyzer.cs
```

Do not edit the contents. `System.Diagnostics.Process`, `System.Text.RegularExpressions` and `System.Threading` all live in `System.dll` and `System.Core.dll`, which the core project already references.

- [ ] **Step 2: Verify the build**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Debug
```

Expected: PASS with 0 warnings. `ProcessWindow` still compiles unchanged: it references `FileAnalyzer` by the same name in the same namespace.

- [ ] **Step 3: Verify the tests still pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add -A app/core app/gui
git commit -F - <<'EOF'
Move FileAnalyzer into core unchanged

A pure file move, with no edits, so that the work that follows -- extracting the
parser, introducing a process seam, and fixing the exit-code guess -- reviews as
a diff of what actually changed rather than as a rewrite tangled up with a move.

Nothing at the call site changes. ProcessWindow refers to the same type name in
the same namespace, and namespaces span assemblies.

EOF
```

---

### Task 4: Extract the pure protocol parser

**Files:**
- Create: `app/core/PfcToolLine.cs`, `app/core/PfcToolProtocol.cs`, `app/core/AnalysisOptions.cs`
- Modify: `app/core/FileAnalyzer.cs`
- Test: `app/tests/PfcToolProtocolTests.cs`

**Interfaces:**
- Consumes: `ColorMode` from Task 2.
- Produces:
  - `enum PfcToolLineKind { Blank, Header, Page, Unrecognized }`
  - `sealed class PfcToolLine` with `Kind`, `PageCount`, `BookmarkCount`, `PageNumber`, `WidthInches`, `HeightInches`, `ColorMode` (all get-only) and static factories `Blank()`, `Header(int,int)`, `Page(int,decimal,decimal,ColorMode)`, `Unrecognized()`.
  - `static class PfcToolProtocol` with `string FormatArguments(string filename, bool checkColor, decimal colorThreshold, bool checkPixels)`, `PfcToolLine Parse(string line)` and `ColorMode ToColorMode(int color)`.
  - `sealed class AnalysisOptions` with `ToolPath` (default `"pfc-tool.exe"`), `PerformColorAnalysis`, `ColorThreshold`, `CheckImagePixels`, and `const string DefaultToolPath`.

- [ ] **Step 1: Write the failing tests**

Create `app/tests/PfcToolProtocolTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter PfcToolProtocolTests
```

Expected: FAIL, `The type or namespace name 'PfcToolProtocol' could not be found`.

- [ ] **Step 3: Write PfcToolLine**

Create `app/core/PfcToolLine.cs`:

```csharp
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
```

- [ ] **Step 4: Write PfcToolProtocol**

Create `app/core/PfcToolProtocol.cs`:

```csharp
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TIFPDFCounter
{
    /// <summary>
    /// The pfc-tool.exe command line and stdout protocol, in one place and with no
    /// state. The producer side is app/pfc-tool/main.c; if you change one, change the
    /// other.
    /// <code>
    /// pfc-tool.exe "&lt;filename&gt;" &lt;colorThreshold|-1&gt; &lt;checkPixels 0|1&gt;
    ///
    /// PageCount=&lt;n&gt; BookmarkCount=&lt;n&gt;
    /// Page=&lt;pageNum&gt; Size=&lt;widthPt&gt;,&lt;heightPt&gt; Color=&lt;-1|0|1|2&gt;
    /// </code>
    /// </summary>
    public static class PfcToolProtocol
    {
        private static readonly Regex HeaderPattern =
            new Regex(@"^PageCount=(\d+) BookmarkCount=(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Note that Size is unsigned. A page whose fz_load_page threw once reported
        /// fz_empty_rect's inverted sentinel as Size=-4294967296.000000,..., and this
        /// pattern is what makes such a line a protocol violation -- and so a failed
        /// file -- rather than a plausible number entering the page totals.
        /// </summary>
        private static readonly Regex PagePattern =
            new Regex(@"^Page=(\d+) Size=([\d\.]+),([\d\.]+) Color=(-?\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Builds the command line. The threshold must be formatted culture-invariantly:
        /// pfc-tool.exe parses it with the C locale, and a comma-decimal culture would
        /// otherwise emit "0,25", which the tool rejects.
        /// </summary>
        public static string FormatArguments(string filename, bool checkColor, decimal colorThreshold, bool checkPixels)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\" {1} {2}",
                filename,
                checkColor ? colorThreshold.ToString(CultureInfo.InvariantCulture) : "-1",
                checkPixels ? "1" : "0");
        }

        /// <summary>
        /// Parses one line of stdout. End-of-stream is not a line and is not passed
        /// here -- the process seam signals it separately.
        /// </summary>
        public static PfcToolLine Parse(string line)
        {
            if (string.IsNullOrEmpty(line))
                return PfcToolLine.Blank();

            var header = HeaderPattern.Match(line);
            if (header.Success)
            {
                return PfcToolLine.Header(
                    Convert.ToInt32(header.Groups[1].Value, CultureInfo.InvariantCulture),
                    Convert.ToInt32(header.Groups[2].Value, CultureInfo.InvariantCulture));
            }

            var page = PagePattern.Match(line);
            if (page.Success)
            {
                // pfc-tool.exe prints sizes with the C locale, so they must be parsed
                // culture-invariantly. Convert.ToDecimal uses CurrentCulture, where a
                // comma-decimal culture reads the '.' in "612.000000" as a group
                // separator and returns 612000000 -- every page size inflated by 10^6.
                return PfcToolLine.Page(
                    Convert.ToInt32(page.Groups[1].Value, CultureInfo.InvariantCulture),
                    decimal.Parse(page.Groups[2].Value, CultureInfo.InvariantCulture) / 72m,
                    decimal.Parse(page.Groups[3].Value, CultureInfo.InvariantCulture) / 72m,
                    ToColorMode(Convert.ToInt32(page.Groups[4].Value, CultureInfo.InvariantCulture)));
            }

            return PfcToolLine.Unrecognized();
        }

        /// <summary>
        /// Maps the protocol's Color field: 0 is black and white, 1 and 2 are colour,
        /// and anything else -- including -1, meaning analysis was skipped or failed --
        /// is unknown.
        /// </summary>
        public static ColorMode ToColorMode(int color)
        {
            switch (color)
            {
                case 0:
                    return ColorMode.BW;
                case 1:
                case 2:
                    return ColorMode.Color;
                default:
                    return ColorMode.Unknown;
            }
        }
    }
}
```

- [ ] **Step 5: Write AnalysisOptions**

Create `app/core/AnalysisOptions.cs`:

```csharp
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
```

- [ ] **Step 6: Route FileAnalyzer through the parser**

In `app/core/FileAnalyzer.cs`, replace the body of `process_OutputDataReceived`'s `else` branch (everything from `var matchPageCount = ...` to the closing brace of the final `else`) with:

```csharp
                var line = PfcToolProtocol.Parse(e.Data);

                switch (line.Kind)
                {
                    case PfcToolLineKind.Blank:
                        break;

                    case PfcToolLineKind.Header:
                        Result = new TPCFile(Filename, line.PageCount, line.BookmarkCount);
                        ProgressChanged.Invoke(this, 0, line.PageCount);
                        break;

                    case PfcToolLineKind.Page:
                        if (Result == null)
                        {
                            Fail("Page spec came before page count");
                            return;
                        }

                        Result.AddPage(line.PageNumber, line.WidthInches, line.HeightInches, line.ColorMode);

                        if (lastProgress == null || (DateTime.Now - lastProgress) > progressInterval)
                        {
                            lastProgress = DateTime.Now;
                            ProgressChanged.Invoke(this, line.PageNumber, Result.PageCount);
                        }
                        break;

                    default:
                        Fail("Text was not in an expected format: " + e.Data);
                        return;
                }
```

Also replace the `else if (e.Data.Length == 0)` branch by folding it into the parse — `Parse("")` returns `Blank`. The method keeps its `if (e.Data == null) { SignalArrived(); return; }` guard exactly as it is: only null means end-of-file, and treating a blank line alike would signal completion twice.

Remove the now-unused `using System.Globalization;` and `using System.Text.RegularExpressions;` if the compiler warns. Leave the `Errors`, `Kill`, `Complete`, `Validate` and signal-counting code untouched — Task 5 changes those.

The old code guarded each regex with `matchPageCount.Groups.Count == 3` and `matchPageSpec.Groups.Count == 5`. Both are tautological when `Success` is true, and they are dropped.

- [ ] **Step 7: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A app/core app/tests
git commit -F - <<'EOF'
Extract the stdout protocol into a pure parser

Three of the fixes this code carries are one-line culture and pattern details
buried inside a Process callback, where nothing could reach them: the
invariant-culture size parse, the invariant-culture threshold formatting, and
the unsigned Size= pattern that makes a bogus page size a protocol violation
rather than a plausible number entering the customer-facing totals.

PfcToolProtocol is a pure function of one line of text, so each of those is now
a test rather than a comment. Two of them run under a comma-decimal culture,
which is the condition that broke them in the first place -- German formatting
emitted "0,25", which the tool rejects, and Convert.ToDecimal read the '.' in
"612.000000" as a group separator and inflated every page size by a million.

AnalysisOptions comes with it, so the tool path is something the caller supplies
rather than a constant compiled into the analyzer. The GUI keeps passing the
bare relative filename it always did; only a test host needs anything else.

FileAnalyzer's behaviour is unchanged. The two tautological Groups.Count guards
are dropped -- they are always satisfied when the match succeeded.

EOF
```

---

### Task 5: Introduce the process seam and stop guessing the exit code

**Files:**
- Create: `app/core/IPfcToolProcess.cs`, `app/core/PfcToolProcess.cs`
- Modify: `app/core/FileAnalyzer.cs`
- Test: `app/tests/FakePfcToolProcess.cs`, `app/tests/FileAnalyzerTests.cs`

**Interfaces:**
- Consumes: `PfcToolProtocol`, `AnalysisOptions` from Task 4.
- Produces:
  - `interface IPfcToolProcess : IDisposable` with events `OutputLineReceived(string)`, `OutputEnded()`, `ErrorLineReceived(string)`, `ErrorEnded()`, `Exited(int)`, and methods `Start()`, `Kill()`.
  - `interface IPfcToolProcessFactory` with `IPfcToolProcess Create(string toolPath, string arguments)`.
  - `sealed class PfcToolProcessFactory : IPfcToolProcessFactory`.
  - `FileAnalyzer(string filename, AnalysisOptions options, IPfcToolProcessFactory factory, Func<DateTime> clock = null)`. `Tag` is removed.

- [ ] **Step 1: Write the test double**

Create `app/tests/FakePfcToolProcess.cs`:

```csharp
using System;
using System.Collections.Generic;
using TIFPDFCounter;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// A process that never exists. Tests drive the three completion signals -- stdout
    /// end-of-file, stderr end-of-file and exit -- independently and in any order,
    /// because the ordering between them is the thing most worth testing.
    /// </summary>
    public sealed class FakePfcToolProcess : IPfcToolProcess
    {
        public event Action<string> OutputLineReceived = delegate { };
        public event Action OutputEnded = delegate { };
        public event Action<string> ErrorLineReceived = delegate { };
        public event Action ErrorEnded = delegate { };
        public event Action<int> Exited = delegate { };

        public string ToolPath { get; private set; }
        public string Arguments { get; private set; }
        public bool Started { get; private set; }
        public bool Killed { get; private set; }
        public bool Disposed { get; private set; }

        /// <summary>When set, Start() throws it -- the missing pfc-tool.exe case.</summary>
        public Exception StartThrows { get; set; }

        public FakePfcToolProcess(string toolPath, string arguments)
        {
            ToolPath = toolPath;
            Arguments = arguments;
        }

        public void Start()
        {
            if (StartThrows != null)
                throw StartThrows;

            Started = true;
        }

        public void Kill()
        {
            Killed = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void EmitStdout(string line) { OutputLineReceived(line); }
        public void EmitStderr(string line) { ErrorLineReceived(line); }
        public void EndStdout() { OutputEnded(); }
        public void EndStderr() { ErrorEnded(); }
        public void Exit(int exitCode) { Exited(exitCode); }

        /// <summary>Delivers all three completion signals in the given order.</summary>
        public void Finish(int exitCode, params string[] signalOrder)
        {
            foreach (string signal in signalOrder)
            {
                switch (signal)
                {
                    case "stdout": EndStdout(); break;
                    case "stderr": EndStderr(); break;
                    case "exit": Exit(exitCode); break;
                    default: throw new ArgumentException("Unknown signal: " + signal);
                }
            }
        }
    }

    public sealed class FakePfcToolProcessFactory : IPfcToolProcessFactory
    {
        private readonly List<FakePfcToolProcess> created = new List<FakePfcToolProcess>();

        /// <summary>When set, every created process throws this from Start().</summary>
        public Exception StartThrows { get; set; }

        public IReadOnlyList<FakePfcToolProcess> Created { get { return created; } }

        public FakePfcToolProcess Last { get { return created[created.Count - 1]; } }

        public IPfcToolProcess Create(string toolPath, string arguments)
        {
            var process = new FakePfcToolProcess(toolPath, arguments) { StartThrows = StartThrows };
            lock (created) { created.Add(process); }
            return process;
        }
    }
}
```

- [ ] **Step 2: Write the failing analyzer tests**

Create `app/tests/FileAnalyzerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class FileAnalyzerTests
    {
        private static AnalysisOptions Options()
        {
            return new AnalysisOptions
            {
                PerformColorAnalysis = true,
                ColorThreshold = 0.25m,
                CheckImagePixels = true
            };
        }

        private sealed class Harness
        {
            public FakePfcToolProcessFactory Factory = new FakePfcToolProcessFactory();
            public FileAnalyzer Analyzer;
            public int CompletedCount;
            public List<Tuple<int, int>> Progress = new List<Tuple<int, int>>();
            public DateTime Now = new DateTime(2026, 1, 1, 0, 0, 0);

            public Harness(string filename = @"C:\files\a.pdf")
            {
                Analyzer = new FileAnalyzer(filename, Options(), Factory, () => Now);
                Analyzer.AnalysisComplete += a => CompletedCount++;
                Analyzer.ProgressChanged += (a, completed, total) => Progress.Add(Tuple.Create(completed, total));
            }

            public FakePfcToolProcess Process { get { return Factory.Last; } }

            public void GoAndReportOnePage()
            {
                Analyzer.Go();
                Process.EmitStdout("PageCount=1 BookmarkCount=0");
                Process.EmitStdout("Page=1 Size=612.000000,792.000000 Color=0");
            }
        }

        public static IEnumerable<object[]> SignalOrderings()
        {
            yield return new object[] { new[] { "stdout", "stderr", "exit" } };
            yield return new object[] { new[] { "stdout", "exit", "stderr" } };
            yield return new object[] { new[] { "stderr", "stdout", "exit" } };
            yield return new object[] { new[] { "stderr", "exit", "stdout" } };
            yield return new object[] { new[] { "exit", "stdout", "stderr" } };
            yield return new object[] { new[] { "exit", "stderr", "stdout" } };
        }

        [Theory]
        [MemberData(nameof(SignalOrderings))]
        public void CompletesExactlyOnceAfterAllThreeSignals_InAnyOrder(string[] order)
        {
            var h = new Harness();
            h.GoAndReportOnePage();

            // Two of the three signals must not be enough. Completing early is how a
            // failure message got truncated while stderr was still filling.
            h.Process.Finish(0, order[0]);
            Assert.Equal(0, h.CompletedCount);
            h.Process.Finish(0, order[1]);
            Assert.Equal(0, h.CompletedCount);

            h.Process.Finish(0, order[2]);
            Assert.Equal(1, h.CompletedCount);
            Assert.False(h.Analyzer.Failed);
            Assert.False(h.Analyzer.Cancelled);
            Assert.NotNull(h.Analyzer.Result);
            Assert.Equal(1, h.Analyzer.Result.PageCount);
            Assert.Single(h.Analyzer.Result.Pages);
        }

        [Fact]
        public void ANonZeroExitCodeFailsTheFile()
        {
            var h = new Harness();
            h.GoAndReportOnePage();
            h.Process.Finish(1, "stdout", "stderr", "exit");

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("exit code: 1", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void APageCountMismatchFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=3 BookmarkCount=0");
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Expected=3 Received=1", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void AZeroPageCountFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=0 BookmarkCount=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Page count is zero", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void NoHeaderLineFailsTheFileRatherThanThrowing()
        {
            // Reading Result.PageCount unguarded here used to throw
            // NullReferenceException on a thread pool thread, which takes the whole
            // application down.
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("No page count was reported", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void APageLineBeforeTheHeaderFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Page spec came before page count", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void GarbageOnStdoutFailsTheFile()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("error: cannot open document");
            h.Process.Finish(1, "stdout", "stderr", "exit");

            Assert.True(h.Analyzer.Failed);
            Assert.Contains("not in an expected format", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void ABlankStdoutLineIsNotAViolation()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=1 BookmarkCount=0");
            h.Process.EmitStdout("");
            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            h.Process.Finish(0, "stdout", "stderr", "exit");

            Assert.False(h.Analyzer.Failed);
        }

        [Fact]
        public void StderrIsAccumulated()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStderr("cannot open file");
            h.Process.EmitStderr("giving up");
            h.Process.Finish(1, "stdout", "stderr", "exit");

            string errors = h.Analyzer.Errors.ToString();
            Assert.Contains("cannot open file", errors);
            Assert.Contains("giving up", errors);
        }

        [Fact]
        public void AFailureToStartReportsAFailedFileAndStillCompletes()
        {
            // If the process never starts, none of the three signals can ever arrive, so
            // the batch would wait on this analyzer forever.
            // The process is created in the FileAnalyzer constructor, not in Go(), so
            // this has to be set on the process rather than on the factory.
            var h = new Harness();
            h.Process.StartThrows = new InvalidOperationException("The system cannot find the file specified");

            h.Analyzer.Go();

            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Failed);
            Assert.Contains("Could not start pfc-tool.exe", h.Analyzer.Errors.ToString());
        }

        [Fact]
        public void CancelMarksTheFileCancelledAndKillsTheProcess()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Analyzer.Cancel();

            Assert.True(h.Analyzer.Cancelled);
            Assert.True(h.Process.Killed);

            h.Process.Finish(1, "stdout", "stderr", "exit");
            Assert.Equal(1, h.CompletedCount);
            Assert.True(h.Analyzer.Cancelled);
        }

        [Fact]
        public void TheProcessIsLaunchedWithTheConfiguredPathAndArguments()
        {
            var options = Options();
            options.ToolPath = @"C:\build\pfc-tool.exe";
            var factory = new FakePfcToolProcessFactory();
            var analyzer = new FileAnalyzer(@"C:\files\a.pdf", options, factory);

            analyzer.Go();

            Assert.Equal(@"C:\build\pfc-tool.exe", factory.Last.ToolPath);
            Assert.Equal("\"C:\\files\\a.pdf\" 0.25 1", factory.Last.Arguments);
        }

        [Fact]
        public void ProgressIsThrottledToOneEventEveryFiveHundredMilliseconds()
        {
            var h = new Harness();
            h.Analyzer.Go();
            h.Process.EmitStdout("PageCount=4 BookmarkCount=0");

            // The header always reports progress, so the throttle starts from there.
            Assert.Single(h.Progress);
            Assert.Equal(Tuple.Create(0, 4), h.Progress[0]);

            h.Process.EmitStdout("Page=1 Size=612.0,792.0 Color=0");
            Assert.Equal(2, h.Progress.Count);

            // Within the interval: suppressed.
            h.Now = h.Now.AddMilliseconds(400);
            h.Process.EmitStdout("Page=2 Size=612.0,792.0 Color=0");
            Assert.Equal(2, h.Progress.Count);

            // Past the interval: reported.
            h.Now = h.Now.AddMilliseconds(200);
            h.Process.EmitStdout("Page=3 Size=612.0,792.0 Color=0");
            Assert.Equal(3, h.Progress.Count);
            Assert.Equal(Tuple.Create(3, 4), h.Progress[2]);
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter FileAnalyzerTests
```

Expected: FAIL, `The type or namespace name 'IPfcToolProcess' could not be found`.

- [ ] **Step 4: Write the seam interfaces**

Create `app/core/IPfcToolProcess.cs`:

```csharp
using System;

namespace TIFPDFCounter
{
    /// <summary>
    /// One pfc-tool.exe child process, as FileAnalyzer needs to see it.
    /// <para>
    /// The three completion signals are separate events rather than one "finished"
    /// callback because analysis is finished only once the process has exited AND both
    /// redirected streams have signalled end-of-file. Exit can fire before the
    /// asynchronous readers have delivered their final lines, and completing early
    /// truncates the very stderr message explaining a failure.
    /// </para>
    /// </summary>
    public interface IPfcToolProcess : IDisposable
    {
        /// <summary>One line of stdout. Never null; end-of-file is <see cref="OutputEnded"/>.</summary>
        event Action<string> OutputLineReceived;

        /// <summary>End of stdout.</summary>
        event Action OutputEnded;

        /// <summary>One line of stderr. Never null; end-of-file is <see cref="ErrorEnded"/>.</summary>
        event Action<string> ErrorLineReceived;

        /// <summary>End of stderr.</summary>
        event Action ErrorEnded;

        /// <summary>
        /// The process exited, carrying its exit code. Reading the code here rather than
        /// at completion time is what lets FileAnalyzer stop guessing: it used to read
        /// Process.ExitCode inside a try/catch that treated any failure as exit code 0,
        /// that is, as success.
        /// </summary>
        event Action<int> Exited;

        /// <summary>Starts the process. Throws if it cannot be started.</summary>
        void Start();

        /// <summary>Kills the process if it is still running. Never throws.</summary>
        void Kill();
    }

    public interface IPfcToolProcessFactory
    {
        IPfcToolProcess Create(string toolPath, string arguments);
    }
}
```

- [ ] **Step 5: Write the real implementation**

Create `app/core/PfcToolProcess.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Text;

namespace TIFPDFCounter
{
    /// <summary>
    /// The real thing: a redirected pfc-tool.exe child process.
    /// </summary>
    public sealed class PfcToolProcess : IPfcToolProcess
    {
        private Process process;

        public event Action<string> OutputLineReceived = delegate { };
        public event Action OutputEnded = delegate { };
        public event Action<string> ErrorLineReceived = delegate { };
        public event Action ErrorEnded = delegate { };
        public event Action<int> Exited = delegate { };

        public PfcToolProcess(string toolPath, string arguments)
        {
            process = new Process();
            process.StartInfo.FileName = toolPath;

            // Pass the arguments through unchanged. This used to be re-encoded as
            // Encoding.Default.GetString(Encoding.UTF8.GetBytes(args)), which corrupted
            // every non-ASCII filename: .NET hands Arguments to CreateProcessW as UTF-16,
            // so mangling the string first simply put mojibake on the command line.
            // pfc-tool.exe reads the real UTF-16 command line through wmain and converts
            // it to the UTF-8 that mupdf expects.
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // pfc-tool.exe writes UTF-8: filenames reach mupdf as UTF-8 and come back
            // inside its error text. Decoding that as the ANSI code page turned a
            // copyright sign in a failing path into "?" in the results grid, which reads
            // as a second fault rather than as the one being reported.
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.StartInfo.UseShellExecute = false;

            process.OutputDataReceived += (sender, e) =>
            {
                // Only null means end-of-file; an empty line is ordinary data. Treating
                // both alike would signal completion twice and could finish the file
                // before stderr had drained.
                if (e.Data == null)
                    OutputEnded();
                else
                    OutputLineReceived(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    ErrorEnded();
                else
                    ErrorLineReceived(e.Data);
            };

            process.Exited += (sender, e) => Exited(ReadExitCode());
            process.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Reads the exit code at the only moment it is reliably available: inside the
        /// Exited handler, before anything disposes the process. The fallback exists so
        /// that a disposal race cannot throw on a thread pool thread and take the
        /// application down with it; it is not the routine path it used to be.
        /// </summary>
        private int ReadExitCode()
        {
            try
            {
                Process p = process;
                return p == null ? 0 : p.ExitCode;
            }
            catch
            {
                return 0;
            }
        }

        public void Start()
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        public void Kill()
        {
            Process p = process;
            if (p == null)
                return;

            try
            {
                if (!p.HasExited)
                    p.Kill();
            }
            catch { /* swallow -- the process may have exited or been disposed already */ }
        }

        public void Dispose()
        {
            Process p = process;
            process = null;
            if (p == null)
                return;

            try { p.Close(); p.Dispose(); }
            catch { /* nothing useful left to do at this point */ }
        }
    }

    public sealed class PfcToolProcessFactory : IPfcToolProcessFactory
    {
        public IPfcToolProcess Create(string toolPath, string arguments)
        {
            return new PfcToolProcess(toolPath, arguments);
        }
    }
}
```

- [ ] **Step 6: Rewrite FileAnalyzer against the seam**

Replace `app/core/FileAnalyzer.cs` with the following. Every comment carried over from the original is load-bearing — it documents the hang, the starvation and the crash that produced this shape.

```csharp
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TIFPDFCounter
{
    /// <summary>
    /// Reads the stdout of pfc-tool.exe and provides a <see cref="Result"/>
    /// </summary>
    public class FileAnalyzer
    {
        private IPfcToolProcess process;
        private readonly Func<DateTime> clock;

        /// <summary>
        /// The exit code delivered with <see cref="IPfcToolProcess.Exited"/>. Null means
        /// the process never exited, which is reachable only when Start threw -- and the
        /// file has already been failed in that case.
        /// </summary>
        private int? exitCode;

        /// <summary>
        /// Holds the last moment in time when <see cref="ProgressChanged"/> was invoked.
        /// Null if the event was never invoked.
        /// </summary>
        private DateTime? lastProgress;

        /// <summary>
        /// The minimum amount of time to wait between invocations of <see cref="ProgressChanged"/> event handler.
        /// </summary>
        private TimeSpan progressInterval = new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 0, milliseconds: 500);

        /// <summary>
        /// Analysis is finished only once the process has exited AND both redirected
        /// streams have signalled end-of-file. Exit can fire before the asynchronous
        /// readers have delivered their final lines, so exit alone is not enough.
        /// <para>
        /// These are set from several different threads: the exit notification and the
        /// two stream readers each arrive on a thread pool thread. Nothing here blocks
        /// waiting for the others -- whichever signal lands last performs the
        /// completion. An earlier version had the exit handler spin in
        /// <c>Thread.Sleep</c> until stdout finished, which parked a pool thread per
        /// analyzer while the callback that would release it needed a pool thread of its
        /// own; a burst of fast-failing files could stall the batch, and a sentinel that
        /// never arrived stranded the file as "Processing" forever.
        /// </para>
        /// </summary>
        private int signalsOutstanding = 3;

        /// <summary>
        /// Guards <see cref="AnalysisComplete"/> so it is raised exactly once, whichever
        /// signal happens to arrive last.
        /// <para>
        /// Deliberately no timeout anywhere in this class. A deadline would be a guess
        /// about how fast a machine is, and the machines this ships to are not this one --
        /// slower disks, network shares, antivirus in the path, a loaded terminal server.
        /// Completion is defined by the three signals rather than by the clock, so it is
        /// correct regardless of how long any of it takes.
        /// </para>
        /// </summary>
        private int completionRaised;

        /// <summary>
        /// <see cref="Errors"/> is appended to from the stderr reader and from
        /// <see cref="Fail"/>, which can run on different threads at the same time.
        /// StringBuilder is not thread safe.
        /// </summary>
        private readonly object errorsLock = new object();

        public delegate void ProgressChangedEventHandler(FileAnalyzer instance, int completed, int total);
        public delegate void AnalysisCompleteEventHandler(FileAnalyzer instance);
        public event ProgressChangedEventHandler ProgressChanged = delegate { /* empty in case of no subscribers */ };
        public event AnalysisCompleteEventHandler AnalysisComplete = delegate { /* empty in case of no subscribers */ };

        /// <summary>
        /// The filename assigned to this <see cref="FileAnalyzer"/>.
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// <see cref="Cancel"/> was called. <see cref="Result"/> is invalid or null.
        /// </summary>
        public bool Cancelled { get; private set; }

        /// <summary>
        /// <see cref="Fail(string)"/> was called. <see cref="Result"/> is invalid or null.
        /// </summary>
        public bool Failed { get; private set; }

        /// <summary>
        /// May be null or invalid if <see cref="Cancelled"/> is true or <see cref="Failed"/> is true.
        /// </summary>
        public TPCFile Result { get; private set; }

        /// <summary>
        /// Output from process StandardError. <see cref="FileAnalyzer"/> may also add error messages here.
        /// </summary>
        public StringBuilder Errors { get; private set; }

        /// <param name="clock">
        /// Supplies "now" for the <see cref="ProgressChanged"/> throttle. Injected so the
        /// throttle can be tested without a test that depends on wall-clock timing.
        /// </param>
        public FileAnalyzer(string filename, AnalysisOptions options, IPfcToolProcessFactory processFactory, Func<DateTime> clock = null)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (processFactory == null) throw new ArgumentNullException("processFactory");

            Filename = filename;
            Errors = new StringBuilder();
            this.clock = clock ?? (() => DateTime.Now);

            string args = PfcToolProtocol.FormatArguments(
                filename,
                options.PerformColorAnalysis,
                options.ColorThreshold,
                options.CheckImagePixels);
            Debug.Print("pfc-tool.exe {0}", args);

            process = processFactory.Create(options.ToolPath, args);
            process.OutputLineReceived += OnOutputLine;
            process.OutputEnded += SignalArrived;
            process.ErrorLineReceived += OnErrorLine;
            process.ErrorEnded += OnErrorEnded;
            process.Exited += OnExited;
        }

        public void Go()
        {
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                // If the process never starts, none of the three completion signals can
                // ever arrive, so the caller would wait on this analyzer forever. Report
                // it as a failed file instead.
                Fail("Could not start pfc-tool.exe: " + ex.Message);
                Complete();
            }
        }

        public void Cancel()
        {
            Debug.Print("Cancel FileAnalyzer");
            Cancelled = true;
            Kill();
        }

        private void Fail(string message)
        {
            Debug.Print("Fail FileAnalyzer: " + message);
            AppendError(message);
            Failed = true;
            Kill();
        }

        private void AppendError(string message)
        {
            lock (errorsLock)
            {
                Errors.AppendLine(message);
            }
        }

        private void Kill()
        {
            // The process reference is cleared only after completion, but a stream
            // callback can still arrive afterwards and call Fail, so read it once.
            IPfcToolProcess p = process;
            if (p == null)
                return;

            p.Kill();
        }

        private void OnErrorLine(string line)
        {
            if (line.Length > 0)
                AppendError(line);
        }

        private void OnErrorEnded()
        {
            // Waiting for this matters: the grid shows the first 255 characters of
            // Errors when a file fails, and completing before stderr has drained can
            // truncate the very message explaining the failure.
            SignalArrived();
        }

        private void OnExited(int code)
        {
            // Exit is only one of the three signals; the readers may still have lines in
            // flight, and whichever signal lands last does the completing.
            exitCode = code;
            SignalArrived();
        }

        /// <summary>
        /// Records one of the three completion signals. The last one to arrive finishes
        /// the analysis, so no thread ever waits on another.
        /// </summary>
        private void SignalArrived()
        {
            if (Interlocked.Decrement(ref signalsOutstanding) == 0)
            {
                Complete();
            }
        }

        /// <summary>
        /// Finishes the analysis exactly once, on whichever thread delivered the last
        /// signal.
        /// </summary>
        private void Complete()
        {
            // Two signals can land at the same moment on different threads; only the
            // first caller gets to finish the file.
            if (Interlocked.CompareExchange(ref completionRaised, 1, 0) != 0)
                return;

            Validate();

            AnalysisComplete.Invoke(this);

            // Clear the reference before disposing so a late stream callback that calls
            // Fail -> Kill sees null rather than a disposed process.
            IPfcToolProcess p = process;
            process = null;
            if (p != null)
            {
                try { p.Dispose(); }
                catch { /* nothing useful left to do at this point */ }
            }
        }

        private void Validate()
        {
            if (exitCode.HasValue && exitCode.Value != 0)
            {
                Fail("pfc-tool.exe exit code: " + exitCode.Value);
            }

            if (!Failed && !Cancelled)
            {
                if (Result == null)
                {
                    // No PageCount line was ever parsed, so there is nothing to report.
                    // Reading Result.PageCount here used to throw on a thread pool
                    // thread, which takes the whole application down.
                    Fail("No page count was reported.");
                }
                else if (Result.PageCount == 0)
                {
                    Fail("Page count is zero.");
                }
                else if (Result.Pages.Count != Result.PageCount)
                {
                    Fail("Page count mismatch. Expected=" + Result.PageCount + " Received=" + Result.Pages.Count);
                }
            }
        }

        private void OnOutputLine(string data)
        {
            var line = PfcToolProtocol.Parse(data);

            switch (line.Kind)
            {
                case PfcToolLineKind.Blank:
                    // Blank line -- nothing to parse, and not a protocol violation.
                    break;

                case PfcToolLineKind.Header:
                    Result = new TPCFile(Filename, line.PageCount, line.BookmarkCount);
                    ProgressChanged.Invoke(this, 0, line.PageCount);
                    break;

                case PfcToolLineKind.Page:
                    if (Result == null)
                    {
                        Fail("Page spec came before page count");
                        return;
                    }

                    Result.AddPage(line.PageNumber, line.WidthInches, line.HeightInches, line.ColorMode);

                    DateTime now = clock();
                    if (lastProgress == null || (now - lastProgress) > progressInterval)
                    {
                        lastProgress = now;
                        ProgressChanged.Invoke(this, line.PageNumber, Result.PageCount);
                    }
                    break;

                default:
                    Fail("Text was not in an expected format: " + data);
                    return;
            }
        }
    }
}
```

Note the deliberate detail: `lastProgress` is left null by the header's `ProgressChanged` call, exactly as before, so the first page line always reports.

- [ ] **Step 7: Update the one caller**

In `app/gui/ProcessWindow.cs`, `NextFile()` constructs the analyzer. Change:

```csharp
                var analyzer = new FileAnalyzer(filename, Settings.Current.PerformColorAnalysis, Settings.Current.ColorThreshold, Settings.Current.CheckImagePixels);
```

to:

```csharp
                var analyzer = new FileAnalyzer(filename, analysisOptions, processFactory);
```

and add two readonly fields to `ProcessWindow`, initialised in the `ProcessWindow(List<string>)` constructor:

```csharp
        private readonly AnalysisOptions analysisOptions;
        private readonly IPfcToolProcessFactory processFactory;
```

```csharp
            analysisOptions = new AnalysisOptions
            {
                PerformColorAnalysis = Settings.Current.PerformColorAnalysis,
                ColorThreshold = Settings.Current.ColorThreshold,
                CheckImagePixels = Settings.Current.CheckImagePixels
            };
            processFactory = new PfcToolProcessFactory();
```

`FileAnalyzer.Tag` is gone, so `analyzer.Tag = dgvr;` and the two `(DataGridViewRow)instance.Tag` casts no longer compile. Replace the `Tag` mechanism with a temporary `Dictionary<FileAnalyzer, DataGridViewRow>` field:

```csharp
        private readonly Dictionary<FileAnalyzer, DataGridViewRow> rowByAnalyzer = new Dictionary<FileAnalyzer, DataGridViewRow>();
```

In `NextFile`, replace `analyzer.Tag = dgvr;` with `rowByAnalyzer[analyzer] = dgvr;`. In both handlers, replace `var dgvr = (DataGridViewRow)instance.Tag;` with `var dgvr = rowByAnalyzer[instance];`, and in `Analyzer_AnalysisComplete` add `rowByAnalyzer.Remove(instance);` immediately after the two event unsubscriptions. Task 6 removes all of this again when the batch takes over.

- [ ] **Step 8: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

- [ ] **Step 9: Verify no new build warnings**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 10: Commit**

```bash
git add -A app/core app/gui app/tests
git commit -F - <<'EOF'
Put a seam between FileAnalyzer and the process it launches

The completion handshake is the part of this code that has actually broken, and
it was the part no test could reach. Analysis finishes only when the process has
exited and both redirected streams have signalled end-of-file, and the ordering
between those three is not something the caller controls -- they each arrive on
a thread pool thread. That ordering is now driven directly: the tests deliver
the three signals in all six orders and assert that two are never enough.

IPfcToolProcess also carries the exit code on the exit signal, which removes a
guess. Validate() read Process.ExitCode inside a try/catch that treated any
failure to read it as exit code 0 -- that is, as success -- and by completion
time the reference it read could already have been cleared. The code is now
captured in the exit notification, where it is valid, and an analyzer whose
process never exited says so rather than assuming the best.

The 500 ms progress throttle takes an injected clock, so the rule is tested
without a test that depends on wall-clock timing.

Tag is gone. It existed solely to carry a DataGridViewRow for the caller;
ProcessWindow keeps a dictionary for now, and the batch extraction removes both.

EOF
```

---

### Task 6: Extract the worker pool into AnalysisBatch

**Files:**
- Create: `app/core/BatchItem.cs`, `app/core/AnalysisBatch.cs`
- Modify: `app/gui/ProcessWindow.cs`, `app/gui/MainForm.cs:116-123`
- Test: `app/tests/AnalysisBatchTests.cs`

**Interfaces:**
- Consumes: `FileAnalyzer`, `AnalysisOptions`, `IPfcToolProcessFactory` from Task 5.
- Produces:
  - `sealed class BatchItem` with `int Index`, `string Filename`.
  - `sealed class AnalysisBatch` with constructor `(IEnumerable<string> filenames, AnalysisOptions options, IPfcToolProcessFactory processFactory, int maxConcurrency = 0, Func<DateTime> clock = null)`; properties `IReadOnlyList<BatchItem> Items`, `IReadOnlyList<TPCFile> Results`, `IReadOnlyList<FileAnalyzer> Failures`, `bool Finished`; events `Action<BatchItem> FileStarted`, `Action<BatchItem, int, int> FileProgress`, `Action<BatchItem, FileAnalyzer> FileCompleted`, `Action BatchFinished`; methods `Start()`, `Cancel()`.
  - `maxConcurrency = 0` means "use `Math.Max(Environment.ProcessorCount - 1, 1)`".

- [ ] **Step 1: Write the failing tests**

Create `app/tests/AnalysisBatchTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class AnalysisBatchTests
    {
        private static AnalysisOptions Options()
        {
            return new AnalysisOptions { PerformColorAnalysis = true, ColorThreshold = 0.25m, CheckImagePixels = true };
        }

        private static List<string> Files(int count)
        {
            return Enumerable.Range(1, count).Select(i => @"C:\files\f" + i + ".pdf").ToList();
        }

        /// <summary>Drives one fake process through a clean single-page success.</summary>
        private static void SucceedOnePage(FakePfcToolProcess p)
        {
            p.EmitStdout("PageCount=1 BookmarkCount=0");
            p.EmitStdout("Page=1 Size=612.000000,792.000000 Color=0");
            p.Finish(0, "stdout", "stderr", "exit");
        }

        [Fact]
        public void NeverExceedsTheConcurrencyCap()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(10), Options(), factory, maxConcurrency: 3);

            batch.Start();

            Assert.Equal(3, factory.Created.Count);
        }

        [Fact]
        public void RefillsThePoolAsEachFileCompletes()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(5), Options(), factory, maxConcurrency: 2);

            batch.Start();
            Assert.Equal(2, factory.Created.Count);

            SucceedOnePage(factory.Created[0]);
            Assert.Equal(3, factory.Created.Count);

            SucceedOnePage(factory.Created[1]);
            Assert.Equal(4, factory.Created.Count);
        }

        [Fact]
        public void FinishesOnlyWhenTheQueueAndTheRunningSetAreBothEmpty()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(3), Options(), factory, maxConcurrency: 2);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();
            SucceedOnePage(factory.Created[0]);
            Assert.Equal(0, finished);
            SucceedOnePage(factory.Created[1]);
            Assert.Equal(0, finished);

            // The third and last file.
            SucceedOnePage(factory.Created[2]);
            Assert.Equal(1, finished);
            Assert.True(batch.Finished);
            Assert.Equal(3, batch.Results.Count);
            Assert.Empty(batch.Failures);
        }

        [Fact]
        public void AnEmptyFileListFinishesImmediately()
        {
            // ProcessWindow never finished one of these: NextFile did nothing, no
            // analyzer ever completed, and FinishBatch was never reached. It stayed
            // latent only because MainForm guards against an empty drop.
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(new List<string>(), Options(), factory);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();

            Assert.Equal(1, finished);
            Assert.True(batch.Finished);
            Assert.Empty(factory.Created);
        }

        [Fact]
        public void CancelDrainsTheQueueAndCancelsWhatIsRunning()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(6), Options(), factory, maxConcurrency: 2);

            batch.Start();
            Assert.Equal(2, factory.Created.Count);

            batch.Cancel();

            Assert.True(factory.Created[0].Killed);
            Assert.True(factory.Created[1].Killed);

            // No further files are started as the cancelled ones drain.
            factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            factory.Created[1].Finish(1, "stdout", "stderr", "exit");

            Assert.Equal(2, factory.Created.Count);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void AFailedFileIsRecordedAsAFailureAndTheBatchCarriesOn()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(2), Options(), factory, maxConcurrency: 1);

            batch.Start();
            factory.Created[0].EmitStderr("cannot open document");
            factory.Created[0].Finish(1, "stdout", "stderr", "exit");

            Assert.Single(batch.Failures);
            Assert.Equal(2, factory.Created.Count);

            SucceedOnePage(factory.Created[1]);

            Assert.Single(batch.Results);
            Assert.Single(batch.Failures);
            Assert.True(batch.Finished);
        }

        [Fact]
        public void EventsCarryTheItemThatIdentifiesTheFile()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(2), Options(), factory, maxConcurrency: 1);

            var started = new List<BatchItem>();
            var completed = new List<BatchItem>();
            batch.FileStarted += item => started.Add(item);
            batch.FileCompleted += (item, analyzer) => completed.Add(item);

            batch.Start();
            SucceedOnePage(factory.Created[0]);
            SucceedOnePage(factory.Created[1]);

            Assert.Equal(new[] { 0, 1 }, started.Select(i => i.Index).ToArray());
            Assert.Equal(new[] { 0, 1 }, completed.Select(i => i.Index).ToArray());
            Assert.Equal(@"C:\files\f1.pdf", started[0].Filename);
        }

        [Fact]
        public void ADuplicatePathIsAnalyzedOnce()
        {
            // Over-reporting page totals is not a cosmetic bug: customers price their own
            // customers' work from these numbers.
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(
                new List<string> { @"C:\files\a.pdf", @"C:\files\A.PDF", @"C:\files\b.pdf" },
                Options(), factory, maxConcurrency: 4);

            Assert.Equal(2, batch.Items.Count);

            batch.Start();

            Assert.Equal(2, factory.Created.Count);
        }

        [Fact]
        public void AStormOfSynchronousFailuresDoesNotOverflowTheStack()
        {
            // Go() raises AnalysisComplete on the calling thread when Start throws, so a
            // pump that recursed from its own completion handler would nest one frame set
            // per queued file. A missing pfc-tool.exe and a few hundred dropped files
            // would then overflow the stack, which .NET cannot catch.
            var factory = new FakePfcToolProcessFactory
            {
                StartThrows = new InvalidOperationException("The system cannot find the file specified")
            };
            var batch = new AnalysisBatch(Files(1000), Options(), factory, maxConcurrency: 4);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();

            Assert.Equal(1, finished);
            Assert.Equal(1000, batch.Failures.Count);
            Assert.Empty(batch.Results);
        }

        [Fact]
        public void ProgressIsForwardedWithTheItem()
        {
            var factory = new FakePfcToolProcessFactory();
            var batch = new AnalysisBatch(Files(1), Options(), factory, maxConcurrency: 1);

            var progress = new List<Tuple<int, int, int>>();
            batch.FileProgress += (item, completed, total) => progress.Add(Tuple.Create(item.Index, completed, total));

            batch.Start();
            factory.Created[0].EmitStdout("PageCount=4 BookmarkCount=0");

            Assert.Single(progress);
            Assert.Equal(Tuple.Create(0, 0, 4), progress[0]);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter AnalysisBatchTests
```

Expected: FAIL, `The type or namespace name 'AnalysisBatch' could not be found`.

- [ ] **Step 3: Write BatchItem**

Create `app/core/BatchItem.cs`:

```csharp
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
```

- [ ] **Step 4: Write AnalysisBatch**

Create `app/core/AnalysisBatch.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace TIFPDFCounter
{
    /// <summary>
    /// Runs a bounded pool of <see cref="FileAnalyzer"/> over a queue of files, keeping
    /// at most <c>maxConcurrency</c> pfc-tool.exe processes alive at once and refilling
    /// the pool as each one finishes.
    /// <para>
    /// Events are raised on whatever thread completed the work -- an analyzer's
    /// callbacks arrive on thread pool threads -- and never while the lock is held. A
    /// UI caller marshals them onto its own thread in its handlers. It must not do so by
    /// blocking: the UI thread does real work per completed file, and the threads it
    /// would block are the very thread pool threads the redirected stream readers need
    /// in order to deliver their end-of-file callbacks.
    /// </para>
    /// </summary>
    public sealed class AnalysisBatch
    {
        private readonly object gate = new object();
        private readonly Queue<BatchItem> queue = new Queue<BatchItem>();
        private readonly Dictionary<FileAnalyzer, BatchItem> running = new Dictionary<FileAnalyzer, BatchItem>();
        private readonly List<BatchItem> items = new List<BatchItem>();
        private readonly List<TPCFile> results = new List<TPCFile>();
        private readonly List<FileAnalyzer> failures = new List<FileAnalyzer>();

        private readonly AnalysisOptions options;
        private readonly IPfcToolProcessFactory processFactory;
        private readonly Func<DateTime> clock;
        private readonly int maxConcurrency;

        /// <summary>
        /// True while a thread is inside <see cref="Pump"/>. A completion that arrives
        /// during a pump -- including one raised synchronously by Go() -- records nothing
        /// more than "there is work to do" and returns, because the running pump
        /// re-evaluates on its next iteration. Without this, Go() raising
        /// AnalysisComplete on the calling thread would re-enter the pump from inside
        /// itself, nesting one frame set per queued file: a missing pfc-tool.exe and a
        /// few hundred dropped files would overflow the stack, which .NET cannot catch.
        /// </summary>
        private bool pumping;

        private bool cancelled;
        private bool finished;

        public event Action<BatchItem> FileStarted = delegate { };
        public event Action<BatchItem, int, int> FileProgress = delegate { };
        public event Action<BatchItem, FileAnalyzer> FileCompleted = delegate { };
        public event Action BatchFinished = delegate { };

        /// <param name="maxConcurrency">
        /// Zero means one process per core, less one, which is what ships.
        /// </param>
        public AnalysisBatch(
            IEnumerable<string> filenames,
            AnalysisOptions options,
            IPfcToolProcessFactory processFactory,
            int maxConcurrency = 0,
            Func<DateTime> clock = null)
        {
            if (filenames == null) throw new ArgumentNullException("filenames");
            if (options == null) throw new ArgumentNullException("options");
            if (processFactory == null) throw new ArgumentNullException("processFactory");

            this.options = options;
            this.processFactory = processFactory;
            this.clock = clock;
            this.maxConcurrency = maxConcurrency > 0 ? maxConcurrency : Math.Max(Environment.ProcessorCount - 1, 1);

            // A file must never be counted twice: the page totals this produces are what
            // customers price their own customers' work from. MainForm already filters a
            // drop, so this is a guard rather than the rule -- silent rather than fatal,
            // because throwing here would crash the application over a condition that has
            // never occurred in practice.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string filename in filenames)
            {
                if (!seen.Add(NormalizeForComparison(filename)))
                    continue;

                var item = new BatchItem(items.Count, filename);
                items.Add(item);
                queue.Enqueue(item);
            }
        }

        private static string NormalizeForComparison(string filename)
        {
            try
            {
                return Path.GetFullPath(filename);
            }
            catch
            {
                // Not a path this process can resolve. Compare it as written rather than
                // discarding it.
                return filename ?? string.Empty;
            }
        }

        /// <summary>Every file this batch will analyze, in order, with duplicates removed.</summary>
        public IReadOnlyList<BatchItem> Items { get { return items; } }

        /// <summary>Files that analyzed successfully. Safe to read once the batch has finished.</summary>
        public IReadOnlyList<TPCFile> Results { get { return results; } }

        /// <summary>Analyzers that failed. Safe to read once the batch has finished.</summary>
        public IReadOnlyList<FileAnalyzer> Failures { get { return failures; } }

        public bool Finished
        {
            get { lock (gate) { return finished; } }
        }

        public void Start()
        {
            Pump();
        }

        /// <summary>
        /// Drains the queue and kills whatever is running. The batch finishes once the
        /// cancelled analyzers have delivered their completion signals.
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.Print("Cancel batch");

            List<FileAnalyzer> toCancel;
            lock (gate)
            {
                cancelled = true;
                queue.Clear();
                toCancel = new List<FileAnalyzer>(running.Keys);
            }

            foreach (var analyzer in toCancel)
                analyzer.Cancel();

            // Nothing was running, so no completion will arrive to finish the batch.
            Pump();
        }

        private void Pump()
        {
            lock (gate)
            {
                if (pumping)
                    return;

                pumping = true;
            }

            while (true)
            {
                BatchItem item = null;
                FileAnalyzer analyzer = null;
                bool raiseFinished = false;

                lock (gate)
                {
                    if (!cancelled && running.Count < maxConcurrency && queue.Count > 0)
                    {
                        item = queue.Dequeue();
                        analyzer = CreateAnalyzer(item);
                        running.Add(analyzer, item);
                    }
                    else if (running.Count == 0 && queue.Count == 0 && !finished)
                    {
                        finished = true;
                        raiseFinished = true;
                    }
                    else
                    {
                        // Either the pool is full, or work is still in flight and its
                        // completion will pump again. Releasing `pumping` under the same
                        // lock as this decision is what makes that safe: a completion
                        // either removes itself from `running` before this check, and so
                        // is seen here, or arrives after `pumping` is false and pumps
                        // itself.
                        pumping = false;
                        return;
                    }
                }

                if (raiseFinished)
                {
                    BatchFinished();
                    lock (gate) { pumping = false; }
                    return;
                }

                FileStarted(item);

                // Go() can raise AnalysisComplete on this very thread, when the process
                // fails to start. That re-enters OnAnalyzerComplete -> Pump, which sees
                // `pumping` and returns; this loop then picks the work up on its next
                // iteration instead of recursing.
                analyzer.Go();
            }
        }

        private FileAnalyzer CreateAnalyzer(BatchItem item)
        {
            var analyzer = new FileAnalyzer(item.Filename, options, processFactory, clock);
            analyzer.ProgressChanged += OnAnalyzerProgress;
            analyzer.AnalysisComplete += OnAnalyzerComplete;
            return analyzer;
        }

        private void OnAnalyzerProgress(FileAnalyzer analyzer, int completed, int total)
        {
            BatchItem item;
            lock (gate)
            {
                if (!running.TryGetValue(analyzer, out item))
                    return;
            }

            FileProgress(item, completed, total);
        }

        private void OnAnalyzerComplete(FileAnalyzer analyzer)
        {
            BatchItem item;
            lock (gate)
            {
                if (!running.TryGetValue(analyzer, out item))
                    return;

                running.Remove(analyzer);

                // Cancelled is checked before Failed on purpose. A cancelled analyzer's
                // process is killed, so it exits non-zero and is marked failed too; it is
                // not a file that failed to analyze.
                if (analyzer.Cancelled)
                {
                    // Neither a result nor a failure.
                }
                else if (analyzer.Failed)
                {
                    failures.Add(analyzer);
                }
                else if (analyzer.Result != null)
                {
                    results.Add(analyzer.Result);
                }
            }

            analyzer.ProgressChanged -= OnAnalyzerProgress;
            analyzer.AnalysisComplete -= OnAnalyzerComplete;

            FileCompleted(item, analyzer);
            Pump();
        }
    }
}
```

- [ ] **Step 5: Run the batch tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter AnalysisBatchTests
```

Expected: PASS, 10 tests.

- [ ] **Step 6: Rewrite ProcessWindow against the batch**

Replace the top of `app/gui/ProcessWindow.cs` — everything from the class declaration down to and including `FinishBatch` and `CancelBatch` — with the following, keeping `GetRow` deleted, `ScrollToFirstProcessingRow` unchanged, and `grid_CellDoubleClick` unchanged:

```csharp
    public partial class ProcessWindow : Form
    {
        public IReadOnlyList<TPCFile> Results { get { return batch.Results; } }

        private readonly AnalysisBatch batch;
        private readonly DataGridViewRow[] rows;
        private bool batchFinished;
        private bool batchCancelled;

        public ProcessWindow()
        {
            InitializeComponent();
        }

        public ProcessWindow(List<string> filenames) : this()
        {
            var options = new AnalysisOptions
            {
                PerformColorAnalysis = Settings.Current.PerformColorAnalysis,
                ColorThreshold = Settings.Current.ColorThreshold,
                CheckImagePixels = Settings.Current.CheckImagePixels
            };

            batch = new AnalysisBatch(filenames, options, new PfcToolProcessFactory());
            rows = new DataGridViewRow[batch.Items.Count];

            batch.FileProgress += Batch_FileProgress;
            batch.FileCompleted += Batch_FileCompleted;
            batch.BatchFinished += Batch_BatchFinished;

            int colIndex = grid.Columns.Add(new DataGridViewProgressColumn());
            grid.Columns[colIndex].Name = "Progress";
            grid.Columns[colIndex].HeaderText = "Progress";
        }

        private void Batch_FileProgress(BatchItem item, int completed, int total)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                var dgvr = rows[item.Index];

                // Posting rather than blocking means a progress update can arrive after
                // the file finished and its row was removed. Nothing to draw in that case.
                if (dgvr.DataGridView == null || total <= 0)
                    return;

                dgvr.Cells["Status"].Value = "Processing";
                dgvr.Cells["Progress"].Value = completed * 100 / total;
            });
        }

        private void Batch_FileCompleted(BatchItem item, FileAnalyzer analyzer)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                var dgvr = rows[item.Index];

                if (analyzer.Cancelled)
                {
                    dgvr.Cells["Status"].Value = "Cancelled";
                }
                else if (analyzer.Failed)
                {
                    string errorMessage = "Failed: " + analyzer.Errors.ToString(0, Math.Min(analyzer.Errors.Length, 255));
                    dgvr.Cells["Status"].Value = errorMessage;
                    dgvr.DefaultCellStyle.BackColor = Color.DarkRed;
                    dgvr.DefaultCellStyle.ForeColor = Color.White;
                    dgvr.DefaultCellStyle.SelectionBackColor = Color.Red;
                    dgvr.DefaultCellStyle.SelectionForeColor = Color.White;
                }
                else
                {
                    System.Diagnostics.Debug.Assert(analyzer.Result != null);
                    grid.Rows.Remove(dgvr);
                }

                ScrollToFirstProcessingRow();
            });
        }

        private void Batch_BatchFinished()
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                batchFinished = true;

                if (batchCancelled || batch.Failures.Count == 0)
                {
                    Close();
                }
                else
                {
                    Text = "Processing finished with errors";
                }
            });
        }

        private void ProcessWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!batchFinished)
            {
                e.Cancel = true;
                batchCancelled = true;
                batch.Cancel();
            }
        }

        private void ProcessWindow_Load(object sender, EventArgs e)
        {
            foreach (var item in batch.Items)
            {
                string fileSize;
                long fileLength;
                if (Utility.TryGetFileLength(item.Filename, out fileLength))
                {
                    fileSize = Utility.BytesToString(fileLength);
                }
                else
                {
                    fileSize = "Unknown";
                }

                string folder = System.IO.Path.GetDirectoryName(item.Filename);
                string fileName = System.IO.Path.GetFileName(item.Filename);
                string extension = System.IO.Path.GetExtension(item.Filename);
                int rowIndex = grid.Rows.Add(folder, fileName, extension, fileSize, "Queued", 0);
                grid.Rows[rowIndex].Tag = item.Filename;
                rows[item.Index] = grid.Rows[rowIndex];
            }

            batch.Start();
        }
```

Two behaviours moved rather than changed:

- The `"Processing"` status was set in `NextFile` before the analyzer started. It now goes on the first progress update, because the batch starts analyzers off the UI thread. If you would rather keep the exact original moment, subscribe `batch.FileStarted` and set it there — do that, since it preserves the display for a file that produces no progress at all.
- `completedFiles` and `failedFiles` are gone; `batch.Results` and `batch.Failures` replace them.

Add the `FileStarted` subscription in the constructor and the handler:

```csharp
            batch.FileStarted += Batch_FileStarted;
```

```csharp
        private void Batch_FileStarted(BatchItem item)
        {
            UiThread.BeginInvokeIfRequired(this, () =>
            {
                rows[item.Index].Cells["Status"].Value = "Processing";
            });
        }
```

and remove the `dgvr.Cells["Status"].Value = "Processing";` line from `Batch_FileProgress`.

Ensure `using System.Collections.Generic;` is present. Remove `using System.Data;`, `using System.Linq;` and `using System.Text;` only if the compiler reports them unused — `ScrollToFirstProcessingRow` uses `Linq`.

- [ ] **Step 7: Update MainForm for the changed Results type**

In `app/gui/MainForm.cs`, change:

```csharp
            List<TPCFile> processedFiles;
```

to:

```csharp
            IReadOnlyList<TPCFile> processedFiles;
```

`AcceptedFiles.AddRange(processedFiles)` still compiles: `List<T>.AddRange` takes an `IEnumerable<T>`.

- [ ] **Step 8: Run the whole suite and build Release**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add -A app/core app/gui app/tests
git commit -F - <<'EOF'
Lift the worker pool out of ProcessWindow

The pool has real invariants -- refill on completion, never exceed the process
cap, finish only when the queue and the running set are both empty, cancel
drains both -- and none of them need a window. They were untestable purely
because they lived in a Form, and were thread-safe only because every mutation
was funnelled onto the UI thread.

AnalysisBatch guards its own state with a lock and raises events on whatever
thread completed the work. ProcessWindow marshals in its handlers, which
preserves the property that matters: analyzer callbacks must never block a
thread pool thread waiting on the UI thread, because the UI thread does real
work per completed file and those pool threads are what the stream readers need
in order to deliver end-of-file.

The pump has an explicit re-entrancy guard rather than relying on the caller to
post. Go() raises AnalysisComplete on the calling thread when the process fails
to start, so pumping from the completion handler would nest one frame set per
queued file; a missing pfc-tool.exe and a few hundred dropped files would
overflow the stack, which .NET cannot catch. There is a test for 1000 of them.

Two behaviour changes, both stated deliberately:

  - An empty batch now finishes. ProcessWindow never did: NextFile did nothing,
    no analyzer completed, and FinishBatch was unreachable. It stayed latent
    only because MainForm guards against an empty drop.
  - A path repeated within one batch is analyzed once. MainForm already filters
    a drop, so this is a guard rather than the rule, but the page totals feed
    customer pricing and double-counting one file is not a cosmetic defect.

Events carry a BatchItem rather than a Tag, so the grid row lookup no longer
depends on the filenames in a batch happening to be distinct.

EOF
```

---

### Task 7: Integration tests against the real pfc-tool.exe

**Files:**
- Create: `app/tests/RepoLayout.cs`, `app/tests/PfcToolFactAttribute.cs`, `app/tests/PfcToolIntegrationTests.cs`

**Interfaces:**
- Consumes: `FileAnalyzer`, `AnalysisOptions`, `PfcToolProcessFactory` from Task 5.
- Produces: `RepoLayout.PfcToolPath` (string, null when unbuilt), `RepoLayout.TestFilesDirectory` (string, null when absent), `PfcToolFactAttribute`.

- [ ] **Step 1: Write the locator**

Create `app/tests/RepoLayout.cs`:

```csharp
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
```

- [ ] **Step 2: Write the skipping fact attribute**

Create `app/tests/PfcToolFactAttribute.cs`:

```csharp
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
```

- [ ] **Step 3: Write the integration tests**

Create `app/tests/PfcToolIntegrationTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// Runs the real pfc-tool.exe and asserts protocol conformance only.
    /// <para>
    /// Deliberately nothing about colour classification: the pfc-regression skill
    /// verifies that properly, against a shipped baseline over a corpus of real files.
    /// Repeating it here would produce a test that fails for reasons unrelated to the C#
    /// code -- a mupdf version bump, a patch refresh -- and that is not what this suite
    /// is for.
    /// </para>
    /// </summary>
    public class PfcToolIntegrationTests
    {
        private readonly ITestOutputHelper output;

        public PfcToolIntegrationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static AnalysisOptions Options()
        {
            return new AnalysisOptions
            {
                ToolPath = RepoLayout.PfcToolPath,
                PerformColorAnalysis = true,
                ColorThreshold = 0.25m,
                CheckImagePixels = true
            };
        }

        /// <summary>
        /// Runs one file to completion. FileAnalyzer has no timeout by design; a test
        /// host needs one so that a hang is a failing test rather than a hung suite.
        /// </summary>
        private static FileAnalyzer Analyze(string path)
        {
            var analyzer = new FileAnalyzer(path, Options(), new PfcToolProcessFactory());
            using (var done = new ManualResetEventSlim(false))
            {
                analyzer.AnalysisComplete += a => done.Set();
                analyzer.Go();

                Assert.True(done.Wait(TimeSpan.FromSeconds(120)),
                    "Analysis of " + path + " did not complete within 120 seconds.");
            }

            return analyzer;
        }

        [PfcToolFact]
        public void EverySamplePdfConformsToTheProtocol()
        {
            string[] files = Directory.GetFiles(RepoLayout.TestFilesDirectory, "*.pdf");
            Assert.NotEmpty(files);

            foreach (string file in files)
            {
                output.WriteLine("Analyzing " + Path.GetFileName(file));

                var analyzer = Analyze(file);

                Assert.False(analyzer.Cancelled, file);
                Assert.False(analyzer.Failed, file + " failed: " + analyzer.Errors);
                Assert.NotNull(analyzer.Result);
                Assert.True(analyzer.Result.PageCount > 0, file + " reported no pages.");

                // The count in the header and the number of Page= lines must agree.
                Assert.Equal(analyzer.Result.PageCount, analyzer.Result.Pages.Count);

                foreach (var page in analyzer.Result.Pages)
                {
                    Assert.True(page.Width > 0m, file + " page " + page.PageNumber + " has width " + page.Width);
                    Assert.True(page.Height > 0m, file + " page " + page.PageNumber + " has height " + page.Height);
                }
            }
        }

        [PfcToolFact]
        public void AFileThatDoesNotExistFailsCleanly()
        {
            string missing = Path.Combine(RepoLayout.TestFilesDirectory, "no-such-file-" + Guid.NewGuid().ToString("N") + ".pdf");

            var analyzer = Analyze(missing);

            Assert.True(analyzer.Failed);
            Assert.NotEqual(string.Empty, analyzer.Errors.ToString().Trim());
        }

        [PfcToolFact]
        public void ABatchOfEverySampleFileFinishes()
        {
            string[] files = Directory.GetFiles(RepoLayout.TestFilesDirectory, "*.pdf");

            var batch = new AnalysisBatch(files, Options(), new PfcToolProcessFactory(), maxConcurrency: 2);

            using (var done = new ManualResetEventSlim(false))
            {
                batch.BatchFinished += () => done.Set();
                batch.Start();

                Assert.True(done.Wait(TimeSpan.FromSeconds(180)), "The batch did not finish within 180 seconds.");
            }

            Assert.Equal(files.Length, batch.Results.Count + batch.Failures.Count);
            Assert.Empty(batch.Failures);
        }
    }
}
```

- [ ] **Step 4: Verify the tests skip when the tool is absent**

Confirm whether the native tool is present:

```bash
ls app/x64/Release/pfc-tool.exe app/x64/Debug/pfc-tool.exe 2>/dev/null || echo "not built"
```

If it is **not** built, run `dotnet test app/ProFileCounter.sln` and confirm the three integration tests report as **skipped**, not failed, with the skip reason naming the msbuild command.

If it **is** built, temporarily rename it, run `dotnet test`, confirm the skips, then rename it back.

Expected: 3 skipped, everything else passing.

- [ ] **Step 5: Verify the tests pass when the tool is present**

Ensure the native tool is built:

```bash
msbuild app/pfc-tool/pfc-tool.sln /p:Configuration=Release /p:Platform=x64
```

(Use `.\build.ps1 -Configuration Release` if msbuild is not on `PATH`.) Then run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS, with the three integration tests now running rather than skipped.

If the native build is not available in this environment, record that fact and report the integration tests as verified-skipping only. Do not claim they pass without having seen them pass.

- [ ] **Step 6: Commit**

```bash
git add -A app/tests
git commit -F - <<'EOF'
Add opt-in integration tests against the real pfc-tool.exe

The hermetic tests prove the C# side handles the protocol; they cannot prove the
protocol is still the one the tool speaks. These run the real analyzer over the
sample files and check that the two halves still agree: analysis succeeds, the
page count is non-zero, the number of Page= lines matches the count in the
header, and every size parses to something positive.

They assert nothing about colour classification. The pfc-regression skill
already verifies that against a shipped baseline over a corpus of real files;
duplicating it here would produce a test that fails for a mupdf bump or a patch
refresh, which is not what this suite is for.

Building the native tool needs Visual Studio with the v142 toolset and takes
minutes, so `dotnet test` after a C# change must not depend on it. A Fact
attribute that sets Skip in its constructor reports these as skipped rather than
failed when the tool is not there, and names the msbuild command in the reason.

EOF
```

---

### Task 8: Update the installer and CLAUDE.md

**Files:**
- Modify: `installer/script.iss:41`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything.
- Produces: nothing.

- [ ] **Step 1: Add the core DLL to the installer**

In `installer/script.iss`, immediately after the `Newtonsoft.Json.dll` line (line 41), add:

```
Source: "..\app\x64\Release\ProFileCounter.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
```

The analysis pipeline lives in this assembly; without it the application will not start.

- [ ] **Step 2: Rewrite the pipeline section of CLAUDE.md**

Replace the whole "## Processing pipeline (C# side)" section with:

```markdown
## Processing pipeline (C# side)

The managed side is two assemblies. `app/gui` is the WinForms application;
`app/core` (`ProFileCounter.Core.dll`) holds the whole analysis pipeline with no UI
dependency, so it can be tested without building a Form. **Both use the
`TIFPDFCounter` namespace** — namespaces span assemblies, so moving a type between them
changes no `using` statement. Only duplicate *type names* would collide, which is why
the GUI's WinForms thread helpers are `UiThread` and only the non-UI half kept the name
`Utility`.

1. `MainForm` handles drag-drop, recursively expands dropped folders (`DiscoverFiles`),
   filters out files already summarized, and opens a `ProcessWindow` with the result.
2. `ProcessWindow` (`app/gui`) is now UI only. It builds `AnalysisOptions` from
   `Settings.Current`, hands the file list to an `AnalysisBatch`, and paints what the
   batch reports. Every batch event is marshalled with `UiThread.BeginInvokeIfRequired`
   — never the blocking `Invoke`; see the comment on that method for the starvation it
   prevents.
3. `AnalysisBatch` (`app/core`) runs the bounded worker pool: at most
   `ProcessorCount - 1` analyzers at once, refilled as each completes, finishing only
   when the queue and the running set are both empty. It guards its own state with a
   lock and raises events on whatever thread completed the work. Its pump has an
   explicit re-entrancy guard, because `FileAnalyzer.Go()` raises `AnalysisComplete`
   synchronously when the process fails to start. Events carry a `BatchItem` (index plus
   filename), which is how the GUI maps a result back to a grid row. It also collapses
   duplicate paths, so no file can be counted twice in the totals.
4. `FileAnalyzer` (`app/core`) owns one `pfc-tool.exe` child through the
   `IPfcToolProcess` seam, accumulates a `TPCFile` from the parsed lines, and completes
   only once all three signals — stdout EOF, stderr EOF and process exit — have arrived.
   There is deliberately no timeout; see the comments in the class. The real seam
   implementation is `PfcToolProcess`; tests substitute a fake.
5. `PfcToolProtocol` (`app/core`) is the pure half: it formats the command line and
   parses one line of stdout, with no state and no process. The culture-invariance and
   the unsigned `Size=` pattern both live here.
6. Completed `TPCFile` results feed `PageSizeCounter` (`app/core`), which buckets pages
   into user-defined `PageSize` ranges (`IsMatch` checks both orientations) plus
   colour/BW/unknown counts, for the summary grid in `MainForm`.

## Tests

```
dotnet test app\ProFileCounter.sln
```

**Run this after changing any C# code.** It builds `app/core` and `app/tests` only — not
the WinForms project, and not the native tool. The tests are in `app/tests`
(xUnit v2, net48) and reference `app/core` alone, which is what keeps them fast and
keeps `dotnet test` off the WinForms build.

Most are hermetic: a fake `IPfcToolProcess` drives the analyzer's three completion
signals in every order, a fake factory drives the batch's pool invariants, and the
protocol parser is tested as a pure function under a comma-decimal culture.

A handful of integration tests run the real `pfc-tool.exe` over `test files/`. They find
it by walking up to `app\x64\{Release,Debug}\` and **report as skipped, not failed, when
it has not been built** — so a C# change does not require the multi-minute native build.
They assert protocol conformance only. Colour classification is verified by the
`pfc-regression` skill against a shipped baseline; do not duplicate it here.

Neither new project sets `PlatformTarget`. `dotnet test` on net48 may host the tests at
x86, and an x64-marked `ProFileCounter.Core.dll` would fail to load. The GUI stays x64.

`app/tests` deliberately keeps the default `bin\` output rather than the
`app\x64\$(Configuration)\` the other two projects share: the installer packages from
that directory, and the xunit and testhost assemblies must never land in it.
```

- [ ] **Step 3: Update the two other CLAUDE.md passages that are now wrong**

In the **Build** section, the sentence listing what lands in `app\x64\Release\` must
include the new assembly. Change:

> containing `ProFile Counter.exe`, `ProFile Counter.exe.config`, `pfc-tool.exe`, `Newtonsoft.Json.dll`, and `System.Resources.Extensions.dll` plus its dependency closure

to:

> containing `ProFile Counter.exe`, `ProFile Counter.exe.config`, `pfc-tool.exe`, `ProFileCounter.Core.dll`, `Newtonsoft.Json.dll`, and `System.Resources.Extensions.dll` plus its dependency closure

Also change the sentence "There are two solutions, and no project belongs to both" —
the managed solution now holds three projects. Update the `app/ProFileCounter.sln`
bullet to read:

> - **`app/ProFileCounter.sln`** — managed only: the WinForms GUI, the `ProFileCounter.Core` class library it depends on, and the xUnit test project. `dotnet build app\ProFileCounter.sln -c Release` is the normal C# build; `dotnet test app\ProFileCounter.sln` is what to run after changing C# code.

And replace the line "There is no automated test suite in this repo." with:

> The managed side has an automated test suite; see **Tests** below. The native tool does not — it is covered by the `pfc-regression` skill against a shipped baseline.

- [ ] **Step 4: Verify the installer file list matches the build output**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Release && ls app/x64/Release/
```

Then confirm every DLL and EXE listed in the output has a `Source:` line in
`installer/script.iss` (ignore `.pdb` files, which Release does not produce).

- [ ] **Step 5: Commit**

```bash
git add installer/script.iss CLAUDE.md
git commit -F - <<'EOF'
Document the split pipeline and ship the core assembly

ProFileCounter.Core.dll holds the analysis pipeline, so the application does not
start without it. The installer needs the Source: line for the same reason it
needed one for the config file.

CLAUDE.md's pipeline section described a four-step flow through ProcessWindow
and FileAnalyzer that no longer exists, and the repo is no longer without an
automated test suite. Both are now the first thing a reader would act on
wrongly.

The Tests section says what `dotnet test` does and does not build, because the
native half needs Visual Studio with the v142 toolset and takes minutes: a C#
change must not drag that in. It also records why app/tests keeps the default
bin\ output -- the installer packages from app\x64\Release\ -- and why neither
new project sets PlatformTarget.

EOF
```

---

## Final verification

Run after Task 8, before opening the PR. These are the checks that matter most: a green
test suite proves the new code works, not that the application still does.

- [ ] **Automated**

```bash
dotnet test app/ProFileCounter.sln
```
Expected: all pass; integration tests skipped or passing depending on the native build.

```bash
dotnet build app/ProFileCounter.sln -c Release
```
Expected: `0 Warning(s)`, `0 Error(s)`.

```bash
.\build.ps1 -Configuration Release
```
Expected: both solutions build, output in `app\x64\Release\`.

- [ ] **Settings survival** — back up `%LocalAppData%\ProFile Counter\UserSettings.xml`
  first. Launch `app\x64\Release\ProFile Counter.exe`. Confirm **no** "Default settings
  are loaded." dialog appears, and that the configured page sizes are intact under
  Settings. `PageSize` moved assemblies; `Settings.Load` fails closed on any unknown
  element or attribute, so this is the check that the move was inert in practice.

- [ ] **End-to-end run** — drag several files from `test files\` onto the main window at
  once, so the worker pool is genuinely exercised rather than running one file at a
  time. Confirm: progress bars advance; completed files leave the grid; failures show in
  red with their error text; the summary grid totals appear; the title-bar icon and the
  settings window's panel background still render (the preserialized-resource path).

- [ ] **Cancellation** — start a batch and close the process window mid-run. Confirm it
  closes rather than hanging.

- [ ] **Open the PR** into `develop`. Do not push without being asked.

## Self-review notes

Spec coverage checked against `specs/2026-08-29-testable-analysis-pipeline-design.md`:
every section maps to a task. Projects and constraints → Task 1. Moved types and the
`Utility`/`UiThread` split → Task 2. `FileAnalyzer` move → Task 3. `PfcToolProtocol` and
`AnalysisOptions` → Task 4. Process seam and the exit-code fix → Task 5. `AnalysisBatch`,
threading, re-entrancy, duplicate guard, and the GUI rewire → Task 6. Integration tests
and the skip attribute → Task 7. Installer and CLAUDE.md → Task 8. The three deliberate
behaviour changes appear in the commit messages for Tasks 5 and 6. The known limitation
(cross-drop case-sensitive comparison in `MainForm`) is recorded in the spec and
deliberately has no task.
