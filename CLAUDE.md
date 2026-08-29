# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ProFile Counter (internal namespace `TIFPDFCounter`) is a Windows desktop app for batch-analyzing PDF files: page count, page dimensions (mapped to named paper sizes like ANSI-A/ARCH-D), color vs. black-and-white classification, bookmark count, and duplicate detection via MD5. Users drag-and-drop files/folders onto the main window and get a grid report.

It is two separate projects glued together by a subprocess boundary:

- **`app/gui`** — a C# WinForms app (`ProFile Counter.csproj`, .NET Framework 4.8, target `TIFPDFCounter` namespace). This is the UI and orchestration layer.
- **`app/pfc-tool`** — a native C/C++ console EXE (`pfc-tool.vcxproj`, producing `pfc-tool.exe`) that links against MuPDF, built from source out of the `app/pfc-tool/mupdf` git submodule (pinned to tag `1.28.2`, vendored from [ArtifexSoftware/mupdf](https://github.com/ArtifexSoftware/mupdf)). It does the actual PDF parsing/rendering and prints results as plain text to stdout. The project and its output are deliberately *not* called `mupdf` — the submodule ships its own `platform/win32/mupdf.vcxproj` viewer, and sharing the name made the two easy to confuse.

The C# app never links against MuPDF directly — it shells out to `pfc-tool.exe` as a child process per file and parses its stdout line-by-line. Understanding that protocol is essential before touching either side.

## Build

The two halves have different prerequisites. The C# app needs only the .NET SDK: its net48 reference assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies` package, so no Visual Studio targeting pack is required. `pfc-tool` needs Visual Studio 2019+ (or matching MSBuild) with the C++ desktop workload.

`pfc-tool.vcxproj` pins `PlatformToolset` to v142, matching the mupdf submodule, which hardcodes v142 in all 349 `<PlatformToolset>` entries across its 32 `platform/win32` project files. Artifex's own docs (`docs/reference/cxx-and-derived-bindings.rst`) tell you to install the v142 tools rather than retarget. On a newer Visual Studio that toolset is not present by default and MSBuild fails with **MSB8020**; add the individual component **"MSVC v142 - VS 2019 C++ x64/x86 build tools (v14.29-16.11)"** (VS 2026 still offers it — do not use "Remove out-of-support components", which strips it back out). The ARM, Spectre, ATL/MFC and C++/CLI v142 variants are not needed.

A newer toolset does work if you ever need one: pass `/p:PlatformToolset=`, which the `PreBuildEvent` forwards to the nested mupdf build so `libmupdf.lib` and `pfc-tool.exe` are never compiled with mismatched toolsets. Prefer installing v142 — it keeps a bare `msbuild app\pfc-tool\pfc-tool.sln` working and matches what upstream tests.

`pfc-tool.exe` links the **static** CRT (`/MT`, `/MTd`). It ships as a bare executable next to the GUI with no redistributable, so it must not depend on `MSVCP140.dll`/`VCRUNTIME140.dll`. mupdf's own projects hardcode `MultiThreadedDLL`, so the `PreBuildEvent` injects `static-crt.props` into the nested build via `/p:ForceImportAfterCppTargets` to override them without editing the submodule. If you change the runtime in `pfc-tool.vcxproj`, change `static-crt.props` to match or the link will fail on CRT mismatch. Verify with `dumpbin /dependents app\x64\Release\pfc-tool.exe` — `KERNEL32.dll` should be the only entry.

Clone with submodules, or run `git submodule update --init --recursive` after cloning — `app/pfc-tool/mupdf` and its own nested thirdparty submodules (freetype, harfbuzz, tesseract, etc.) must be checked out before building.

There are two solutions, and no project belongs to both:

- **`app/ProFileCounter.sln`** — managed only. `dotnet build app\ProFileCounter.sln -c Release` is the normal C# build, and the one to use after changing C# code.
- **`app/pfc-tool/pfc-tool.sln`** — native only. `msbuild app\pfc-tool\pfc-tool.sln /p:Configuration=Release /p:Platform=x64` builds the analyzer, and is only needed after editing `main.c`, bumping the mupdf tag, or refreshing a patch.

They are separate because the dotnet CLI cannot build C++ projects: the C++ targets are .NET Framework assemblies that MSBuild-on-.NET cannot load, and no flag changes that. While the vcxproj shared a solution with the GUI, `dotnet build` (and later `dotnet test`) could never run against the managed side.

**Both solutions deliberately output to the same `app\x64\$(Configuration)\`**, because `FileAnalyzer` launches `pfc-tool.exe` as a bare relative filename from the GUI's working directory, so the two executables have to sit side by side. Each project derives that directory from `$(ProjectDir)`, never `$(SolutionDir)`, so no solution file is load-bearing: either solution can be moved or renamed without changing what gets built or where it lands. Keep any new `OutDir`/`OutputPath` `$(ProjectDir)`-relative — a `$(SolutionDir)`-relative path relocates output silently rather than failing, and the stale `pfc-tool.exe` left behind in `app\x64\Release\` is what the GUI would then launch and the installer would package.

Building `pfc-tool` triggers a `PreBuildEvent` that applies the local mupdf patches (see below) and then invokes the submodule's own `mupdf/platform/win32/mupdf.sln` to build `libmupdf` from source (matching `$(Configuration)`/`$(Platform)`), after which `pfc-tool` links against the resulting `libmupdf.lib`. This nested-build approach (rather than folding mupdf's project graph directly into `pfc-tool.sln`) exists because mupdf's own build scripts (e.g. `bin2coff`'s font-embedding step) hardcode paths relative to `$(SolutionDir)` assuming `mupdf.sln` itself is the entry point — nesting the invocation keeps those assumptions intact instead of relying on `ProjectReference`-based inclusion.

## Local mupdf patches

`app/pfc-tool/patches/` holds fixes carried against the pinned mupdf tag. The submodule is a pristine upstream checkout, so edits made inside it are untracked here and are wiped by the next `git submodule update` — keeping them as patch files means they are version controlled and reapplied on every build. `apply-patches.cmd` (run from the `pfc-tool` `PreBuildEvent`) applies every `*.patch` in that directory to the submodule. It is idempotent: an already-applied patch is skipped, and a patch that no longer applies **fails the build** rather than silently producing an unpatched binary — which is the signal to refresh the patch after bumping the mupdf tag. It requires `git` on `PATH`.

These patches are deliberately not submitted upstream. Currently: `0001-test-device-colour-sampling.patch` fixes three defects in mupdf's test device (the device `FileAnalyzer` relies on for colour detection). Two made it sample uninitialised memory, so the same file could be classified black-and-white or colour at random between runs — most importantly, both image-sampling call sites passed the `fz_color_converter` arguments in `(dst, src)` order when the declared order is `(src, dst)`. The third made it sample *undecoded* data: the compressed fast path in `fz_test_fill_image()` assumed `fz_open_compressed_buffer()` returns samples, but the "full image formats" (JPX, PNG, TIFF, BMP, GIF, PNM, PSD, JXR) fall through the switch in `fz_open_image_decomp_stream()` and come back still encoded. A JPX image therefore tripped colour detection on the ASCII `jP` of its JP2 signature box at the second pixel, at any threshold, before anything was decoded. The fast path is now restricted to compressions that genuinely decode to samples; the rest go through `fz_get_pixmap_from_image()`.

`build.ps1` builds both, native first, and is what to use before cutting a release — the split removed the ordering guarantee a single-solution build used to give, that a fresh `pfc-tool.exe` ends up beside a fresh GUI:

```
.\build.ps1 -Configuration Release
```

Or each half on its own:

```
dotnet build app\ProFileCounter.sln -c Release
msbuild app\pfc-tool\pfc-tool.sln /p:Configuration=Release /p:Platform=x64
```

The managed build needs no `Platform` argument: the GUI project is x64 and writes to `app\x64\$(Configuration)\` unconditionally.

Output lands in `app\x64\Release\` (or `app\x64\Debug\`) containing `ProFile Counter.exe`, `ProFile Counter.exe.config`, `pfc-tool.exe`, `Newtonsoft.Json.dll`, and `System.Resources.Extensions.dll` plus its dependency closure (`System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Vectors.dll`, `System.Runtime.CompilerServices.Unsafe.dll`) side by side — `ProFile Counter.exe` expects `pfc-tool.exe` in its own working directory (see `FileAnalyzer.cs`, which invokes `pfc-tool.exe` as a bare relative filename). `libmupdf.lib` itself lands under `app\pfc-tool\mupdf\platform\win32\x64\Release\`, per the submodule's own build layout.

### Why System.Resources.Extensions ships

`MainForm.resx` (`$this.Icon`) and `SettingsWindow.resx` (`panel1.BackgroundImage`) each hold a `System.Drawing` object as `bytearray.base64`. The `GenerateResource` task running on .NET cannot instantiate those types in order to round-trip them, so `dotnet build` fails with **MSB3822**/**MSB3823**. The project sets `GenerateResourceUsePreserializedResources`, which copies the base64 through untouched instead — at the cost of making `System.Resources.Extensions` a *runtime* dependency, because those two `.resources` files now name `DeserializingResourceReader` in their header. The other five are unaffected and are byte-identical to what the pre-SDK build produced.

Two consequences worth knowing before touching any of this:

- **`ProFile Counter.exe.config` is now required at runtime, not merely nice to have.** The `.resources` headers name `System.Resources.Extensions, Version=4.0.0.0` while the shipped assembly is `8.0.0.0`, and the binding redirect reconciling them lives in that config, which the SDK generates from `app.config`. Without it `MainForm.InitializeComponent` throws `FileLoadException` reading its icon, so the app does not start at all. The installer ships it for exactly this reason; before the SDK conversion it did not need to.
- **The package is pinned to 8.0.0 deliberately.** From 9.0.0 it depends on the split-out NRBF reader and drags `System.Formats.Nrbf`, `System.Reflection.Metadata`, `System.Collections.Immutable` and `Microsoft.Bcl.HashCode` along too — ten files beside the exe instead of five. Nothing here needs `BinaryFormatter` payloads; both resources are TypeConverter-based.

A successful build proves none of this. After changing the resource pipeline, the package version or the installer's file list, launch the app and confirm the main window's title-bar icon and the settings window's panel background still render.

There is no automated test suite in this repo.

## What pfc-tool will and won't open

`main.c` registers `pdf_document_handler` and `img_document_handler` individually instead of calling `fz_register_document_handlers()`. Only formats whose page count and page dimensions are properties of the file are counted — PDF, and TIFF/JPEG/PNG/BMP via the image handler.

Everything else mupdf can open is deliberately excluded. The reflowable formats (txt, html, xhtml, md, epub, mobi, fb2) and the Office formats have no intrinsic pagination: mupdf converts them to HTML and flows the result onto `FZ_DEFAULT_LAYOUT_W/H`, a 420x595pt A5 canvas. Measured before the handlers were dropped, a plain `.txt` reported 11 pages, a one-line `.html` reported 1, a spreadsheet reported 4, and a landscape PowerPoint deck reported 2 — every one of them A5 portrait. Those numbers describe mupdf's default layout rather than the document, and they would land in `PageSizeCounter` as 5.83x8.27in, a size matching no ANSI or ARCH bucket. `cbz` is excluded for the same reason: it turns an archive's image entries into "pages".

Dropping handlers is not sufficient on its own. mupdf's PDF repair scans a file for a `%PDF` marker and rebuilds a document from whatever it finds, so a `.zip` full of PDFs was reported as a document carrying the page count of the first one inside — and `pdf_document_handler` obviously cannot be dropped. `archive_kind()` therefore reads the first 262 bytes (enough to reach TAR's `ustar` magic at offset 257) and refuses ZIP, 7-Zip, RAR, gzip, bzip2, xz and TAR containers before `fz_open_document` sees the file. Recognition is by magic number, not extension, so a renamed archive is still caught. The ZIP signature also covers `.docx`/`.xlsx`/`.pptx`/`.epub`, which are ZIP containers.

This is a blocklist of container formats rather than an allowlist of known-good headers, and deliberately so: genuinely damaged PDFs may carry garbage before their `%PDF` marker, and mupdf repairs them successfully. `test files/` and the wider problem-file corpus depend on that repair path continuing to work.

## The pfc-tool.exe stdout protocol

Defined by `app/pfc-tool/main.c` on the producer side and parsed by `app/gui/FileAnalyzer.cs` (regex-matched) on the consumer side. If you change one, update the other:

```
pfc-tool.exe "<filename>" <colorThreshold|-1> <checkPixels 0|1>
```

stdout (unbuffered, flushed per line so the C# side can stream progress):
```
PageCount=<n> BookmarkCount=<n>
Page=<pageNum> Size=<widthPt>,<heightPt> Color=<-1|0|1|2>
Page=<pageNum> Size=<widthPt>,<heightPt> Color=<-1|0|1|2>
...
```
`Size` is in PDF points; `FileAnalyzer` converts to inches (÷72) before building a `TPCFilePage`. `Color` is `-1` (unknown/analysis skipped or failed), `0` (BW), `1` or `2` (color) — mapped to the `ColorMode` enum. A non-zero exit code, a missing/garbled line, a page count of zero, or a page count that doesn't match the number of `Page=` lines received are all treated as failures (`FileAnalyzer.Fail`).

## Processing pipeline (C# side)

1. `MainForm` handles drag-drop, recursively expands dropped folders (`DiscoverFiles`), and opens a `ProcessWindow` with the resulting file list.
2. `ProcessWindow` runs a bounded worker pool (`maxProcesses = ProcessorCount - 1`) over a `Queue<string>` of file paths, spawning one `FileAnalyzer` (i.e. one `pfc-tool.exe` process) per in-flight file and refilling the pool as each completes (`NextFile`/`Analyzer_AnalysisComplete`). All UI mutation from analyzer callbacks goes through `Utility.InvokeIfRequired` since `Process` events fire on background threads.
3. Each `FileAnalyzer` owns one `pfc-tool.exe` child process, streams stdout via `OutputDataReceived`, and raises `ProgressChanged`/`AnalysisComplete` events; a `TPCFile` (`File.cs`) accumulates `TPCFilePage` (`FilePage.cs`) entries as they arrive.
4. Completed `TPCFile` results feed `PageSizeCounter` (`PageSizeCounter.cs`), which buckets pages into user-defined `PageSize` ranges (`PageSize.cs`, `IsMatch` checks both width×height orientations) plus color/BW/unknown counts, for the summary grid in `MainForm`.

## Settings

`Settings.cs` is a static singleton loading/saving `UserSettings` as XML to `%LocalAppData%\ProFile Counter\UserSettings.xml`. It fails closed: any deserialization error (including unknown XML elements/attributes, which are treated as hard errors) falls back to `DefaultUserSettings`, which also defines the hard-coded standard paper sizes (ANSI/ARCH series). `PageSizeManager`/`PageSizeEditor` forms let users edit the page size list at runtime.

## Other components

- `docs/` is the GitHub Pages update-check + download site (`index.html`, `latest_version.json`) — `Settings.DefaultUserSettings.AppUpdateJsonUrl` points at it, and `MainForm` polls it on startup when `CheckForProgramUpdates` is set.
- `installer/script.iss` is an Inno Setup script that packages the Release|x64 output (`ProFile Counter.exe`, `pfc-tool.exe`, `Newtonsoft.Json.dll`) into the distributable installer. Bump `MyAppVersion` there together with `AssemblyVersion` in `app/gui/Properties/AssemblyInfo.cs` when cutting a release. `AssemblyVersion` is the real product version; the csproj no longer carries one, its old `ApplicationVersion` having been ClickOnce leftover that never matched.
- **`docs/latest_version.json` is bumped last, and only once the release is genuinely published** — the installer built, uploaded, and linked for download from `docs/index.html`. That file is what triggers the update prompt in every running copy (`MainForm.CheckForProgramUpdates` compares it against `Application.ProductVersion`), so raising it before there is something to download points users at a release that does not exist. An `AssemblyInfo.cs`/installer/`index.html` version ahead of `latest_version.json` is the normal in-development state, **not** a bug to fix.
- `test files/` contains sample PDFs (including a known mupdf color-detection edge case, per the filename) used for manual testing — there is no automated harness driving them.
