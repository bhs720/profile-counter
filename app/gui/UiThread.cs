using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace TIFPDFCounter
{
    public static class UiThread
    {
        public static void InvokeIfRequired(Control ctrl, MethodInvoker action)
        {
            if (ctrl.InvokeRequired)
            {
                ctrl.Invoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// Marshals <paramref name="action"/> onto the UI thread without waiting for it.
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
            if (ctrl.IsHandleCreated)
            {
                try
                {
                    ctrl.BeginInvoke(action);
                }
                // Covers ObjectDisposedException too, which derives from this: the window
                // can be closed between the post and its delivery, and neither case is an
                // error.
                catch (InvalidOperationException) { }
            }
            else
            {
                // No handle to post to, so there is no message loop to wait for. Callers
                // reach this only before ProcessWindow_Load, where no analyzer exists yet.
                action();
            }
        }
    }
}
