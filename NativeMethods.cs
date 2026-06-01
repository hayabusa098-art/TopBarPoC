using System.Runtime.InteropServices;
using System.Text;

namespace TopBarPoC;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct RECT  { public int Left, Top, Right, Bottom; }

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA
{
    public uint   cbSize;
    public IntPtr hWnd;
    public uint   uCallbackMessage;
    public uint   uEdge;
    public RECT   rc;
    public IntPtr lParam;
}

internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

internal static class NativeMethods
{
    // ── Display / DPI ─────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    internal static extern IntPtr  MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("shcore.dll")]
    internal static extern int     GetDpiForMonitor(IntPtr hMon, int dpiType, out uint dpiX, out uint dpiY);

    // ── Window positioning ────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    internal static extern bool    SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    // ── AppBar ────────────────────────────────────────────────────────────────
    [DllImport("shell32.dll")]
    internal static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    // ── Messages ──────────────────────────────────────────────────────────────
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern uint    RegisterWindowMessage(string lpString);
    // SendMessageTimeout prevents UI-thread hang on unresponsive target windows (SMTO_ABORTIFHUNG)
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern IntPtr  SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    // ── Window enumeration ────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    internal static extern bool    EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")]
    internal static extern bool    IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    internal static extern uint    GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int     GetWindowText(IntPtr hWnd, StringBuilder buf, int len);
    [DllImport("user32.dll")]
    internal static extern int     GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]
    internal static extern IntPtr  GetShellWindow();
    [DllImport("user32.dll")]
    internal static extern int     GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    internal static extern IntPtr  GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")]
    internal static extern bool    IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]
    internal static extern bool    IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    internal static extern bool    ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    internal static extern bool    PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    internal static extern bool    SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    internal static extern IntPtr  GetForegroundWindow();

    // ── Class icon (x64 / x86 dual import; callers use GetClassLongPtrSafe) ──
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr   GetClassLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetClassLongW")]
    private static extern uint     GetClassLong32(IntPtr hWnd, int nIndex);

    internal static IntPtr GetClassLongPtrSafe(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, nIndex) : (IntPtr)GetClassLong32(hWnd, nIndex);

    // Sends WM_GETICON with SMTO_ABORTIFHUNG; returns icon handle or zero on timeout/failure
    internal static IntPtr SendIconMsg(IntPtr hwnd, int iconType)
    {
        SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)iconType, IntPtr.Zero,
            SMTO_ABORTIFHUNG, 50, out IntPtr result);
        return result;
    }

    // ── Constants ─────────────────────────────────────────────────────────────
    internal const uint ABM_NEW      = 0, ABM_REMOVE  = 1;
    internal const uint ABM_QUERYPOS = 2, ABM_SETPOS  = 3;
    internal const uint ABM_ACTIVATE = 6, ABM_WINDOWPOSCHANGED = 9;
    internal const uint ABE_TOP      = 1;
    internal const uint ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;
    internal const uint SWP_NOACTIVATE = 0x0010, SWP_NOZORDER = 0x0004;
    internal const int  WM_ACTIVATE = 0x0006, WM_WINDOWPOSCHANGED = 0x0047;
    internal const int  GWL_EXSTYLE       = -20;
    internal const int  WS_EX_TOOLWINDOW  = 0x00000080, WS_EX_APPWINDOW = 0x00040000;
    internal const uint GW_OWNER          = 4;
    internal const int  SW_MINIMIZE       = 6, SW_RESTORE = 9;
    internal const uint WM_CLOSE          = 0x0010;
    internal const uint WM_GETICON        = 0x007F;
    internal const uint SMTO_ABORTIFHUNG  = 0x0002;
    internal const int  ICON_SMALL        = 0;
    internal const int  ICON_SMALL2       = 2;
    internal const int  GCLP_HICONSM     = -34;
    internal const int  GCLP_HICON       = -14;

    // Registered once per process at class-load time (no window handle required)
    internal static readonly uint WM_APPBAR         = RegisterWindowMessage("TopBarPoC_AppBarCallback");
    internal static readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");
}
