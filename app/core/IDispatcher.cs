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
        /// Implementations MUST ALSO NOT run two posted actions concurrently with each
        /// other. <see cref="AnalysisBatch"/> holds no lock of its own: every piece of its
        /// state -- its queue, its running set, its results and failures, whether it has
        /// been cancelled, whether it has finished -- is mutated only from inside a posted
        /// action, on the assumption that exactly one such action is ever running at a
        /// time. Posting via <c>ThreadPool.QueueUserWorkItem</c>, for instance, satisfies
        /// "not inline" while violating this: two actions posted close together can run on
        /// two different pool threads at once, and two analyzers completing at the same
        /// moment would then be free to mutate that state simultaneously and to call the
        /// batch's internal pump concurrently, which can double-start a file or raise
        /// BatchFinished twice. The two implementations in this codebase satisfy this
        /// differently -- <see cref="ControlDispatcher"/> by serializing through the
        /// WinForms message loop, and the tests' QueueDispatcher by being drained from a
        /// single thread -- but any implementation must guarantee it somehow.
        /// </para>
        /// <para>
        /// Dropping the action is a valid outcome when the target can no longer run it --
        /// a closed window, for instance. Running it anyway on the wrong thread, or
        /// alongside another posted action, is not.
        /// </para>
        /// </summary>
        void Post(Action action);
    }
}
