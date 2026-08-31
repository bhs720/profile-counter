# Batch Threading Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `AnalysisBatch`'s free-threaded lock-and-pump design with an injected `IDispatcher`, so all batch state lives on one thread and the machinery behind three open defects deletes itself.

**Architecture:** A three-line `IDispatcher` interface whose `Post` must never run inline. The GUI supplies a `Control`-backed implementation carrying the disposed-form guard; tests supply a queue drained on demand. `AnalysisBatch` becomes a plain `while` loop with no lock, no re-entrancy flag and no pending-completions counter. `FileAnalyzer` is untouched — `System.Diagnostics.Process` delivers on three thread-pool threads regardless.

**Tech Stack:** C# on .NET Framework 4.8, SDK-style projects, xUnit v2, `dotnet build` / `dotnet test`.

**Spec:** `specs/2026-08-30-batch-threading-model-design.md`

## Global Constraints

- Branch is `feature/testable-analysis-pipeline` — the existing PR #6 branch. Do not create a new branch. Do not push.
- **`IDispatcher.Post` must never run its action inline.** This is the contract that replaces the `pumping` flag; a synchronously-completing analyzer must post and return rather than re-enter the pump.
- `FileAnalyzer` keeps `signalsOutstanding`, `completionRaised`, the three per-signal idempotence flags and the `state` CAS. Do not remove them — `Process` delivers on three thread-pool threads whatever the batch does, and `FileAnalyzer` is public with non-batch callers.
- The namespace stays `TIFPDFCounter` in both assemblies.
- `app/tests` keeps its default `bin\` output. Never point it at `app\x64\` — the installer packages from there.
- Do NOT commit the pre-existing modified submodule `app/pfc-tool/mupdf`. Never `git add -A` from the repo root.
- Every commit must leave `dotnet build app\ProFileCounter.sln -c Release` at 0 warnings / 0 errors and `dotnet test app\ProFileCounter.sln` green.
- Append to every commit message, after a blank line: `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`

## File Structure

**Created:**

| Path | Responsibility |
|---|---|
| `app/core/IDispatcher.cs` | The one-method marshalling seam |
| `app/gui/ControlDispatcher.cs` | WinForms implementation, carrying the disposed-form guard |
| `app/tests/QueueDispatcher.cs` | Deterministic test double, drained on demand |

**Modified:** `app/core/AnalysisBatch.cs`, `app/core/PfcToolProtocol.cs`, `app/gui/ProcessWindow.cs`, `app/gui/Program.cs`, `app/tests/AnalysisBatchTests.cs`, `app/tests/FileAnalyzerTests.cs`, `app/tests/PfcToolProtocolTests.cs`, `CLAUDE.md`

**Deleted:** `app/gui/UiThread.cs`

---

### Task 1: The dispatcher seam

**Files:**
- Create: `app/core/IDispatcher.cs`, `app/gui/ControlDispatcher.cs`, `app/tests/QueueDispatcher.cs`
- Delete: `app/gui/UiThread.cs`
- Modify: `app/gui/ProcessWindow.cs` (four handlers stop marshalling)
- Test: `app/tests/QueueDispatcherTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `interface IDispatcher { void Post(Action action); }` in `ProFileCounter.Core`; `sealed class ControlDispatcher : IDispatcher` with constructor `ControlDispatcher(Control control)` in the GUI; `sealed class QueueDispatcher : IDispatcher` in the tests with `int RunUntilIdle()` and `int PendingCount { get; }`.

This task does not change `AnalysisBatch`. It builds the seam and moves the existing marshalling behind it, so Task 2's diff is only the threading change.

- [ ] **Step 1: Write the failing test**

Create `app/tests/QueueDispatcherTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace TIFPDFCounter.Tests
{
    public class QueueDispatcherTests
    {
        [Fact]
        public void PostDoesNotRunTheActionInline()
        {
            // The whole threading design rests on this. AnalysisBatch drops its
            // re-entrancy flag because a synchronously-completing analyzer posts and
            // returns instead of re-entering the pump. A dispatcher that ran actions
            // inline would silently restore the recursion the flag used to catch.
            var dispatcher = new QueueDispatcher();
            bool ran = false;

            dispatcher.Post(() => ran = true);

            Assert.False(ran);
            Assert.Equal(1, dispatcher.PendingCount);

            dispatcher.RunUntilIdle();
            Assert.True(ran);
        }

        [Fact]
        public void RunUntilIdleDrainsActionsQueuedByOtherActions()
        {
            // Pump() posts nothing itself, but a completion posted from inside a running
            // action must still be drained by the same RunUntilIdle call, or tests would
            // have to guess how many times to pump.
            var dispatcher = new QueueDispatcher();
            var order = new List<string>();

            dispatcher.Post(() =>
            {
                order.Add("first");
                dispatcher.Post(() => order.Add("second"));
            });

            int ran = dispatcher.RunUntilIdle();

            Assert.Equal(2, ran);
            Assert.Equal(new[] { "first", "second" }, order);
        }

        [Fact]
        public void ActionsRunInPostOrder()
        {
            var dispatcher = new QueueDispatcher();
            var order = new List<int>();

            dispatcher.Post(() => order.Add(1));
            dispatcher.Post(() => order.Add(2));
            dispatcher.Post(() => order.Add(3));
            dispatcher.RunUntilIdle();

            Assert.Equal(new[] { 1, 2, 3 }, order);
        }

        [Fact]
        public void AThrowingActionDoesNotStrandTheQueue()
        {
            // A subscriber's exception propagates to the caller of RunUntilIdle -- the
            // message loop, in the GUI -- but must not leave the remaining actions
            // permanently undrainable.
            var dispatcher = new QueueDispatcher();
            bool secondRan = false;

            dispatcher.Post(() => { throw new System.InvalidOperationException("boom"); });
            dispatcher.Post(() => secondRan = true);

            Assert.Throws<System.InvalidOperationException>(() => dispatcher.RunUntilIdle());

            dispatcher.RunUntilIdle();
            Assert.True(secondRan);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter QueueDispatcherTests
```

Expected: FAIL, `The type or namespace name 'QueueDispatcher' could not be found`.

- [ ] **Step 3: Write the interface**

Create `app/core/IDispatcher.cs`:

```csharp
using System;

namespace TIFPDFCounter
{
    /// <summary>
    /// Runs an action on the thread that owns a batch's state.
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Queues <paramref name="action"/> to run later.
        /// <para>
        /// Implementations MUST NOT run the action inline. <see cref="AnalysisBatch"/>
        /// relies on it: <see cref="FileAnalyzer.Go"/> raises AnalysisComplete on the
        /// calling thread when the process fails to start, and posting is what turns that
        /// from re-entrancy into a queued message. An implementation that ran actions
        /// inline would nest one frame set per queued file, and a missing pfc-tool.exe
        /// with a few hundred dropped files would overflow the stack, which .NET cannot
        /// catch.
        /// </para>
        /// <para>
        /// Dropping the action is a valid outcome when the target can no longer run it --
        /// a closed window, for instance. Running it anyway on the wrong thread is not.
        /// </para>
        /// </summary>
        void Post(Action action);
    }
}
```

- [ ] **Step 4: Write the WinForms implementation**

Create `app/gui/ControlDispatcher.cs`:

```csharp
using System;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    /// <summary>
    /// Marshals onto the UI thread that owns a <see cref="Control"/>, without waiting.
    /// <para>
    /// Posting rather than blocking is not a preference. Analyzer callbacks arrive on
    /// thread pool threads, and Control.Invoke would block one of them until the UI
    /// thread caught up. The UI thread does real work per completed file -- removing a
    /// grid row, scanning the whole grid, starting the next child process -- so with a
    /// full worker pool the blocked threads are the very thread pool threads the
    /// redirected stream readers need in order to deliver their end-of-file callbacks.
    /// Completion for other files then stalls behind the UI, which is how a perfectly
    /// healthy file ended up reported as having timed out.
    /// </para>
    /// <para>
    /// An action arriving after the window has closed is dropped rather than run. The
    /// guard covers three distinct states: after ShowDialog returns but before Dispose,
    /// IsDisposed is still false while IsHandleCreated is already false; IsDisposed stays
    /// false for the whole duration of Dispose, which is what Disposing covers. Running
    /// the action in any of them would execute DataGridView code on a thread pool thread
    /// against a dying control, and an unhandled exception there takes the process down.
    /// </para>
    /// </summary>
    public sealed class ControlDispatcher : IDispatcher
    {
        private readonly Control control;

        public ControlDispatcher(Control control)
        {
            if (control == null) throw new ArgumentNullException("control");
            this.control = control;
        }

        public void Post(Action action)
        {
            if (control.IsDisposed || control.Disposing || !control.IsHandleCreated)
                return;

            try
            {
                control.BeginInvoke(action);
            }
            // Covers ObjectDisposedException too, which derives from this: the window can
            // be closed between the guard above and the post, and neither case is an error.
            catch (InvalidOperationException) { }
        }
    }
}
```

- [ ] **Step 5: Write the test double**

Create `app/tests/QueueDispatcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using TIFPDFCounter;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// A dispatcher tests drain by hand. Actions queue and run only when RunUntilIdle is
    /// called, which is what makes batch tests deterministic: no threads, no barriers, no
    /// timing.
    /// <para>
    /// The queue holds the only lock in the design. That is deliberate and correct --
    /// analyzer callbacks genuinely arrive on thread pool threads, so enqueuing must be
    /// safe from any thread. Everything downstream of the queue is single threaded.
    /// </para>
    /// </summary>
    public sealed class QueueDispatcher : IDispatcher
    {
        private readonly object gate = new object();
        private readonly Queue<Action> queue = new Queue<Action>();

        public int PendingCount
        {
            get { lock (gate) { return queue.Count; } }
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            lock (gate) { queue.Enqueue(action); }
        }

        /// <summary>
        /// Runs queued actions until none remain, including any queued by the actions
        /// themselves. Returns how many ran. An exception from an action propagates; the
        /// remaining actions stay queued and a further call drains them.
        /// </summary>
        public int RunUntilIdle()
        {
            int ran = 0;
            while (true)
            {
                Action next;
                lock (gate)
                {
                    if (queue.Count == 0)
                        return ran;

                    next = queue.Dequeue();
                }

                ran++;
                next();
            }
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter QueueDispatcherTests
```

Expected: PASS, 4 tests.

- [ ] **Step 7: Move ProcessWindow's marshalling behind the seam and delete UiThread**

In `app/gui/ProcessWindow.cs`, add a field and initialise it in the `ProcessWindow(List<string>)` constructor, before the `batch` is constructed:

```csharp
        private readonly IDispatcher dispatcher;
```

```csharp
            dispatcher = new ControlDispatcher(this);
```

Then replace every `UiThread.BeginInvokeIfRequired(this, () => { ... });` call in the four handlers `Batch_FileStarted`, `Batch_FileProgress`, `Batch_FileCompleted` and `Batch_BatchFinished` with `dispatcher.Post(() => { ... });`. The lambda bodies do not change in this task.

Then delete the file:

```bash
git rm app/gui/UiThread.cs
```

`UiThread.InvokeIfRequired` — the blocking variant — has had no callers since before this branch, and `BeginInvokeIfRequired`'s body now lives in `ControlDispatcher`. Confirm nothing else references `UiThread`:

```bash
grep -rn "UiThread" app/ --include=*.cs || echo "no references remain"
```

Expected: `no references remain`.

- [ ] **Step 8: Verify build and full suite**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS. The suite grows by 4 (QueueDispatcherTests).

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add app/core/IDispatcher.cs app/gui/ControlDispatcher.cs app/tests/QueueDispatcher.cs app/tests/QueueDispatcherTests.cs app/gui/ProcessWindow.cs app/gui/UiThread.cs
git commit -F - <<'EOF'
Put the UI marshalling behind a seam

AnalysisBatch is about to stop being free-threaded, and it needs somewhere to
post to that a test can drive. IDispatcher is that seam: one method, and one
contract -- Post must never run its action inline, because that is what will
replace the batch's re-entrancy flag with a structural guarantee.

ControlDispatcher is UiThread.BeginInvokeIfRequired's body, unchanged, including
the guard against a control that is disposed, disposing, or has no handle. That
guard exists because a callback arriving after the window closed once ran
DataGridView code on a thread pool thread against a dying control, which takes
the process down. Folding it into the dispatcher puts it on the path every batch
callback takes, rather than in a helper each future caller has to remember.

UiThread is deleted. Its four call sites now go through the dispatcher, and
InvokeIfRequired -- the blocking variant -- has had no callers since before this
branch.

No behaviour change. The batch still raises events on whatever thread completed
the work; only the route to the UI thread has moved.

EOF
```

---

### Task 2: Single-thread the batch

**Files:**
- Modify: `app/core/AnalysisBatch.cs`
- Modify: `app/gui/ProcessWindow.cs`
- Test: `app/tests/AnalysisBatchTests.cs`

**Interfaces:**
- Consumes: `IDispatcher` from Task 1; `QueueDispatcher` from Task 1.
- Produces: `AnalysisBatch(IEnumerable<string> filenames, AnalysisOptions options, IPfcToolProcessFactory processFactory, IDispatcher dispatcher, int maxConcurrency = 0, Func<DateTime> clock = null)`. All other public members keep their current signatures: `Items`, `Results`, `Failures`, `Finished`, `Start()`, `Cancel()`, and the four events.

**Note on the constructor:** `dispatcher` goes after `processFactory` and before the two optional parameters, because optional parameters must come last. Every existing call site must be updated.

- [ ] **Step 1: Rewrite the batch tests against the dispatcher**

Replace the whole of `app/tests/AnalysisBatchTests.cs` with the following. Three tests change meaning and are called out in comments; `ConcurrentCompletionSmokeTest` is deleted outright because the property it asserted — that no `FileCompleted` arrives after `BatchFinished` — is now structural.

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

        private sealed class Harness
        {
            public readonly FakePfcToolProcessFactory Factory = new FakePfcToolProcessFactory();
            public readonly QueueDispatcher Dispatcher = new QueueDispatcher();
            public readonly AnalysisBatch Batch;

            public Harness(IEnumerable<string> files, int maxConcurrency)
            {
                Batch = new AnalysisBatch(files, Options(), Factory, Dispatcher, maxConcurrency);
            }

            /// <summary>Starts the batch and drains the dispatcher.</summary>
            public void Start()
            {
                Batch.Start();
                Dispatcher.RunUntilIdle();
            }

            /// <summary>Completes one fake process, then drains the posted completion.</summary>
            public void Complete(int index)
            {
                SucceedOnePage(Factory.Created[index]);
                Dispatcher.RunUntilIdle();
            }
        }

        [Fact]
        public void NeverExceedsTheConcurrencyCap()
        {
            var h = new Harness(Files(10), maxConcurrency: 3);
            h.Start();

            Assert.Equal(3, h.Factory.Created.Count);
        }

        [Fact]
        public void RefillsThePoolAsEachFileCompletes()
        {
            var h = new Harness(Files(5), maxConcurrency: 2);
            h.Start();
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(0);
            Assert.Equal(3, h.Factory.Created.Count);

            h.Complete(1);
            Assert.Equal(4, h.Factory.Created.Count);
        }

        [Fact]
        public void FinishesOnlyWhenTheQueueAndTheRunningSetAreBothEmpty()
        {
            var h = new Harness(Files(3), maxConcurrency: 2);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;

            h.Start();
            h.Complete(0);
            Assert.Equal(0, finished);
            h.Complete(1);
            Assert.Equal(0, finished);

            h.Complete(2);
            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Equal(3, h.Batch.Results.Count);
            Assert.Empty(h.Batch.Failures);
        }

        [Fact]
        public void AnEmptyFileListFinishesImmediately()
        {
            // ProcessWindow never finished one of these: NextFile did nothing, no analyzer
            // ever completed, and FinishBatch was never reached. It stayed latent only
            // because MainForm guards against an empty drop.
            var h = new Harness(new List<string>(), maxConcurrency: 0);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;

            h.Start();

            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Empty(h.Factory.Created);
        }

        [Fact]
        public void BatchFinishedIsAlwaysRaisedAfterEveryFileCompleted()
        {
            // The ordering pendingCompletions used to defend is now structural:
            // FileCompleted and the Pump that may raise BatchFinished run in the same
            // dispatched action, in that order.
            var h = new Harness(Files(3), maxConcurrency: 3);
            var order = new List<string>();
            h.Batch.FileCompleted += (item, analyzer) => order.Add("completed:" + item.Index);
            h.Batch.BatchFinished += () => order.Add("finished");

            h.Start();
            h.Complete(0);
            h.Complete(1);
            h.Complete(2);

            Assert.Equal("finished", order.Last());
            Assert.Equal(3, order.Count(x => x.StartsWith("completed:")));
        }

        [Fact]
        public void CancelDrainsTheQueueAndCancelsWhatIsRunning()
        {
            var h = new Harness(Files(6), maxConcurrency: 2);
            h.Start();
            Assert.Equal(2, h.Factory.Created.Count);

            h.Batch.Cancel();
            h.Dispatcher.RunUntilIdle();

            Assert.True(h.Factory.Created[0].Killed);
            Assert.True(h.Factory.Created[1].Killed);

            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Factory.Created[1].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Equal(2, h.Factory.Created.Count);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void CancellingFromWithinFileStartedStillKillsTheProcessAndFinishes()
        {
            // BEHAVIOUR CHANGE, deliberate. Cancel() now posts, so it no longer takes
            // effect inside the caller's stack frame: the process for the file being
            // started does start, and is then killed. The guarantees that matter are
            // unchanged -- no orphaned child, the batch finishes, and the cancelled file
            // is not counted in Results.
            var h = new Harness(Files(1), maxConcurrency: 1);
            int finished = 0;
            h.Batch.BatchFinished += () => finished++;
            h.Batch.FileStarted += item => h.Batch.Cancel();

            h.Start();

            Assert.Single(h.Factory.Created);
            Assert.True(h.Factory.Created[0].Started);
            Assert.True(h.Factory.Created[0].Killed);

            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Equal(1, finished);
            Assert.True(h.Batch.Finished);
            Assert.Empty(h.Batch.Results);
        }

        [Fact]
        public void AFailedFileIsRecordedAsAFailureAndTheBatchCarriesOn()
        {
            var h = new Harness(Files(2), maxConcurrency: 1);
            h.Start();

            h.Factory.Created[0].EmitStderr("cannot open document");
            h.Factory.Created[0].Finish(1, "stdout", "stderr", "exit");
            h.Dispatcher.RunUntilIdle();

            Assert.Single(h.Batch.Failures);
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(1);

            Assert.Single(h.Batch.Results);
            Assert.Single(h.Batch.Failures);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void EventsCarryTheItemThatIdentifiesTheFile()
        {
            var h = new Harness(Files(2), maxConcurrency: 1);
            var started = new List<BatchItem>();
            var completed = new List<BatchItem>();
            h.Batch.FileStarted += item => started.Add(item);
            h.Batch.FileCompleted += (item, analyzer) => completed.Add(item);

            h.Start();
            h.Complete(0);
            h.Complete(1);

            Assert.Equal(new[] { 0, 1 }, started.Select(i => i.Index).ToArray());
            Assert.Equal(new[] { 0, 1 }, completed.Select(i => i.Index).ToArray());
            Assert.Equal(@"C:\files\f1.pdf", started[0].Filename);
        }

        [Fact]
        public void ADuplicatePathIsAnalyzedOnce()
        {
            // Over-reporting page totals is not cosmetic: customers price their own
            // customers' work from these numbers.
            var factory = new FakePfcToolProcessFactory();
            var dispatcher = new QueueDispatcher();
            var batch = new AnalysisBatch(
                new List<string> { @"C:\files\a.pdf", @"C:\files\A.PDF", @"C:\files\b.pdf" },
                Options(), factory, dispatcher, maxConcurrency: 4);

            Assert.Equal(2, batch.Items.Count);

            batch.Start();
            dispatcher.RunUntilIdle();

            Assert.Equal(2, factory.Created.Count);
        }

        [Fact]
        public void AStormOfSynchronousFailuresDoesNotOverflowTheStack()
        {
            // Go() raises AnalysisComplete on the calling thread when Start throws. The
            // completion is posted, so it cannot re-enter the pump -- this is now
            // structural rather than caught by a flag.
            var factory = new FakePfcToolProcessFactory
            {
                StartThrows = new InvalidOperationException("The system cannot find the file specified")
            };
            var dispatcher = new QueueDispatcher();
            var batch = new AnalysisBatch(Files(1000), Options(), factory, dispatcher, maxConcurrency: 4);

            int finished = 0;
            batch.BatchFinished += () => finished++;

            batch.Start();
            dispatcher.RunUntilIdle();

            Assert.Equal(1, finished);
            Assert.Equal(1000, batch.Failures.Count);
            Assert.Empty(batch.Results);
        }

        [Fact]
        public void AThrowingFileCompletedHandlerDoesNotStallThePool()
        {
            // THREE files at concurrency ONE. The previous version of this test used two
            // files at concurrency two, where the other analyzer's completion did the
            // pumping -- so it passed even with Pump() outside the finally. With a queue
            // behind the pool, a skipped Pump is permanent.
            var h = new Harness(Files(3), maxConcurrency: 1);
            int batchFinishedRaised = 0;
            h.Batch.BatchFinished += () => batchFinishedRaised++;

            bool first = true;
            h.Batch.FileCompleted += (item, analyzer) =>
            {
                if (first) { first = false; throw new InvalidOperationException("boom"); }
            };

            h.Start();
            Assert.Equal(1, h.Factory.Created.Count);

            SucceedOnePage(h.Factory.Created[0]);
            Assert.Throws<InvalidOperationException>(() => h.Dispatcher.RunUntilIdle());

            // The pool must have refilled despite the throw.
            Assert.Equal(2, h.Factory.Created.Count);

            h.Complete(1);
            h.Complete(2);

            Assert.Equal(1, batchFinishedRaised);
            Assert.True(h.Batch.Finished);
        }

        [Fact]
        public void AThrowingFileStartedHandlerDoesNotStrandItsAnalyzer()
        {
            // An analyzer added to `running` must always be started, or nothing will ever
            // deliver a completion for it and the batch can never finish. Go() runs in a
            // finally for exactly this reason.
            var h = new Harness(Files(2), maxConcurrency: 1);
            int batchFinishedRaised = 0;
            h.Batch.BatchFinished += () => batchFinishedRaised++;

            bool first = true;
            h.Batch.FileStarted += item =>
            {
                if (first) { first = false; throw new InvalidOperationException("boom"); }
            };

            h.Batch.Start();
            Assert.Throws<InvalidOperationException>(() => h.Dispatcher.RunUntilIdle());

            // Started despite the throwing subscriber.
            Assert.Single(h.Factory.Created);
            Assert.True(h.Factory.Created[0].Started);

            h.Complete(0);
            h.Complete(1);

            Assert.Equal(1, batchFinishedRaised);
            Assert.True(h.Batch.Finished);
            Assert.Equal(2, h.Batch.Results.Count);
        }

        [Fact]
        public void ProgressIsForwardedWithTheItem()
        {
            var h = new Harness(Files(1), maxConcurrency: 1);
            var progress = new List<Tuple<int, int, int>>();
            h.Batch.FileProgress += (item, completed, total) => progress.Add(Tuple.Create(item.Index, completed, total));

            h.Start();
            h.Factory.Created[0].EmitStdout("PageCount=4 BookmarkCount=0");
            h.Dispatcher.RunUntilIdle();

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

Expected: FAIL to compile — the `AnalysisBatch` constructor does not take an `IDispatcher`.

- [ ] **Step 3: Rewrite AnalysisBatch**

In `app/core/AnalysisBatch.cs`:

Delete the fields `gate`, `pumping`, `pendingCompletions` and their XML comments. Add:

```csharp
        private readonly IDispatcher dispatcher;
```

Replace the class-level XML summary's `<para>` block with:

```csharp
    /// <para>
    /// All state lives on the dispatcher's thread. Analyzer callbacks arrive on thread
    /// pool threads and are posted straight onto it, so there is no lock here and no
    /// re-entrancy guard: Go() completing synchronously posts rather than re-entering the
    /// pump. The dispatcher must never run an action inline; see <see cref="IDispatcher"/>.
    /// </para>
```

Add `IDispatcher dispatcher` to the constructor after `processFactory`, with the null check and assignment alongside the existing ones:

```csharp
            if (dispatcher == null) throw new ArgumentNullException("dispatcher");
            this.dispatcher = dispatcher;
```

Replace `Finished`, `Start`, `Cancel`, `Pump`, `OnAnalyzerProgress` and `OnAnalyzerComplete` with:

```csharp
        public bool Finished { get { return finished; } }

        public void Start()
        {
            dispatcher.Post(Pump);
        }

        /// <summary>
        /// Drains the queue and kills whatever is running. Asynchronous: the work is
        /// dispatched, so it does not take effect inside the caller's stack frame. The
        /// batch finishes once the cancelled analyzers have delivered their completions.
        /// </summary>
        public void Cancel()
        {
            System.Diagnostics.Debug.Print("Cancel batch");

            dispatcher.Post(() =>
            {
                cancelled = true;
                queue.Clear();

                foreach (var analyzer in new List<FileAnalyzer>(running.Keys))
                    analyzer.Cancel();

                // Nothing was running, so no completion will arrive to finish the batch.
                Pump();
            });
        }

        private void Pump()
        {
            while (!cancelled && running.Count < maxConcurrency && queue.Count > 0)
            {
                BatchItem item = queue.Dequeue();
                FileAnalyzer analyzer = CreateAnalyzer(item);
                running.Add(analyzer, item);

                // Go() in a finally: an analyzer that reached `running` MUST be started,
                // or nothing will ever deliver a completion for it and the batch can never
                // finish. Raising FileStarted first preserves the event order the GUI
                // paints from. Go() can raise AnalysisComplete on this very thread when
                // the process fails to start, but that completion is posted, so it cannot
                // re-enter this loop.
                try
                {
                    FileStarted(item);
                }
                finally
                {
                    analyzer.Go();
                }
            }

            if (!finished && running.Count == 0 && queue.Count == 0)
            {
                finished = true;
                BatchFinished();
            }
        }

        private void OnAnalyzerProgress(FileAnalyzer analyzer, int completed, int total)
        {
            dispatcher.Post(() =>
            {
                BatchItem item;
                if (!running.TryGetValue(analyzer, out item))
                    return;

                FileProgress(item, completed, total);
            });
        }

        private void OnAnalyzerComplete(FileAnalyzer analyzer)
        {
            dispatcher.Post(() =>
            {
                BatchItem item;
                if (!running.TryGetValue(analyzer, out item))
                    return;

                running.Remove(analyzer);

                analyzer.ProgressChanged -= OnAnalyzerProgress;
                analyzer.AnalysisComplete -= OnAnalyzerComplete;

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

                // Pump in a finally so a throwing subscriber cannot stall the pool: with a
                // queue behind it, a skipped Pump is permanent.
                try
                {
                    FileCompleted(item, analyzer);
                }
                finally
                {
                    Pump();
                }
            });
        }
```

Leave `Items`, `Results`, `Failures`, `CreateAnalyzer`, `NormalizeForComparison`, the duplicate-collapsing constructor body and the `BatchItem` type exactly as they are.

- [ ] **Step 4: Update the GUI call site**

In `app/gui/ProcessWindow.cs`, pass the dispatcher to the batch. The `dispatcher` field already exists from Task 1, and must be assigned before the batch is constructed:

```csharp
            dispatcher = new ControlDispatcher(this);
            batch = new AnalysisBatch(filenames, options, new PfcToolProcessFactory(), dispatcher);
```

Then remove the `dispatcher.Post(() => { ... });` wrapper from all four handlers — `Batch_FileStarted`, `Batch_FileProgress`, `Batch_FileCompleted` and `Batch_BatchFinished`. The batch now delivers them on the UI thread already, so the handler bodies become the method bodies directly. Do not change what the bodies do.

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

- [ ] **Step 6: Prove the deleted machinery is really gone**

Run:

```bash
grep -n "pumping\|clearedInLock\|pendingCompletions\|lock (gate)" app/core/AnalysisBatch.cs || echo "all removed"
```

Expected: `all removed`.

- [ ] **Step 7: Verify Release build**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add app/core/AnalysisBatch.cs app/gui/ProcessWindow.cs app/tests/AnalysisBatchTests.cs
git commit -F - <<'EOF'
Single-thread the batch behind the dispatcher

Free-threading AnalysisBatch was never required. The property the original
design set out to protect -- analyzer callbacks must not block a thread pool
thread waiting on the UI thread -- is preserved by posting, not by locking, and
the pre-change code drove this pool from the UI thread and could not race at all.

So the pump becomes a plain while loop and the machinery goes: gate and its eight
lock sites, pumping, clearedInLock, pendingCompletions. With it goes the class of
defect it produced. Three findings from review are now structurally impossible
rather than patched:

  - A throwing FileCompleted cannot stall the pool. Pump() is in the finally,
    and the test that covers it uses three files at concurrency one -- the
    previous two-at-two version passed even with the bug, because the other
    analyzer's completion did the pumping.
  - A throwing FileStarted cannot strand an analyzer. Go() runs in a finally, so
    anything that reached `running` is always started and will always complete.
  - The unlocked read of the results list has nowhere to live; the list is
    mutated and read on the same thread.

Re-entrancy is structural now: Go() raising AnalysisComplete on the calling
thread posts instead of re-entering the pump. The 1000-file test still passes,
for a better reason than before.

One deliberate behaviour change. Cancel() posts, so it no longer takes effect
inside the caller's stack frame: a subscriber that cancels from its own
FileStarted handler no longer prevents that file's process from starting -- it
starts and is then killed. No orphaned child, the batch still finishes, and the
file is still not counted. The test asserts the new outcome.

FileAnalyzer is untouched. Process delivers on three thread pool threads whatever
the batch does, so its primitives are inherent, not a consequence of the choice
reversed here.

EOF
```

---

### Task 3: Cut the timing-calibrated test and shrink its survivor

**Files:**
- Modify: `app/tests/FileAnalyzerTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: nothing later tasks rely on.

- [ ] **Step 1: Delete the 400-wave test and its helpers**

From `app/tests/FileAnalyzerTests.cs`, delete these three members entirely, including their XML documentation:

- `CompletesExactlyOnceWhenAStartFailureRacesTheCompletionSignals`
- `WarmUpJit`
- `CalibrateHandicap`

It is roughly 130 lines guarding a single `Interlocked.CompareExchange`, and it does so by measuring both code paths with a `Stopwatch` and burning the difference in `Thread.SpinWait` cycles, mutating the process-wide `ThreadPool` minimum thread count, and spinning about 25,600 work items. Its own documentation records that it misses the regression roughly one run in ten. Machine-timing-calibrated test code is the most maintenance-hostile category there is, and `CompletesExactlyOnceUnderConcurrentSignalDelivery` covers the same guard honestly.

After deleting, confirm no leftovers:

```bash
grep -n "WarmUpJit\|CalibrateHandicap\|SetMinThreads\|SpinWait" app/tests/FileAnalyzerTests.cs || echo "all removed"
```

Expected: `all removed`.

- [ ] **Step 2: Shrink the surviving concurrency test**

In `CompletesExactlyOnceUnderConcurrentSignalDelivery`, change the loop bound from 2000 to 200:

```csharp
            for (int i = 0; i < 200; i++)
```

Update the comment above the loop so the recorded numbers stay honest — it must say 200 iterations, not 2000. If the XML documentation quotes a measured detection rate from the 2000-iteration version, either re-measure at 200 and record the new figure, or state plainly that the figure was measured at 2000 iterations and that the count was reduced afterwards. Do not leave a number that no longer describes what the test does.

- [ ] **Step 3: Run the suite and record the timing**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS. Note the reported duration — it should fall substantially from the ~7 s before this task.

- [ ] **Step 4: Verify the shrunk test still bites**

Temporarily change `Complete()`'s guard in `app/core/FileAnalyzer.cs` from

```csharp
            if (Interlocked.CompareExchange(ref completionRaised, 1, 0) != 0)
```

to the non-atomic

```csharp
            if (completionRaised != 0) return; completionRaised = 1; if (false)
```

Run `CompletesExactlyOnceUnderConcurrentSignalDelivery` ten times and record how many fail. Then restore the file with `git checkout -- app/core/FileAnalyzer.cs`, confirm `git diff` is clean, and re-run to confirm it passes.

Report both counts as raw "N of 10" figures. If detection at 200 iterations is materially worse than it was at 2000, raise the count until it is comparable and report the number you settled on — a faster suite is not worth a test that no longer catches its target.

- [ ] **Step 5: Commit**

```bash
git add app/tests/FileAnalyzerTests.cs
git commit -F - <<'EOF'
Cut the timing-calibrated race test

Roughly 130 lines guarding a single Interlocked.CompareExchange, by measuring
both code paths with a Stopwatch, burning the difference in SpinWait cycles,
mutating the process-wide ThreadPool minimum thread count, and spinning about
25,600 work items -- and its own documentation recorded that it misses the
regression about one run in ten. Machine-timing-calibrated test code is the most
maintenance-hostile category there is, and the price here was most of the suite's
runtime.

CompletesExactlyOnceUnderConcurrentSignalDelivery covers the same guard honestly
with three real threads, and drops from 2000 iterations to 200. Its measured
detection rate is recorded in the commit trail rather than claimed.

EOF
```

Include the measured "N of 10" figures from Step 4 in the commit message body, above the trailer.

---

### Task 4: Stop a protocol-legal page count killing the process

**Files:**
- Modify: `app/core/PfcToolProtocol.cs`, `app/gui/Program.cs`
- Test: `app/tests/PfcToolProtocolTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: nothing later tasks rely on.

Unrelated to threading, but it is the fourth open review finding and blocks merge.

- [ ] **Step 1: Write the failing test**

Add to `app/tests/PfcToolProtocolTests.cs`:

```csharp
        [Fact]
        public void Parse_RejectsAnImplausiblePageCount()
        {
            // PageCount=2000000000 parses as an int, and TPCFile then does
            // new List<TPCFilePage>(pageCount), which throws OutOfMemoryException on the
            // stdout reader thread -- taking the process down and losing the whole batch.
            // Treat an implausible count as a protocol violation so it fails the file,
            // which is what every other malformed line does.
            var line = PfcToolProtocol.Parse("PageCount=2000000000 BookmarkCount=0");

            Assert.Equal(PfcToolLineKind.Unrecognized, line.Kind);
        }

        [Fact]
        public void Parse_AcceptsALargeButPlausiblePageCount()
        {
            var line = PfcToolProtocol.Parse("PageCount=1000000 BookmarkCount=0");

            Assert.Equal(PfcToolLineKind.Header, line.Kind);
            Assert.Equal(1000000, line.PageCount);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
dotnet test app/ProFileCounter.sln --filter PfcToolProtocolTests
```

Expected: FAIL — `Parse_RejectsAnImplausiblePageCount` gets `Header`, not `Unrecognized`.

- [ ] **Step 3: Bound the page count in the parser**

In `app/core/PfcToolProtocol.cs`, add the constant next to the regex fields:

```csharp
        /// <summary>
        /// A page count above this is treated as a protocol violation rather than a
        /// number. TPCFile passes the count to a List capacity, and an implausible one
        /// throws OutOfMemoryException on the stdout reader thread, where nothing catches
        /// it and the process dies. The bound is arbitrary but far above any real
        /// document -- the largest file in the problem-file corpus is in the low
        /// thousands of pages, and a capacity of a million allocates about 8 MB rather
        /// than throwing.
        /// </summary>
        private const int MaxPlausiblePageCount = 1000000;
```

Then, in the header branch of `Parse`, after the two values have been parsed and before returning `PfcToolLine.Header(...)`:

```csharp
                if (pageCount > MaxPlausiblePageCount)
                    return PfcToolLine.Unrecognized();
```

Match the surrounding style: the header branch currently uses `TryParse` into locals, so add the check against those locals.

- [ ] **Step 4: Add the last-resort unhandled exception handler**

In `app/gui/Program.cs`, inside `Main`, before `Application.Run(new MainForm())`:

```csharp
			// Analyzer callbacks run on thread pool threads. An exception there is not
			// routed through Application.ThreadException -- it terminates the process, and
			// the user loses the whole batch with no message. Several classes in this
			// codebase reason carefully about not throwing on those threads; this is the
			// backstop for the case one of them is wrong.
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				try
				{
					MessageBox.Show(
						"ProFile Counter hit an unexpected error and has to close.\n\n" + e.ExceptionObject,
						"Unexpected error",
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
				}
				catch { /* nothing useful left to do at this point */ }
			};
```

Note that this file is indented with tabs, not spaces, and uses CRLF line endings — match both. `using System;` and `using System.Windows.Forms;` are already present; do not add duplicates.

This does not prevent termination — a `.NET Framework` unhandled exception on a background thread is fatal — but it turns a silent disappearance into a message the user can report.

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
dotnet test app/ProFileCounter.sln
```

Expected: PASS.

- [ ] **Step 6: Verify Release build**

Run:

```bash
dotnet build app/ProFileCounter.sln -c Release
```

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add app/core/PfcToolProtocol.cs app/gui/Program.cs app/tests/PfcToolProtocolTests.cs
git commit -F - <<'EOF'
Stop a protocol-legal page count killing the process

Parse was hardened to return Unrecognized rather than throw, on the grounds that
an exception out of OnOutputLine reaches a thread pool thread where nothing
catches it and the process dies. PageCount=2000000000 defeated that one call
later: it parses cleanly, and TPCFile's new List<TPCFilePage>(pageCount) throws
OutOfMemoryException from the same place.

Treat an implausible count as a protocol violation, which fails the file the same
way every other malformed line does. The bound is far above any real document.

Program.Main also gains the AppDomain.UnhandledException handler that roughly six
comments in this codebase implicitly assume exists. It cannot prevent
termination, but it turns a silent disappearance into something the user can
report.

EOF
```

---

### Task 5: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything.
- Produces: nothing.

- [ ] **Step 1: Correct the pipeline description**

`CLAUDE.md`'s "Processing pipeline (C# side)" section currently describes the free-threaded batch. Find and update these specific claims — read the file rather than trusting line numbers:

- Step 2 says every batch event is marshalled with `UiThread.BeginInvokeIfRequired`. `UiThread` no longer exists. It should say that `ProcessWindow` supplies a `ControlDispatcher` and that the batch already delivers events on the UI thread, so the handlers do not marshal.
- Step 3 says `AnalysisBatch` "guards its own state with a lock and raises events on whatever thread completed the work" and has "an explicit re-entrancy guard". Replace with: all state lives on the injected `IDispatcher`'s thread; analyzer callbacks are posted onto it; there is no lock and no re-entrancy guard, because `Post` never runs inline.
- Add one sentence recording the contract: an `IDispatcher` implementation must never run an action inline, or the re-entrancy the posting prevents comes back.

- [ ] **Step 2: Correct the test-count claim if present**

Search for any hard-coded test count in `CLAUDE.md` and either update it or reword it so it does not go stale:

```bash
grep -n "[0-9]\+ tests\|tests are in" CLAUDE.md
```

- [ ] **Step 3: Verify nothing else in CLAUDE.md is now wrong**

Read the whole file and check for any other reference to `UiThread`, to the batch's lock, or to marshalling in `ProcessWindow`'s handlers:

```bash
grep -n "UiThread\|BeginInvokeIfRequired\|re-entrancy\|whatever thread" CLAUDE.md
```

Fix anything that survives. Do NOT change anything about the native build — the v142 toolset, MSB8020, the static CRT, `static-crt.props`, the mupdf submodule, the patches, `apply-patches.cmd`, or "What pfc-tool will and won't open". None of that moved. Do not change the documented `pfc-tool.exe` wire protocol either.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -F - <<'EOF'
Update the pipeline docs for the dispatcher

CLAUDE.md described a batch that guards its own state with a lock, raises events
on whatever thread completed the work, and needs an explicit re-entrancy guard,
and told the reader to marshal with a UiThread helper that no longer exists.
All of that is now the first thing a reader would act on wrongly.

EOF
```

---

## Final verification

- [ ] **Automated**

```bash
dotnet test app/ProFileCounter.sln
```
Expected: all pass, materially faster than the ~7 s before Task 3.

```bash
dotnet build app/ProFileCounter.sln -c Release
```
Expected: `0 Warning(s)`, `0 Error(s)`.

```bash
.\build.ps1 -Configuration Release
```
Expected: both solutions build.

- [ ] **Re-run the adversarial review's three probes.** Each targeted a defect this plan removes; each must now fail to reproduce.
  1. Three files at concurrency one with the first `FileCompleted` throwing: the pool must refill and the batch must finish. Covered by `AThrowingFileCompletedHandlerDoesNotStallThePool`.
  2. A throwing `FileStarted`: the analyzer must be started and the batch must finish. Covered by `AThrowingFileStartedHandlerDoesNotStrandItsAnalyzer`.
  3. The unlocked `Results` read: no longer expressible, since `results` is mutated and read on one thread. Confirm by inspection that `AnalysisBatch` contains no `lock` at all.

- [ ] **End-to-end in the real GUI**, driven manually, because `ProcessWindow` has no automated coverage and `dotnet test` does not compile it:
  - Launch `app\x64\Release\ProFile Counter.exe`. No "Default settings are loaded." dialog; page sizes intact.
  - Drag several files from `test files\` at once. Progress bars advance, completed files leave the grid, failures show dark red with error text, summary totals appear.
  - Re-drop an already-accepted file: it must be refused with the "already in the list" message.
  - Drop a different file: it must process and be added.
  - Cancel a batch by closing the process window mid-run. It must close, with no orphaned `pfc-tool.exe` afterwards (`Get-Process pfc-tool`).

- [ ] **Push and update PR #6.** Do not merge.

## Self-review notes

Spec coverage checked against `specs/2026-08-30-batch-threading-model-design.md`: the dispatcher interface, `ControlDispatcher`, `QueueDispatcher` and the deletion of `UiThread` → Task 1. The rewritten `AnalysisBatch`, the deleted machinery, the `try/finally` around `FileStarted` and `FileCompleted`, the deleted `started = !cancelled` re-check, the asynchronous `Cancel`, and `ProcessWindow`'s changes → Task 2. Deleted and shrunk tests → Task 3. The `PageCount` bound and the unhandled-exception handler → Task 4. Docs → Task 5. The spec's note that `FileAnalyzer` is untouched is enforced by the Global Constraints and by Task 3 restoring the file after its mutation check.

Type consistency: the `AnalysisBatch` constructor signature in Task 2's Interfaces block matches every call site written in Task 2's tests and in the `ProcessWindow` edit. `QueueDispatcher.RunUntilIdle()` and `PendingCount` are used in Tasks 1 and 2 exactly as declared in Task 1.
