using System;
using System.Diagnostics;
using System.Text;

namespace TIFPDFCounter
{
    /// <summary>
    /// The real thing: a redirected pfc-tool.exe child process.
    /// </summary>
    public sealed class PfcToolProcess : IPfcToolProcess
    {
        private Process process;

        public event Action<string> OutputLineReceived = delegate { };
        public event Action OutputEnded = delegate { };
        public event Action<string> ErrorLineReceived = delegate { };
        public event Action ErrorEnded = delegate { };
        public event Action<int> Exited = delegate { };

        public PfcToolProcess(string toolPath, string arguments)
        {
            process = new Process();
            process.StartInfo.FileName = toolPath;

            // Pass the arguments through unchanged. This used to be re-encoded as
            // Encoding.Default.GetString(Encoding.UTF8.GetBytes(args)), which corrupted
            // every non-ASCII filename: .NET hands Arguments to CreateProcessW as UTF-16,
            // so mangling the string first simply put mojibake on the command line.
            // pfc-tool.exe reads the real UTF-16 command line through wmain and converts
            // it to the UTF-8 that mupdf expects.
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // pfc-tool.exe writes UTF-8: filenames reach mupdf as UTF-8 and come back
            // inside its error text. Decoding that as the ANSI code page turned a
            // copyright sign in a failing path into "?" in the results grid, which reads
            // as a second fault rather than as the one being reported.
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.StartInfo.UseShellExecute = false;

            process.OutputDataReceived += (sender, e) =>
            {
                // Only null means end-of-file; an empty line is ordinary data. Treating
                // both alike would signal completion twice and could finish the file
                // before stderr had drained.
                if (e.Data == null)
                    OutputEnded();
                else
                    OutputLineReceived(e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                    ErrorEnded();
                else
                    ErrorLineReceived(e.Data);
            };

            process.Exited += (sender, e) => Exited(ReadExitCode());
            process.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Reads the exit code at the only moment it is reliably available: inside the
        /// Exited handler, before anything disposes the process. The fallback exists so
        /// that a disposal race cannot throw on a thread pool thread and take the
        /// application down with it; it is not the routine path it used to be.
        /// </summary>
        private int ReadExitCode()
        {
            try
            {
                Process p = process;
                return p == null ? 0 : p.ExitCode;
            }
            catch
            {
                return 0;
            }
        }

        public void Start()
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
        }

        public void Kill()
        {
            Process p = process;
            if (p == null)
                return;

            try
            {
                if (!p.HasExited)
                    p.Kill();
            }
            catch { /* swallow -- the process may have exited or been disposed already */ }
        }

        public void Dispose()
        {
            Process p = process;
            process = null;
            if (p == null)
                return;

            try { p.Close(); p.Dispose(); }
            catch { /* nothing useful left to do at this point */ }
        }
    }

    public sealed class PfcToolProcessFactory : IPfcToolProcessFactory
    {
        public IPfcToolProcess Create(string toolPath, string arguments)
        {
            return new PfcToolProcess(toolPath, arguments);
        }
    }
}
