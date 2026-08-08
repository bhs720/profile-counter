# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ProFile Counter (internal namespace `TIFPDFCounter`) is a Windows desktop app for batch-analyzing PDF files: page count, page dimensions (mapped to named paper sizes like ANSI-A/ARCH-D), color vs. black-and-white classification, bookmark count, and duplicate detection via MD5. Users drag-and-drop files/folders onto the main window and get a grid report.

It is two separate projects glued together by a subprocess boundary:

- **`app/gui`** — a C# WinForms app (`ProFile Counter.csproj`, .NET Framework 4.8, target `TIFPDFCounter` namespace). This is the UI and orchestration layer.
- **`app/pfc-tool`** — a native C/C++ console EXE (`mupdf.vcxproj`) that links against MuPDF, built from source out of the `app/pfc-tool/mupdf` git submodule (pinned to tag `1.28.2`, vendored from [ArtifexSoftware/mupdf](https://github.com/ArtifexSoftware/mupdf)). It does the actual PDF parsing/rendering and prints results as plain text to stdout.

The C# app never links against MuPDF directly — it shells out to `mupdf.exe` as a child process per file and parses its stdout line-by-line. Understanding that protocol is essential before touching either side.

## Build

Requires Visual Studio 2019+ (or matching MSBuild) with the C++ desktop workload (`PlatformToolset` v142, matching what the mupdf submodule's own `platform/win32` projects target) and .NET Framework 4.8 targeting pack.

Clone with submodules, or run `git submodule update --init --recursive` after cloning — `app/pfc-tool/mupdf` and its own nested thirdparty submodules (freetype, harfbuzz, tesseract, etc.) must be checked out before building.

Open `app/app.sln` — it contains the C# app and the native `pfc-tool` project, with the app project depending on `pfc-tool`. Building `pfc-tool` triggers a `PreBuildEvent` that invokes the submodule's own `mupdf/platform/win32/mupdf.sln` to build `libmupdf` from source (matching `$(Configuration)`/`$(Platform)`), then `pfc-tool` links against the resulting `libmupdf.lib`. This nested-build approach (rather than folding mupdf's project graph directly into `app.sln`) exists because mupdf's own build scripts (e.g. `bin2coff`'s font-embedding step) hardcode paths relative to `$(SolutionDir)` assuming `mupdf.sln` itself is the entry point — nesting the invocation keeps those assumptions intact instead of relying on `ProjectReference`-based inclusion.

```
msbuild app\app.sln /p:Configuration=Release /p:Platform=x64
```

Output lands in `app\x64\Release\` (or `app\x64\Debug\`) containing `ProFile Counter.exe`, `mupdf.exe`, and `Newtonsoft.Json.dll` side by side — `ProFile Counter.exe` expects `mupdf.exe` in its own working directory (see `FileAnalyzer.cs`, which invokes `mupdf.exe` as a bare relative filename). `libmupdf.lib` itself lands under `app\pfc-tool\mupdf\platform\win32\x64\Release\`, per the submodule's own build layout.

There is no automated test suite in this repo.

## The mupdf.exe stdout protocol

Defined by `app/pfc-tool/main.c` on the producer side and parsed by `app/gui/FileAnalyzer.cs` (regex-matched) on the consumer side. If you change one, update the other:

```
mupdf.exe "<filename>" <colorThreshold|-1> <checkPixels 0|1>
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
2. `ProcessWindow` runs a bounded worker pool (`maxProcesses = ProcessorCount - 1`) over a `Queue<string>` of file paths, spawning one `FileAnalyzer` (i.e. one `mupdf.exe` process) per in-flight file and refilling the pool as each completes (`NextFile`/`Analyzer_AnalysisComplete`). All UI mutation from analyzer callbacks goes through `Utility.InvokeIfRequired` since `Process` events fire on background threads.
3. Each `FileAnalyzer` owns one `mupdf.exe` child process, streams stdout via `OutputDataReceived`, and raises `ProgressChanged`/`AnalysisComplete` events; a `TPCFile` (`File.cs`) accumulates `TPCFilePage` (`FilePage.cs`) entries as they arrive.
4. Completed `TPCFile` results feed `PageSizeCounter` (`PageSizeCounter.cs`), which buckets pages into user-defined `PageSize` ranges (`PageSize.cs`, `IsMatch` checks both width×height orientations) plus color/BW/unknown counts, for the summary grid in `MainForm`.

## Settings

`Settings.cs` is a static singleton loading/saving `UserSettings` as XML to `%LocalAppData%\ProFile Counter\UserSettings.xml`. It fails closed: any deserialization error (including unknown XML elements/attributes, which are treated as hard errors) falls back to `DefaultUserSettings`, which also defines the hard-coded standard paper sizes (ANSI/ARCH series). `PageSizeManager`/`PageSizeEditor` forms let users edit the page size list at runtime.

## Other components

- `docs/` is the GitHub Pages update-check + download site (`index.html`, `latest_version.json`) — `Settings.DefaultUserSettings.AppUpdateJsonUrl` points at it, and `MainForm` polls it on startup when `CheckForProgramUpdates` is set.
- `installer/script.iss` is an Inno Setup script that packages the Release|x64 output (`ProFile Counter.exe`, `mupdf.exe`, `Newtonsoft.Json.dll`) into the distributable installer. Bump `MyAppVersion` there, in the csproj `ApplicationVersion`, and in `docs/latest_version.json`/`docs/index.html` together when cutting a release.
- `test files/` contains sample PDFs (including a known mupdf color-detection edge case, per the filename) used for manual testing — there is no automated harness driving them.
