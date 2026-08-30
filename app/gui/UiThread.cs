using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public static class UiThread
    {
        /// <summary>
        /// Marshals <paramref name="action"/> onto the UI thread without waiting for it. A
        /// callback that arrives after the window has already closed is dropped rather
        /// than run.
        /// <para>
        /// Use this instead of <see cref="InvokeIfRequired"/> for anything raised from a
        /// <see cref="FileAnalyzer"/> callback. Those run on thread pool threads, and
        /// Control.Invoke would block one of them until the UI thread caught up. The UI
        /// thread does real work per completed file -- removing a grid row, scanning the
        /// whole grid, and starting the next child process -- so with a full worker pool
        /// the blocked threads are the very thread pool threads the redirected stream
        /// readers need in order to deliver their end-of-file callbacks. Completion for
        /// other files then stalls behind the UI, which is how a perfectly healthy file
        /// ended up reported as having timed out.
        /// </para>
        /// <para>
        /// A batch keeps delivering FileProgress/FileCompleted to whatever files are still
        /// in flight even after the window that started it has closed -- BatchFinished
        /// only waits for FileCompleted, not for a late FileProgress from an analyzer that
        /// has not yet reached completion. By the time such a callback arrives the control
        /// may be disposed, mid-Dispose, or (briefly, after ShowDialog returns and before
        /// the caller's `using` block disposes it) still not disposed but with its handle
        /// already gone -- <see cref="Control.IsDisposed"/> is false in that window too.
        /// Dropping the action whenever the control is not genuinely usable is correct:
        /// there is nothing left worth repainting, and the alternative -- running it
        /// inline on a thread pool thread because there is no message loop to post to --
        /// would touch a dead or dying DataGridView from off the UI thread, an unhandled
        /// exception that takes the whole process down.
        /// </para>
        /// </summary>
        public static void BeginInvokeIfRequired(Control ctrl, MethodInvoker action)
        {
            // Post even when the caller is already on the UI thread. A FileAnalyzer can
            // complete synchronously -- Go() catches a failed Process.Start and raises
            // AnalysisComplete on the calling thread -- and running the handler inline
            // would re-enter ProcessWindow.NextFile from inside NextFile, nesting one
            // frame set per queued file. A missing pfc-tool.exe and a few hundred dropped
            // files would then overflow the stack, which .NET cannot catch.
            //
            // Posting also keeps exceptions thrown by the action out of the catch below,
            // where they were being swallowed; delivered from the message loop they
            // surface normally.
            if (ctrl.IsDisposed || ctrl.Disposing || !ctrl.IsHandleCreated)
                return;

            try
            {
                ctrl.BeginInvoke(action);
            }
            // Covers ObjectDisposedException too, which derives from this: the window can
            // still be disposed between the guard above and this call, and neither case is
            // an error.
            catch (InvalidOperationException) { }
        }
    }
}
