using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XI.Common
{
    // debug::init / setCoreDumpsEnabled (debug_windows.cpp) for the C# host: an exception no code caught, or the
    // ForceCrash binding, is reported as a crash; with main.GENERATE_CORE_DUMP on, a minidump of the process is
    // written first to dmp/tombstone_<pid>_<yyyyMMdd_HHmmss>.dmp under the server root, the C++ SEH filter's name.
    // Deliberate differences: the dump is MiniDumpWithPrivateReadWriteMemory (with the data segments, handles and
    // thread info), not MiniDumpNormal, since a .NET dump without the managed heap cannot be read by the .NET
    // debuggers; it is written from the runtime's unhandled exception event, not an SEH filter, so it holds no
    // exception record; on Linux and macOS (no dbghelp) nothing is written and the runtime's own
    // DOTNET_DbgEnableMiniDump settings apply; no tombstone text file is written: the crash line, the exception in it,
    // goes to Log (the host's critical logger), since the runtime prints nothing before TerminateProcess ends it.
    // The process then ends at once with TerminateProcess, as the C++ filter's EXCEPTION_EXECUTE_HANDLER ends it: no
    // Windows Error Reporting dialog or report for a watchdog to wait on; the exit code is the exception's
    // (0xC0000005 for ForceCrash's access violation, 0xE0434352, the CLR's exception code, for a .NET exception).
    public static class CrashDump
    {
        private static volatile bool enabled_;
        private static string root_ = ".";
        private static int reported_;

        // where the crash report's line goes; the host sets it to its critical logger
        public static Action<string> Log = s => Console.Error.WriteLine(s);

        // debug::init: the unhandled exception handler
        public static void Install(string root)
        {
            root_ = root;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Report($"unhandled-exception: {e.ExceptionObject}");
                End(ClrExceptionCode);
            };
        }

        // debug::setCoreDumpsEnabled (main.GENERATE_CORE_DUMP)
        public static bool Enabled
        {
            get => enabled_;
            set => enabled_ = value;
        }

        // the SEH codes the process ends with: EXCEPTION_ACCESS_VIOLATION, and the CLR's for a managed exception
        public const uint AccessViolationCode = 0xC0000005, ClrExceptionCode = 0xE0434352;

        // where the process ends: TerminateProcess with the code; settable for the tests
        public static Action<uint> Terminate = DefaultTerminate;

        // crash(): the ForceCrash binding's write through a null pointer, reported as an access violation and the
        // process ended at once (no finally block or exit handler runs, as none runs in a crash)
        public static void Force()
        {
            Report("EXCEPTION_ACCESS_VIOLATION: ForceCrash");
            End(AccessViolationCode);
        }

        private static void End(uint code) => Terminate(code);

        // TerminateProcess(GetCurrentProcess(), code); elsewhere than Windows Environment.FailFast (no WER there)
        private static void DefaultTerminate(uint code)
        {
            if (OperatingSystem.IsWindows()) TerminateProcess(GetCurrentProcess(), code);
            Environment.FailFast($"crash 0x{code:X8}");
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

        // debug::beginCrashReport: only the first report writes, the dump first when enabled
        private static void Report(string reason)
        {
            if (System.Threading.Interlocked.Exchange(ref reported_, 1) != 0) return;
            string dump = enabled_ ? WriteMiniDump(root_) : null;
            Log($"crash: {reason}" + (dump != null ? $" (minidump {dump})" : ""));
        }

        // writeMiniDump: the dump under root's dmp/, its path; null when none was written
        public static string WriteMiniDump(string root)
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                string dir = Path.Combine(root, "dmp");
                Directory.CreateDirectory(dir);
                using var self = Process.GetCurrentProcess();
                string path = Path.Combine(dir, $"tombstone_{self.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.dmp");
                // a dump of the process itself can come out partial (ERROR_PARTIAL_COPY: memory changed under
                // the read, the GC's): written again, up to three times
                for (int attempt = 1; ; attempt++)
                {
                    int error;
                    using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (MiniDumpWriteDump(self.Handle, (uint)self.Id, file.SafeFileHandle, DumpType, IntPtr.Zero,
                                IntPtr.Zero, IntPtr.Zero))
                            return path;
                        error = Marshal.GetLastWin32Error();
                    }
                    if ((error & 0xFFFF) != ErrorPartialCopy || attempt == 3) throw new Win32Exception(error);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception
                                          or DllNotFoundException)
            {
                Log($"crash: the minidump was not written: {e.Message}");
                return null;
            }
        }

        // MiniDumpWithDataSegs | MiniDumpWithHandleData | MiniDumpWithUnloadedModules |
        // MiniDumpWithPrivateReadWriteMemory | MiniDumpWithThreadInfo: dotnet-dump's "heap" dump
        private const int ErrorPartialCopy = 299;

        private const uint DumpType = 0x1 | 0x4 | 0x20 | 0x200 | 0x1000;

        [DllImport("dbghelp.dll", SetLastError = true)]
        private static extern bool MiniDumpWriteDump(IntPtr hProcess, uint processId, SafeFileHandle hFile,
                                                     uint dumpType, IntPtr exceptionParam, IntPtr userStreamParam,
                                                     IntPtr callbackParam);
    }
}
