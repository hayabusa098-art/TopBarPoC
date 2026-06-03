using System.Diagnostics;
using System.IO;
using SysDebug = System.Diagnostics.Debug;

namespace TopBarPoC;

internal static class DiagnosticsLog
{
    private static readonly object Sync = new();
    private static TextWriterTraceListener? _listener;

    internal static string? LogPath { get; private set; }

    internal static void Initialize()
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TopBarPoC",
                "logs");
            Directory.CreateDirectory(logDir);

            LogPath = Path.Combine(
                logDir,
                $"topbar-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            _listener = new TextWriterTraceListener(LogPath, "TopBarPoCFileDiagnostics");
            SysDebug.AutoFlush = true;

#if DEBUG
            const string buildMode = "Debug";
#else
            const string buildMode = "Release";
#endif
            WriteLine(
                $"[Diagnostics] started timestamp={DateTime.Now:O} pid={Environment.ProcessId} " +
                $"build={buildMode} logPath=\"{LogPath}\"");
            WriteProcessDpiAwareness();
        }
        catch
        {
            _listener = null;
            LogPath = null;
        }
    }

    internal static void WriteLine(string? message)
    {
        try
        {
            SysDebug.WriteLine(message);
            lock (Sync)
            {
                _listener?.WriteLine(message);
                _listener?.Flush();
            }
        }
        catch
        {
            // Diagnostics must never block application execution.
        }
    }

    private static void WriteProcessDpiAwareness()
    {
        try
        {
            int processHr = NativeMethods.GetProcessDpiAwareness(IntPtr.Zero, out int processAwareness);
            var threadContext = NativeMethods.GetThreadDpiAwarenessContext();
            int threadAwareness = threadContext == IntPtr.Zero
                ? -1
                : NativeMethods.GetAwarenessFromDpiAwarenessContext(threadContext);

            WriteLine(
                $"[DpiAwareness] processHr=0x{processHr:X8} processAwareness={processAwareness} " +
                $"threadContext=0x{threadContext.ToInt64():X} threadAwareness={threadAwareness}");
        }
        catch (Exception ex)
        {
            WriteLine($"[DpiAwareness] unavailable error=\"{ex.Message}\"");
        }
    }

    internal static void Shutdown()
    {
        try
        {
            if (_listener is null) return;
            WriteLine($"[Diagnostics] stopping timestamp={DateTime.Now:O}");
            SysDebug.Flush();
            _listener.Flush();
            _listener.Close();
        }
        catch
        {
            // Diagnostics must never block application shutdown.
        }
        finally
        {
            _listener = null;
        }
    }
}

internal static class Debug
{
    [Conditional("DEBUG")]
    internal static void WriteLine(string? message) => DiagnosticsLog.WriteLine(message);
}
