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
