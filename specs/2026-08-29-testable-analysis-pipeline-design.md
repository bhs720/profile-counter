# Making the file-analysis pipeline testable

**Date:** 2026-08-29
**Branch:** `feature/testable-analysis-pipeline`, branched from `develop`, PR into `develop`.

## Problem

Two classes hold the logic most worth testing, and neither can be tested at all.

`app/gui/FileAnalyzer.cs` spawns one `pfc-tool.exe` per file, parses its stdout against
the documented protocol, and decides whether the file succeeded. Every part of that is
entangled with `System.Diagnostics.Process`.

`app/gui/ProcessWindow.cs` is a WinForms `Form` that also contains a bounded worker pool.
The pool has real invariants — refill on completion, never exceed `maxProcesses`, finish
only when the queue and the running set are both empty, cancel drains both — and none of
them need a window.

The recent commit history is concentrated in exactly this code: `1e1a280` (three-signal
completion), `ee8038f` (the unsigned `Size=` regex making a bogus page size a protocol
violation) and `fd228f0` (invariant-culture argument formatting). Locking those fixes in
is a large part of the value here.

## Scope

Two new projects join `app/ProFileCounter.sln`. A UI-free class library takes the
analysis pipeline; an xUnit project tests it. The WinForms project is never built by
`dotnet test`.

## Projects

### `app/core/ProFileCounter.Core.csproj`

- `Microsoft.NET.Sdk`, `TargetFramework` `net48`, `RootNamespace` `TIFPDFCounter`,
  `AssemblyName` `ProFileCounter.Core`.
- `OutputPath` `..\x64\$(Configuration)\` with `AppendTargetFrameworkToOutputPath=false`,
  so the DLL lands beside `ProFile Counter.exe` and `pfc-tool.exe`. The path is
  `$(ProjectDir)`-relative, never `$(SolutionDir)`-relative, for the reason the GUI
  project's comments give: a `$(SolutionDir)`-relative path relocates output silently
  rather than failing.
- `DebugType none` in Release, `full` in Debug, matching the GUI so Release still ships
  without PDBs.
- `Microsoft.NETFramework.ReferenceAssemblies` (`PrivateAssets=all`) so no targeting pack
  is required, plus the framework `<Reference>` items the SDK does not add for net48.
- No `.resx` files. The GUI's resx manifest names had to be wired explicitly during the
  SDK conversion; the new projects stay out of that area entirely.

### `app/tests/ProFileCounter.Tests.csproj`

- `net48`, xunit 2.9.x, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`,
  `Microsoft.NETFramework.ReferenceAssemblies`.
- Default `bin\` output. It must **not** write to `app\x64\$(Configuration)\`, or the
  xunit assemblies end up in the directory the installer packages from.
- `ProjectReference` to core only. Never to the GUI.

### Neither new project sets `PlatformTarget`

The GUI is x64 because it hosts WinForms and launches an x64 child. A managed class
library has no such need. `dotnet test` on `net48` has historically defaulted its test
host to x86, and an x64-marked `ProFileCounter.Core.dll` would then fail to load with
`BadImageFormatException`. AnyCPU loads under either host, and the integration tests
launch `pfc-tool.exe` as a separate process, so host bitness does not reach it.

### Solution

Both projects join the existing `Debug|Any CPU` / `Release|Any CPU` configurations. The
GUI gains a `ProjectReference` to core.

## Namespace

Both assemblies use `TIFPDFCounter`. Namespaces span assemblies, so no `using` statement
changes anywhere. Only duplicate *type names* would collide, which affects exactly one
type — see `Utility` below.

## Core contents

### Moved unchanged

`ColorMode`, `TPCFilePage`, `TPCFile`, `PageSize`, `PageSizeCounter`.

`TPCFile`'s constructor keeps its `new FileInfo(...).Length` call. It already catches and
degrades to 0, so a test constructing one for a path that does not exist gets `FileSize`
0 with no failure. Changing when the size is sampled buys nothing this work needs.

### `Utility` split

`BytesToString` and `TryGetFileLength` have no UI dependency and move to core, keeping the
name `Utility`. The WinForms half — `InvokeIfRequired` and `BeginInvokeIfRequired` —
stays in the GUI renamed to `UiThread`, carrying its comments verbatim. Renaming the GUI
half is the cheaper side: two call sites.

`BytesToString` divides a `long` by 1024 repeatedly, so 1536 bytes displays as "1 KB",
not "1.5 KB". That is current behaviour and users are used to it. A test locks it in. It
is not corrected as part of this work.

### `PfcToolProtocol` (static, pure)

- `FormatArguments(filename, checkColor, colorThreshold, checkPixels)` — the
  `CultureInfo.InvariantCulture` formatting from `fd228f0`. A comma-decimal culture would
  otherwise emit `0,25`, which the tool rejects.
- `Parse(line)` returning a value with `Kind` of `Blank`, `Header`, `Page` or
  `Unrecognized`, plus the parsed fields. Holds both regexes, the `/72m`
  invariant-culture decimal conversion of points to inches, and the
  `0 -> BW / 1,2 -> Color / else -> Unknown` map.

The `Size=([\d\.]+),([\d\.]+)` regex is unsigned, which is what makes `ee8038f`'s
`Size=-4294967296.000000,...` line a protocol violation rather than a bad number. That is
now directly assertable.

### `IPfcToolProcess` and `IPfcToolProcessFactory`

```
event Action<string> OutputLineReceived;   // never null; EOF is a separate signal
event Action         OutputEnded;
event Action<string> ErrorLineReceived;
event Action         ErrorEnded;
event Action<int>    Exited;               // carries the exit code
void Start();                              // may throw
void Kill();
```

The three completion signals are separate because the ordering between them is the thing
most worth testing. The real implementation wraps `System.Diagnostics.Process`, keeping
`CreateNoWindow`, both redirects, `UseShellExecute=false` and the UTF-8
`StandardOutputEncoding`/`StandardErrorEncoding` that `fd228f0` added. A fake in tests
raises the signals in any order.

`Exited` carrying the exit code removes the guess in `FileAnalyzer.Validate()`, which
today reads `process.ExitCode` inside a `try/catch` that treats any failure as exit code
0, i.e. as success. The analyzer stores `int?`; null means the process never exited, which
is reachable only when `Start()` threw, in which case the file has already failed.

### `AnalysisOptions`

`ToolPath` (default `"pfc-tool.exe"`, preserving today's bare relative launch resolved
against the working directory), `PerformColorAnalysis`, `ColorThreshold`,
`CheckImagePixels`. The GUI builds this from `Settings.Current` and hands it down.

`Settings.cs` stays in the GUI: it shows a `MessageBox` on load failure, so it cannot move
to a UI-free library. Core therefore never reads settings.

Making `ToolPath` overridable is also what lets the integration tests run the real tool
from a test host, whose working directory is not `app\x64\$(Configuration)\`.

### `FileAnalyzer`

Keeps accumulation into `TPCFile`, the three-signal completion handshake and the failure
rules. Loses the parsing (to `PfcToolProtocol`) and the process construction (to the
factory). Loses `Tag`, which existed solely to carry a `DataGridViewRow` and which nothing
else reads.

Takes an injected `Func<DateTime>` clock, defaulting to `() => DateTime.Now`, so the
500 ms `ProgressChanged` throttle is testable without timing-dependent tests: a fake clock
proves that a second page within 500 ms raises no event and that one after 500 ms does.

There remains deliberately no timeout anywhere in the class, for the reasons `1e1a280`
records.

### `AnalysisBatch`

The worker pool extracted from `ProcessWindow`.

- Constructed with an ordered file list, `AnalysisOptions`, an `IPfcToolProcessFactory`,
  an optional concurrency cap (default `Max(ProcessorCount - 1, 1)`) and an optional clock.
- Exposes `Items`, each carrying an index and a filename. Events carry the item, so
  `ProcessWindow` maps back to a grid row through a row array indexed to match. This
  replaces `FileAnalyzer.Tag` and removes the row lookup's dependence on paths within a
  batch happening to be unique.
- Events: `FileStarted`, `FileProgress`, `FileCompleted` (carrying the `FileAnalyzer`, so
  the GUI can read `Cancelled`, `Failed`, `Errors` and `Result` as it does today) and
  `BatchFinished`.
- `Start()` and `Cancel()`.

#### Threading

The batch guards its queue and running set with its own lock, never invokes a handler
while holding that lock, and raises events on whatever thread completed the work.
`ProcessWindow` marshals to the UI in its handlers with `UiThread.BeginInvokeIfRequired`.

This preserves the property the comment on `BeginInvokeIfRequired` exists to protect:
analyzer callbacks must never block a thread pool thread waiting on the UI thread. The UI
thread does real work per completed file, and the blocked threads would be the very pool
threads the redirected stream readers need in order to deliver end-of-file.

#### Re-entrancy

`Go()` can raise `AnalysisComplete` synchronously, when `Start()` throws. That is exactly
why `BeginInvokeIfRequired` posts even when already on the UI thread: running the handler
inline would re-enter `NextFile` from inside `NextFile`, nesting one frame set per queued
file, and a missing `pfc-tool.exe` with a few hundred dropped files would overflow the
stack.

A batch that pumps directly from its own completion handler recreates that. The pump is
therefore a loop with a "pump already running" flag: a synchronous completion sets
`pumpRequested` and returns, and the outer loop picks it up. This is tested, not assumed.

#### Duplicate guard

The rule that each file is counted once stays where it is today, in
`MainForm.OnDragDrop`. The batch adds a defensive guard: at construction it collapses
repeated paths, normalized with `Path.GetFullPath` and compared `OrdinalIgnoreCase`, and
exposes only the surviving unique items. `ProcessWindow` builds one row per item, so no
file can be analyzed or summed twice even if a caller passes one twice.

The guard is silent rather than fatal. A throw here would crash the application on a path
that has never fired in practice.

**Known limitation, deliberately not fixed.** `MainForm`'s own comparison is ordinal and
case-sensitive, so `C:\Docs\a.pdf` and `C:\docs\a.pdf` are two files to it. The batch
guard catches that within a single drop but not across drops. Aliases that no path
comparison can catch — a UNC path against a mapped drive, an 8.3 short name — remain
covered only informationally by the existing MD5 duplicate report. This is recorded
because over-reporting page totals affects what customers charge their own customers.

## GUI changes

`ProcessWindow` becomes UI only. It builds rows from `batch.Items`, subscribes to the four
batch events and marshals each with `UiThread.BeginInvokeIfRequired`, and keeps its
current semantics: close on finish when cancelled or when nothing failed, otherwise retitle
to "Processing finished with errors"; failed rows in dark red showing the first 255
characters of `Errors`; completed rows removed from the grid. `CancelBatch` delegates to
`batch.Cancel()`.

The `Cancelled`-before-`Failed` check order is preserved. A cancelled analyzer's process is
killed, exits non-zero, and so is also marked failed; the display depends on testing
`Cancelled` first.

`MainForm` builds `AnalysisOptions` from `Settings.Current` and passes it to
`ProcessWindow`.

## Tests

### Hermetic

- **Protocol.** Header line, page line, blank line, garbage. The negative-size line from
  `ee8038f` is rejected as `Unrecognized`. Parsing and argument formatting both run with
  `CurrentCulture` set to a comma-decimal culture, locking in both invariant-culture
  fixes. Colour mapping over 0, 1, 2, -1 and an unexpected value. `-1` emitted when colour
  analysis is off.
- **FileAnalyzer**, against a fake process: completion only after all three signals, in
  each of the six orderings; non-zero exit fails; page-count mismatch fails; zero page
  count fails; no header line fails; a page line before the header fails; garbage fails;
  stderr is accumulated and readable; a `Start()` that throws reports a failed file and
  still completes; `Cancel` sets `Cancelled`; the progress throttle against a fake clock.
- **AnalysisBatch**, against a fake factory: never exceeds the concurrency cap; refills as
  each completes; finishes only when queue and running set are both empty; an empty file
  list finishes immediately; cancel drains the queue and cancels running analyzers;
  a duplicate path is analyzed once; 1000 files against an always-throwing factory
  complete without overflowing the stack.
- **Utility.** `BytesToString` locks 1536 -> "1 KB", with 0, 1023, 1024 and a gigabyte.
- **PageSize / PageSizeCounter.** `IsMatch` in both orientations; the null-`PageSize`
  catch-all counter; bucketing by colour.
- **PageSize XML shape.** A `List<PageSize>` round-tripped through `XmlSerializer`,
  asserting the serialized element and attribute names are unchanged. `Settings.Load`
  fails closed — any unknown element or attribute is a hard error that silently falls
  back to defaults and discards the user's configured page sizes — so a rename here would
  be costly and silent. Serialization is by type shape, not assembly, so the move should
  be inert; this test plus a manual check against a real `UserSettings.xml` verifies that
  rather than assuming it.

### Integration, opt-in

A `PfcToolFactAttribute : FactAttribute` sets `Skip` in its constructor when
`pfc-tool.exe` cannot be found by walking up from the test assembly's base directory to
`app\x64\Release\` then `app\x64\Debug\`. Tests then report as *skipped*, not failed, when
the native tool has not been built. No extra package.

The tests run the real tool over `test files\` and assert **protocol conformance only**:
analysis succeeds, page count is non-zero, the number of pages received matches the
reported count, sizes parse. They assert nothing about colour classification — the
`pfc-regression` skill already verifies that properly against a shipped baseline, and
duplicating it here would produce a test that fails for reasons unrelated to the C# code.

## Deliberate behaviour changes

Each is stated in the commit message that makes it. Nothing else changes observably.

1. **The exit code is always known.** `Validate()` currently treats an unreadable
   `ExitCode` as 0, i.e. as success. Carrying it on the exit signal removes the guess.
2. **An empty batch finishes.** `ProcessWindow` today never finishes one: `NextFile()`
   does nothing, no analyzer completes, and `FinishBatch()` is never reached. It is latent
   rather than live, because `MainForm` guards against an empty drop. The extracted batch
   raises `BatchFinished` immediately.
3. **A duplicate path within one batch is analyzed once**, per the guard above.

## Also updated

- `installer/script.iss` gains a `Source:` line for `ProFileCounter.Core.dll`.
- `CLAUDE.md`: the "Processing pipeline (C# side)" section is rewritten for the new flow;
  `dotnet test app\ProFileCounter.sln` is documented as the command to run after changing
  C# code, noting that it builds neither the WinForms project nor the native tool.

## Commit sequence

Each commit leaves `dotnet build` and `dotnet test` green.

1. Scaffold `app/core` and `app/tests`, join the solution, reference core from the GUI.
2. Move the value types; split `Utility` / `UiThread`; add their tests.
3. Move `FileAnalyzer` to core unchanged.
4. Extract `PfcToolProtocol` and `AnalysisOptions`; add parser tests.
5. Introduce the process seam; fix the exit-code guess; add analyzer tests.
6. Extract `AnalysisBatch`; rewire `ProcessWindow`; add batch tests.
7. Add the integration tests and the skip attribute.
8. Update `CLAUDE.md` and the installer.

## Verification

- `dotnet test app\ProFileCounter.sln` passes, reporting the integration tests as skipped
  without the native tool and as passing with it.
- `dotnet build app\ProFileCounter.sln -c Release` succeeds with no new warnings.
- `build.ps1` still builds both solutions.
- The app runs end to end: several files from `test files\` dropped at once so the worker
  pool is exercised; progress bars advance, completed files leave the grid, failures show
  in red with their error text, summary totals appear. A batch cancelled mid-run by
  closing the process window closes rather than hanging.
- Existing user settings survive: back up `%LocalAppData%\ProFile Counter\UserSettings.xml`,
  run the app, confirm the configured page sizes are still there and that no
  "Default settings are loaded" dialog appeared.

The last two matter more than the rest. This phase changes live code paths, so a green
test suite proves the new code works, not that the application still does.
