#nullable enable

// Windows player only, like the tests that use it.
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace JonghyunKim.NativeToolkit.Tests
{
    /// <summary>
    /// Starts a helper process (PowerShell) for the player tests and reads what it prints, and
    /// answers the questions about processes the tests ask, without System.Diagnostics.Process.
    /// <para>
    /// IL2CPP players do not support Process: Start throws Win32Exception ("Native error= Success")
    /// and MainModule is null. The first IL2CPP run (2026-09-27) failed every test that starts a
    /// helper or reads the player's path there, before it reached what the test is about. These
    /// calls go to kernel32 directly and behave the same on Mono and IL2CPP.
    /// </para>
    /// <para>
    /// Each output stream is read on its own background thread, as UTF-8, and handed on one line at
    /// a time without the line break; standard output is also kept whole for
    /// <see cref="StandardOutput"/>. The helper's standard input is NUL and it gets no window.
    /// </para>
    /// </summary>
    internal sealed class WindowsTestProcess : IDisposable
    {
        /// <summary>How long <see cref="WaitForExit"/> waits by default.</summary>
        internal const int DefaultWaitMilliseconds = 10000;

        /// <summary>Room for any Win32 path, long paths included.</summary>
        private const int PathCapacity = 32768;

        private IntPtr _process;
        private readonly Thread _outputReader;
        private readonly Thread _errorReader;
        private readonly MemoryStream _output;

        private WindowsTestProcess(IntPtr process, uint id, Thread outputReader, Thread errorReader, MemoryStream output)
        {
            _process = process;
            Id = (int)id;
            _outputReader = outputReader;
            _errorReader = errorReader;
            _output = output;
        }

        internal int Id { get; }

        internal bool HasExited => WaitForSingleObject(Handle, 0) == WaitObject0;

        internal int ExitCode
        {
            get
            {
                if (!HasExited) throw new InvalidOperationException("the process has not exited");
                if (!GetExitCodeProcess(Handle, out uint code)) throw Failure("GetExitCodeProcess");
                return (int)code;
            }
        }

        /// <summary>Whether both streams reached their end, which they do once the process has exited.</summary>
        internal bool OutputComplete => !_outputReader.IsAlive && !_errorReader.IsAlive;

        /// <summary>Everything read from standard output so far.</summary>
        internal string StandardOutput
        {
            get
            {
                lock (_output) return Encoding.UTF8.GetString(_output.GetBuffer(), 0, (int)_output.Length);
            }
        }

        private IntPtr Handle =>
            _process != IntPtr.Zero ? _process : throw new ObjectDisposedException(nameof(WindowsTestProcess));

        /// <summary>
        /// Starts <paramref name="fileName"/> (looked up on PATH as CreateProcess does) with the
        /// arguments. The line callbacks run on the reader threads.
        /// </summary>
        internal static WindowsTestProcess Start(string fileName, string arguments,
            Action<string>? outputLine = null, Action<string>? errorLine = null)
        {
            var inherit = new SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf<SecurityAttributes>(),
                InheritHandle = 1,
            };
            IntPtr input = CreateFileW("NUL", GenericRead, FileShareReadWrite, ref inherit, OpenExisting, 0, IntPtr.Zero);
            if (input == InvalidHandle) throw Failure("CreateFileW(NUL)");

            IntPtr outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero, errorRead = IntPtr.Zero, errorWrite = IntPtr.Zero;
            try
            {
                OpenPipe(ref inherit, out outputRead, out outputWrite);
                OpenPipe(ref inherit, out errorRead, out errorWrite);
                var startup = new StartupInfo
                {
                    Size = (uint)Marshal.SizeOf<StartupInfo>(),
                    Flags = StartfUseStdHandles,
                    StdInput = input,
                    StdOutput = outputWrite,
                    StdError = errorWrite,
                };
                var commandLine = new StringBuilder($"\"{fileName}\" {arguments}");
                if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, true, CreateNoWindow,
                        IntPtr.Zero, null, ref startup, out ProcessInformation started))
                {
                    throw Failure($"CreateProcessW({fileName})");
                }
                CloseHandle(started.Thread);

                try
                {
                    // Each reader thread owns its read end once it has started.
                    var output = new MemoryStream();
                    Thread outputReader = Read(outputRead, outputLine, output);
                    outputRead = IntPtr.Zero;
                    Thread errorReader = Read(errorRead, errorLine, null);
                    errorRead = IntPtr.Zero;
                    return new WindowsTestProcess(started.Process, started.ProcessId, outputReader, errorReader, output);
                }
                catch
                {
                    // Nobody would stop a helper whose output is not being read.
                    TerminateProcess(started.Process, 1);
                    CloseHandle(started.Process);
                    throw;
                }
            }
            finally
            {
                // The helper holds its own copies; closing ours is what lets the pipes end when it exits.
                CloseHandle(input);
                if (outputWrite != IntPtr.Zero) CloseHandle(outputWrite);
                if (errorWrite != IntPtr.Zero) CloseHandle(errorWrite);
                if (outputRead != IntPtr.Zero) CloseHandle(outputRead);
                if (errorRead != IntPtr.Zero) CloseHandle(errorRead);
            }
        }

        /// <summary>
        /// Waits until the process has exited and its output has been read, as Process.WaitForExit
        /// does, but for at most <paramref name="milliseconds"/> (a negative value waits as long as it
        /// takes). Returns whether both happened.
        /// </summary>
        internal bool WaitForExit(int milliseconds = DefaultWaitMilliseconds)
        {
            DateTime? deadline = milliseconds < 0 ? null : DateTime.UtcNow.AddMilliseconds(milliseconds);
            if (WaitForSingleObject(Handle, milliseconds < 0 ? Infinite : (uint)milliseconds) != WaitObject0) return false;
            return _outputReader.Join(Remaining(deadline)) && _errorReader.Join(Remaining(deadline));
        }

        /// <summary>
        /// Stops the process if it is still running, then waits as <see cref="WaitForExit"/> does, so
        /// no line of its output arrives after this returns (unless something it started holds the
        /// pipes).
        /// </summary>
        internal void Kill()
        {
            if (!HasExited && !TerminateProcess(Handle, 1))
            {
                InvalidOperationException failure = Failure("TerminateProcess");
                // It may have exited by itself in between, which TerminateProcess answers with access denied.
                if (!HasExited) throw failure;
            }
            WaitForExit();
        }

        /// <summary>Lets the process go; it is not stopped. The reader threads end with its output.</summary>
        public void Dispose()
        {
            if (_process == IntPtr.Zero) return;
            CloseHandle(_process);
            _process = IntPtr.Zero;
        }

        // ── Questions about processes ────────────────────────────────────────────

        /// <summary>The player's own executable, which Process.MainModule.FileName gives on Mono.</summary>
        internal static string CurrentExecutablePath()
        {
            var path = new StringBuilder(PathCapacity);
            uint length = GetModuleFileNameW(IntPtr.Zero, path, (uint)path.Capacity);
            if (length == 0 || length >= path.Capacity) throw Failure("GetModuleFileNameW");
            return path.ToString(0, (int)length);
        }

        /// <summary>How many running processes run this player's executable, this one included.</summary>
        internal static int CountRunningCopiesOfThisPlayer()
        {
            string? self = ImagePath(GetCurrentProcessId());
            if (self == null) throw Failure("QueryFullProcessImageNameW(this player)");

            // A full array may have been cut short, so ask again with more room until it is not full.
            uint[] ids;
            uint bytes;
            for (int capacity = 1024; ; capacity *= 2)
            {
                ids = new uint[capacity];
                if (!K32EnumProcesses(ids, (uint)(ids.Length * sizeof(uint)), out bytes)) throw Failure("EnumProcesses");
                if (bytes < ids.Length * sizeof(uint)) break;
            }
            int count = 0;
            for (int i = 0; i < bytes / sizeof(uint); i++)
            {
                if (string.Equals(ImagePath(ids[i]), self, StringComparison.OrdinalIgnoreCase)) count++;
            }
            return count;
        }

        /// <summary>The full path of a process's executable, or null when it cannot be asked (gone, or protected).</summary>
        internal static string? ImagePath(uint processId)
        {
            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return null;
            try
            {
                var path = new StringBuilder(PathCapacity);
                uint size = (uint)path.Capacity;
                return QueryFullProcessImageNameW(process, 0, path, ref size) ? path.ToString(0, (int)size) : null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        // ── Reading ──────────────────────────────────────────────────────────────

        private static Thread Read(IntPtr pipe, Action<string>? line, MemoryStream? whole)
        {
            var thread = new Thread(() =>
            {
                var buffer = new byte[4096];
                var pending = new MemoryStream();
                try
                {
                    // ReadFile fails with ERROR_BROKEN_PIPE once every write end is closed.
                    while (ReadFile(pipe, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero) && read > 0)
                    {
                        if (whole != null)
                        {
                            lock (whole) whole.Write(buffer, 0, (int)read);
                        }
                        // '\n' never occurs inside a multi-byte UTF-8 sequence, so splitting bytes there is safe.
                        for (int i = 0; i < read; i++)
                        {
                            if (buffer[i] == (byte)'\n') Emit(pending, line);
                            else pending.WriteByte(buffer[i]);
                        }
                    }
                    if (pending.Length > 0) Emit(pending, line);
                }
                finally
                {
                    CloseHandle(pipe);
                }
            })
            {
                IsBackground = true,
                Name = "NTK test process reader",
            };
            thread.Start();
            return thread;
        }

        private static void Emit(MemoryStream pending, Action<string>? line)
        {
            string text = Encoding.UTF8.GetString(pending.GetBuffer(), 0, (int)pending.Length).TrimEnd('\r');
            pending.SetLength(0);
            line?.Invoke(text);
        }

        private static void OpenPipe(ref SecurityAttributes inherit, out IntPtr read, out IntPtr write)
        {
            if (!CreatePipe(out read, out write, ref inherit, 0)) throw Failure("CreatePipe");
            // Only the write end goes to the helper.
            if (!SetHandleInformation(read, HandleFlagInherit, 0)) throw Failure("SetHandleInformation");
        }

        private static int Remaining(DateTime? deadline) =>
            deadline is { } at ? Math.Max(0, (int)(at - DateTime.UtcNow).TotalMilliseconds) : Timeout.Infinite;

        private static InvalidOperationException Failure(string call) =>
            new($"{call} failed with Win32 error {Marshal.GetLastWin32Error()}");

        // ── kernel32 ─────────────────────────────────────────────────────────────

        private const uint GenericRead = 0x80000000;
        private const uint FileShareReadWrite = 0x00000003;
        private const uint OpenExisting = 3;
        private const uint HandleFlagInherit = 0x00000001;
        private const uint StartfUseStdHandles = 0x00000100;
        private const uint CreateNoWindow = 0x08000000;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint WaitObject0 = 0;
        private const uint Infinite = 0xFFFFFFFF;
        private static readonly IntPtr InvalidHandle = new(-1);

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public uint Length;
            public IntPtr SecurityDescriptor;
            public int InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfo
        {
            public uint Size;
            public IntPtr Reserved;
            public IntPtr Desktop;
            public IntPtr Title;
            public uint X;
            public uint Y;
            public uint XSize;
            public uint YSize;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public ushort ShowWindow;
            public ushort Reserved2Size;
            public IntPtr Reserved2;
            public IntPtr StdInput;
            public IntPtr StdOutput;
            public IntPtr StdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(string? applicationName, StringBuilder commandLine,
            IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint creationFlags,
            IntPtr environment, string? currentDirectory, ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string fileName, uint desiredAccess, uint shareMode,
            ref SecurityAttributes securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr readPipe, out IntPtr writePipe,
            ref SecurityAttributes pipeAttributes, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr file, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder fileName, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool K32EnumProcesses([Out] uint[] processIds, uint size, out uint bytesReturned);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder exeName, ref uint size);
    }
}
#endif
