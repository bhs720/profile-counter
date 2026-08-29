using System;
using System.Collections.Generic;
using TIFPDFCounter;

namespace TIFPDFCounter.Tests
{
    /// <summary>
    /// A process that never exists. Tests drive the three completion signals -- stdout
    /// end-of-file, stderr end-of-file and exit -- independently and in any order,
    /// because the ordering between them is the thing most worth testing.
    /// </summary>
    public sealed class FakePfcToolProcess : IPfcToolProcess
    {
        public event Action<string> OutputLineReceived = delegate { };
        public event Action OutputEnded = delegate { };
        public event Action<string> ErrorLineReceived = delegate { };
        public event Action ErrorEnded = delegate { };
        public event Action<int> Exited = delegate { };

        public string ToolPath { get; private set; }
        public string Arguments { get; private set; }
        public bool Started { get; private set; }
        public bool Killed { get; private set; }
        public bool Disposed { get; private set; }

        /// <summary>When set, Start() throws it -- the missing pfc-tool.exe case.</summary>
        public Exception StartThrows { get; set; }

        /// <summary>
        /// When set, Start() marks the process Started and then throws it -- models
        /// PfcToolProcess.Start(): process.Start() succeeds (the child is live and
        /// EnableRaisingEvents is already set from the constructor) but a later step,
        /// e.g. BeginErrorReadLine(), fails. Unlike StartThrows, signals can legitimately
        /// arrive afterwards because the child process is genuinely running.
        /// </summary>
        public Exception StartThrowsAfterLaunch { get; set; }

        public FakePfcToolProcess(string toolPath, string arguments)
        {
            ToolPath = toolPath;
            Arguments = arguments;
        }

        public void Start()
        {
            if (StartThrowsAfterLaunch != null)
            {
                Started = true;
                throw StartThrowsAfterLaunch;
            }

            if (StartThrows != null)
                throw StartThrows;

            Started = true;
        }

        public void Kill()
        {
            Killed = true;
        }

        public void Dispose()
        {
            Disposed = true;
        }

        public void EmitStdout(string line) { OutputLineReceived(line); }
        public void EmitStderr(string line) { ErrorLineReceived(line); }
        public void EndStdout() { OutputEnded(); }
        public void EndStderr() { ErrorEnded(); }
        public void Exit(int exitCode) { Exited(exitCode); }

        /// <summary>Delivers all three completion signals in the given order.</summary>
        public void Finish(int exitCode, params string[] signalOrder)
        {
            foreach (string signal in signalOrder)
            {
                switch (signal)
                {
                    case "stdout": EndStdout(); break;
                    case "stderr": EndStderr(); break;
                    case "exit": Exit(exitCode); break;
                    default: throw new ArgumentException("Unknown signal: " + signal);
                }
            }
        }
    }

    public sealed class FakePfcToolProcessFactory : IPfcToolProcessFactory
    {
        private readonly List<FakePfcToolProcess> created = new List<FakePfcToolProcess>();

        /// <summary>When set, every created process throws this from Start().</summary>
        public Exception StartThrows { get; set; }

        public IReadOnlyList<FakePfcToolProcess> Created { get { return created; } }

        public FakePfcToolProcess Last { get { return created[created.Count - 1]; } }

        public IPfcToolProcess Create(string toolPath, string arguments)
        {
            var process = new FakePfcToolProcess(toolPath, arguments) { StartThrows = StartThrows };
            lock (created) { created.Add(process); }
            return process;
        }
    }
}
