# Replacing the batch's threading model

**Date:** 2026-08-30
**Branch:** `feature/testable-analysis-pipeline` (the existing PR #6 branch, before merge).
**Supersedes:** the "Threading" and "Re-entrancy" sections of
`specs/2026-08-29-testable-analysis-pipeline-design.md`.

## Why reopen a settled decision

The original design had `AnalysisBatch` guard its own state with a lock and raise events
on whatever thread completed the work, with `ProcessWindow` marshalling to the UI in its
handlers. That choice was made to preserve one property: analyzer callbacks must never
block a thread-pool thread waiting on the UI thread.

Posting rather than blocking is what preserves that property. Free-threading the batch was
not required for it, and was not required for testability either. An adversarial review
found four Important defects; three of them live in the machinery that free-threading made
necessary:

- a throwing `FileCompleted` handler permanently stalls the batch, because the `Pump()`
  that refills the pool sits one line outside the `try/finally` that guards
  `pendingCompletions`;
- a throwing `FileStarted` handler leaves an analyzer in `running` that is never started
  and that `Cancel()` cannot remove, wedging the batch forever;
- `ProcessWindow.Results` reads the batch's live list without taking the lock, on the one
  path — the restored double-close escape hatch — where the batch is not finished.

The pre-change code drove the pool from the UI thread and could not race at all. Its only
pool defect was that an empty batch never finished, which was latent because `MainForm`
guards against an empty drop. This design restores that threading model and keeps the
testability the extraction bought, by making the marshalling target an injected seam
rather than a `Form`.

## Which primitives are inherent, and which are consequential

This distinction drives the whole design.

**Inherent.** `System.Diagnostics.Process` delivers stdout lines, stderr lines and process
exit on three different thread-pool threads. Nothing about this design changes that, so
`FileAnalyzer.signalsOutstanding`, `completionRaised` and the three per-signal idempotence
flags all stay.

**Consequential.** `AnalysisBatch`'s `gate`, `pumping`, `clearedInLock` and
`pendingCompletions` exist only because the batch chose to be entered from arbitrary
threads. All four are deleted.

## The dispatcher

```csharp
namespace TIFPDFCounter
{
    public interface IDispatcher
    {
        void Post(Action action);
    }
}
```

**`Post` must never run the action inline.** This is the contract the design rests on:
`FileAnalyzer.Go()` raises `AnalysisComplete` on the calling thread when the process fails
to start, and posting is what turns that from re-entrancy into a queued message. It
replaces the `pumping` flag with a structural guarantee, and it gets its own test.

### `ControlDispatcher` (GUI)

Wraps a `Control`. Its body is `UiThread.BeginInvokeIfRequired`'s, unchanged:

```csharp
if (control.IsDisposed || control.Disposing || !control.IsHandleCreated)
    return;
try { control.BeginInvoke(action); }
catch (InvalidOperationException) { }
```

The guard is not decoration. Before it existed, a callback arriving after the window
closed ran `DataGridView` code on a thread-pool thread against a disposed form, which
takes the process down.

**`UiThread` is deleted.** Its four call sites disappear when `ProcessWindow` stops
marshalling per-event, and `InvokeIfRequired` — the blocking variant — has had no callers
since before this branch. Folding the guard into `ControlDispatcher` puts it in one place
that every batch callback goes through, rather than in a helper each future caller must
remember to use.

### `QueueDispatcher` (tests)

Queues actions and drains them on demand (`RunUntilIdle`). Batch tests become fully
deterministic: no threads, no `Barrier`, no timing. Its queue holds the only lock in the
new design, which is the right place for one — a general-purpose queue, not batch logic.

## `AnalysisBatch`

Constructor takes an `IDispatcher` alongside the existing arguments. All state —
`queue`, `running`, `results`, `failures`, `cancelled`, `finished` — is touched only on
the dispatcher thread.

```csharp
public void Start()
{
    dispatcher.Post(Pump);
}

public void Cancel()
{
    dispatcher.Post(() =>
    {
        cancelled = true;
        queue.Clear();
        foreach (var analyzer in running.Keys.ToList())
            analyzer.Cancel();
        Pump();
    });
}

private void Pump()                      // dispatcher thread only
{
    while (!cancelled && running.Count < maxConcurrency && queue.Count > 0)
    {
        var item = queue.Dequeue();
        var analyzer = CreateAnalyzer(item);
        running.Add(analyzer, item);

        // Go() in a finally: an analyzer added to `running` MUST be started, or nothing
        // will ever deliver a completion for it and the batch can never finish. Raising
        // FileStarted first preserves today's event order; the finally is what stops a
        // throwing subscriber from stranding the analyzer. Go() may complete
        // synchronously, but that completion is posted, so it cannot re-enter this loop.
        try { FileStarted(item); }
        finally { analyzer.Go(); }
    }

    if (!finished && running.Count == 0 && queue.Count == 0)
    {
        finished = true;
        BatchFinished();
    }
}

private void OnAnalyzerComplete(FileAnalyzer analyzer)   // any thread
{
    dispatcher.Post(() =>
    {
        BatchItem item;
        if (!running.TryGetValue(analyzer, out item))
            return;

        running.Remove(analyzer);
        analyzer.ProgressChanged -= OnAnalyzerProgress;
        analyzer.AnalysisComplete -= OnAnalyzerComplete;

        // Cancelled is checked before Failed on purpose: a cancelled analyzer's process is
        // killed, so it exits non-zero and is marked failed too.
        if (analyzer.Cancelled) { }
        else if (analyzer.Failed) failures.Add(analyzer);
        else if (analyzer.Result != null) results.Add(analyzer.Result);

        try { FileCompleted(item, analyzer); }
        finally { Pump(); }
    });
}
```

`OnAnalyzerProgress` posts in the same way.

### What this fixes structurally

- **`BatchFinished` is last.** `FileCompleted` and the `Pump()` that may raise
  `BatchFinished` run in the same posted action, in that order. `pendingCompletions` is
  deleted.
- **A throwing `FileCompleted` cannot stall the pool.** `Pump()` is in the `finally`.
- **A throwing `FileStarted` cannot strand an analyzer.** `Go()` runs in a `finally`, so an
  analyzer that reached `running` is always started and will always deliver a completion.
  The exception escapes to the message loop, which is honest; the loop stops early and the
  next completion resumes it.
- **The unlocked `Results` read has nowhere to live.** `results` is mutated and read on the
  same thread.
- **Re-entrancy is structural, not flagged.** The 1000-file synchronous-failure test still
  passes, now because posting breaks the recursion rather than because a flag catches it.

## `FileAnalyzer`

Unchanged. It keeps `signalsOutstanding`, `completionRaised`, the three per-signal
idempotence flags and the `state` CAS.

The CAS stays even though `Go()` and `Cancel()` now both arrive on the dispatcher thread
when driven by a batch: `FileAnalyzer` is public and the integration tests drive it
directly without a dispatcher, so removing it would make the type unsafe for its non-batch
callers.

The per-signal flags remain by the user's earlier ruling. The adversarial review's
objection — that they defend an internal interface against an implementation that does not
exist — is recorded and not acted on.

## `ProcessWindow`

- Constructs `new ControlDispatcher(this)` and passes it to the batch. The handle does not
  exist at construction time, but the batch is only started from `ProcessWindow_Load`, by
  which point it does.
- `batch.Start()` now posts rather than pumping inline, so the first analyzer starts on the
  message after `ProcessWindow_Load` returns instead of before it. The rows are already
  built by then, which is the only ordering `Load` cared about. Practically this means the
  window paints its "Queued" rows fractionally sooner than it does today.
- The four batch handlers lose their marshalling wrapper. They already run on the UI
  thread.
- `Results` keeps returning `batch.Results.ToList()`. Both sides are now the UI thread, so
  the snapshot is a courtesy rather than a fix.

Everything else about the window is unchanged, including the double-close escape hatch and
the `Cancelled`-before-`Failed` ordering.

## Also in scope: the fourth finding

Unrelated to threading, but it blocks merge and is two lines:

- `PfcToolProtocol.Parse` accepts `PageCount=2000000000`, and `TPCFile`'s
  `new List<TPCFilePage>(pageCount)` then throws `OutOfMemoryException` on the stdout
  reader thread — defeating the parser hardening one call later. `Parse` treats a page
  count above **1,000,000** as a protocol violation, returning `Unrecognized` so the line
  fails the file like any other malformed line. The bound is arbitrary but far above any
  real document: the largest file in the problem-file corpus is in the low thousands of
  pages, and a `List` capacity of a million costs 8 MB, which allocates rather than
  throwing.
- `Program.Main` installs no `AppDomain.CurrentDomain.UnhandledException` handler, although
  roughly six comments in this codebase reason about throws on thread-pool threads killing
  the process. Add one.

This lands as its own commit, separate from the threading change.

## Tests

**Rewritten.** `AnalysisBatchTests` drive a `QueueDispatcher` and become deterministic. The
existing assertions survive in shape: concurrency cap, refill, finish condition, empty
list finishes immediately, cancel drains both, duplicate collapsed, 1000 synchronous
failures do not overflow the stack.

**Deleted.** `ConcurrentCompletionSmokeTest` and
`BatchFinishedNeverFiresBeforeAPendingFileCompleted` — both assert properties the type can
no longer violate, and the second already documented that it did not detect the bug it
targeted. `CompletesExactlyOnceWhenAStartFailureRacesTheCompletionSignals`, together with
`WarmUpJit` and `CalibrateHandicap` — 130 lines of timing-calibrated test code plus a
global `ThreadPool.SetMinThreads` mutation, guarding a single `Interlocked.CompareExchange`
that its own documentation admits it misses one run in ten.

**Shrunk.** `CompletesExactlyOnceUnderConcurrentSignalDelivery` from 2,000 iterations to
roughly 200. It covers the same guard honestly and is the one worth keeping.

**Added.**

- `Post` must not run its action inline — the contract the whole design rests on.
- A throwing `FileCompleted` still refills the pool, with **three files at concurrency
  one**. The current test uses two files at concurrency two, where the other analyzer's
  completion does the pumping, which is why it passes against the defect.
- A throwing `FileStarted` does not strand its analyzer, and the batch still finishes.
- `PageCount` beyond a sane bound is a protocol violation rather than a crash.

Expect the suite to fall from ~7s to under 1s.

## Behaviour changes

- **The pool refills on the UI thread again**, as it did before this branch. The UI is once
  more a throttle on child-process spawning. This is the shipped behaviour, exercised over
  a 1500-file corpus; the free-threaded version's decoupling was an unannounced change.
- **`Cancel()` is asynchronous.** It posts, so it takes effect on the next dispatched
  action rather than inside the caller's stack frame. `ProcessWindow_FormClosing` already
  cancels the close and waits for the batch to drain, so this changes nothing there. It
  does change one exotic path: a subscriber that calls `Cancel()` from inside its own
  `FileStarted` handler no longer prevents that file's process from starting — the process
  starts and is then killed. The guarantees that matter are unchanged: no orphaned child,
  the batch still finishes, and the file is recorded as cancelled rather than counted. The
  test covering that path is rewritten to assert the new outcome.
- The `started = !cancelled` re-check between dequeue and `running.Add` is deleted.
  `cancelled` can no longer change midway through a pump, because everything that sets it
  is dispatched.
- Nothing else changes observably. The close behaviour, the `Cancelled`-before-`Failed`
  ordering, results kept on cancel, and the duplicate guard are all as they are today.

## Verification

- `dotnet test app\ProFileCounter.sln` green, and materially faster.
- `dotnet build app\ProFileCounter.sln -c Release` with 0 warnings.
- The three probes from the adversarial review re-run and now fail to reproduce: the
  three-files-at-concurrency-one stall, the throwing `FileStarted` wedge, and the unlocked
  `Results` read.
- End-to-end in the real GUI, driven manually as before: a multi-file drop with progress
  advancing, completed rows leaving the grid, failures in red, summary totals, settings
  intact, a re-dropped file refused, and a batch cancelled mid-run closing cleanly with no
  orphaned `pfc-tool.exe`.

The last item still matters most. `ProcessWindow` has no automated coverage and
`dotnet test` does not compile it.
