using System;

namespace TIFPDFCounter
{
    /// <summary>
    /// One pfc-tool.exe child process, as FileAnalyzer needs to see it.
    /// <para>
    /// The three completion signals are separate events rather than one "finished"
    /// callback because analysis is finished only once the process has exited AND both
    /// redirected streams have signalled end-of-file. Exit can fire before the
    /// asynchronous readers have delivered their final lines, and completing early
    /// truncates the very stderr message explaining a failure.
    /// </para>
    /// </summary>
    public interface IPfcToolProcess : IDisposable
    {
        /// <summary>One line of stdout. Never null; end-of-file is <see cref="OutputEnded"/>.</summary>
        event Action<string> OutputLineReceived;

        /// <summary>End of stdout.</summary>
        event Action OutputEnded;

        /// <summary>One line of stderr. Never null; end-of-file is <see cref="ErrorEnded"/>.</summary>
        event Action<string> ErrorLineReceived;

        /// <summary>End of stderr.</summary>
        event Action ErrorEnded;

        /// <summary>
        /// The process exited, carrying its exit code. Reading the code here rather than
        /// at completion time is what lets FileAnalyzer stop guessing: it used to read
        /// Process.ExitCode inside a try/catch that treated any failure as exit code 0,
        /// that is, as success.
        /// </summary>
        event Action<int> Exited;

        /// <summary>Starts the process. Throws if it cannot be started.</summary>
        void Start();

        /// <summary>Kills the process if it is still running. Never throws.</summary>
        void Kill();
    }

    public interface IPfcToolProcessFactory
    {
        IPfcToolProcess Create(string toolPath, string arguments);
    }
}
