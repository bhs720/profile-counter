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
