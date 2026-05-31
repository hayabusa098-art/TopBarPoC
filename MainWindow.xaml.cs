using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

// Bar height is 32 DIP (device-independent pixels). Physical pixels = 32 * dpiScale per monitor.
// Known limitations (out of MVP scope):
//   - Monitor hot-plug / disconnect after startup is not handled; restart required.
//   - Runtime DPI change (display scaling slider) is not handled; restart required.
//   - Runtime resolution change is not handled; restart required.
//   - If Windows taskbar occupies the top edge, TopBar is placed below it (acceptable).
//   - Full-screen exclusive apps: AppBar z-order policy not implemented; bar may be hidden.
public partial class TopBarWindow : Window
{
    // ── Win32 structs ─────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT  { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint   cbSize;
        public IntPtr hWnd;
        public uint   uCallbackMessage;
        public uint   uEdge;
        public RECT   rc;
        public IntPtr lParam;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]  private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("shcore.dll")]  private static extern int    GetDpiForMonitor(IntPtr hMon, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")]  private static extern bool   SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
                               private static extern uint   RegisterWindowMessage(string lpString);

    // ── Window switcher P/Invoke ───────────────────────────────────────────────
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]  private static extern bool   EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")]  private static extern bool   IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]  private static extern uint   GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
                               private static extern int    GetWindowText(IntPtr hWnd, StringBuilder buf, int len);
    [DllImport("user32.dll")]  private static extern int    GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")]  private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")]  private static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]  private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")]  private static extern bool   IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")]  private static extern bool   IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")]  private static extern bool   ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]  private static extern bool   SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]  private static extern IntPtr GetForegroundWindow();

    // ── Constants ─────────────────────────────────────────────────────────────
    private const uint ABM_NEW      = 0, ABM_REMOVE  = 1;
    private const uint ABM_QUERYPOS = 2, ABM_SETPOS  = 3;
    private const uint ABM_ACTIVATE = 6, ABM_WINDOWPOSCHANGED = 9;
    private const uint ABE_TOP      = 1;
    private const uint ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;
    private const uint SWP_NOACTIVATE = 0x0010, SWP_NOZORDER = 0x0004;
    private const int  WM_ACTIVATE = 0x0006, WM_WINDOWPOSCHANGED = 0x0047;
    private const int  GWL_EXSTYLE      = -20;
    private const int  WS_EX_TOOLWINDOW = 0x00000080, WS_EX_APPWINDOW = 0x00040000;
    private const uint GW_OWNER         = 4;
    private const int  SW_RESTORE       = 9;

    // ── Window switcher types ─────────────────────────────────────────────────
    private readonly record struct WindowInfo(IntPtr Handle, string Title, bool IsMinimized);

    private sealed class WindowChipVm : INotifyPropertyChanged
    {
        public required IntPtr Handle      { get; init; }
        public required string Title       { get; init; }
        public required bool   IsMinimized { get; init; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                PropertyChanged?.Invoke(this, _isActivePcea);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private static readonly PropertyChangedEventArgs _isActivePcea = new(nameof(IsActive));
    }

    // Registered once per process; safe to call before any window is created
    private static readonly uint WM_APPBAR         = RegisterWindowMessage("TopBarPoC_AppBarCallback");
    private static readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

    // ── Fields ────────────────────────────────────────────────────────────────
    private readonly WinFormsScreen  _screen;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _windowPollTimer;
    private          List<WindowChipVm> _chipVms     = [];
    private          List<WindowInfo>   _prevWindows = [];
    private bool _appBarRegistered;

    // ── Construction ──────────────────────────────────────────────────────────
    public TopBarWindow(WinFormsScreen screen)
    {
        _screen = screen;
        InitializeComponent();

        // Hold off-screen until RegisterAppBar() positions us via ABM_SETPOS.
        // Placing the window at its target position before ABM_NEW causes ABM_QUERYPOS
        // to treat our own window as an obstacle and shift rc.Top by one bar height.
        double scale = GetDpiScale();
        Left  = -32000;
        Top   = -32000;
        Width = screen.Bounds.Width / scale;

        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        };
        Loaded  += OnLoaded;
        Closing += (_, _) => { _clock?.Stop(); _windowPollTimer?.Stop(); UnregisterAppBar(); };

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();

        _windowPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _windowPollTimer.Tick += (_, _) => RefreshWindowChips();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RegisterAppBar();
        UpdateClock();
        _clock.Start();
        CodeButton.IsEnabled = FindVSCode() is not null;
        RefreshWindowChips();
        _windowPollTimer.Start();
    }

    // ── AppBar ────────────────────────────────────────────────────────────────
    private void RegisterAppBar()
    {
        var hwnd     = new WindowInteropHelper(this).Handle;
        int heightPx = PhysicalBarHeight();
        var data     = BuildAppBarData(hwnd, heightPx);

        if (SHAppBarMessage(ABM_NEW, ref data) == UIntPtr.Zero)
        {
            // Shell registration failed; fall back to Topmost-only positioning
            _appBarRegistered = false;
            SetWindowPos(hwnd, IntPtr.Zero,
                _screen.Bounds.X, _screen.Bounds.Y,
                _screen.Bounds.Width, heightPx,
                SWP_NOACTIVATE | SWP_NOZORDER);
            return;
        }

        _appBarRegistered = true;
        QueryAndApplyPosition(hwnd, heightPx);
    }

    private void RefreshPosition()
    {
        if (!_appBarRegistered) return;
        var hwnd     = new WindowInteropHelper(this).Handle;
        int heightPx = PhysicalBarHeight();
        QueryAndApplyPosition(hwnd, heightPx);
    }

    // ABM_QUERYPOS → clamp height → ABM_SETPOS → SetWindowPos
    private void QueryAndApplyPosition(IntPtr hwnd, int heightPx)
    {
        var data = BuildAppBarData(hwnd, heightPx);

        SHAppBarMessage(ABM_QUERYPOS, ref data);
        data.rc.Bottom = data.rc.Top + heightPx; // maintain 32 DIP regardless of shell adjustment
        SHAppBarMessage(ABM_SETPOS,   ref data);

        SetWindowPos(hwnd, IntPtr.Zero,
            data.rc.Left, data.rc.Top,
            data.rc.Right - data.rc.Left,
            data.rc.Bottom - data.rc.Top,
            SWP_NOACTIVATE | SWP_NOZORDER);
    }

    private void UnregisterAppBar()
    {
        if (!_appBarRegistered) return;
        var data = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd   = new WindowInteropHelper(this).Handle
        };
        SHAppBarMessage(ABM_REMOVE, ref data);
        _appBarRegistered = false;
    }

    // ── WndProc hook ──────────────────────────────────────────────────────────
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        uint uMsg = (uint)msg;

        if (uMsg == WM_APPBAR)
        {
            switch ((uint)wParam)
            {
                case ABN_POSCHANGED:
                    // MVP: skip re-query. ABM_SETPOS itself triggers ABN_POSCHANGED, and
                    // re-querying while our own bar is registered causes the shell to return
                    // rc.Top = barHeight (treating our own reservation as an obstacle), which
                    // shifts the bar down by one bar-height per notification cycle.
                    // Fixed-top bar with bottom taskbar (primary environment) needs no adjustment.
                    break;
                case ABN_FULLSCREENAPP:
                    // MVP: no policy; full-screen z-order handling deferred
                    break;
            }
        }
        else if (uMsg == WM_TASKBARCREATED)
        {
            // Explorer restarted; shell lost our registration — re-register
            _appBarRegistered = false;
            RegisterAppBar();
        }
        else if (msg == WM_ACTIVATE && _appBarRegistered)
        {
            var data = new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
                hWnd   = hwnd,
                lParam = wParam  // low word: 0 = deactivate, 1/2 = activate
            };
            SHAppBarMessage(ABM_ACTIVATE, ref data);
        }
        else if (msg == WM_WINDOWPOSCHANGED && _appBarRegistered)
        {
            var data = new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
                hWnd   = hwnd
            };
            SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref data);
        }

        return IntPtr.Zero;
    }

    // ── Window switcher ───────────────────────────────────────────────────────
    private List<WindowInfo> EnumerateWindows()
    {
        var result   = new List<WindowInfo>(16);
        var shellWnd = GetShellWindow();
        var selfPid  = (uint)Environment.ProcessId;

        EnumWindowsProc callback = (hWnd, _) =>
        {
            if (result.Count >= 10) return false; // stop at cap

            if (!IsWindowVisible(hWnd) || hWnd == shellWnd) return true;

            var textLen = GetWindowTextLength(hWnd);
            if (textLen == 0) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == selfPid) return true;

            int  exStyle      = GetWindowLong(hWnd, GWL_EXSTYLE);
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow  = (exStyle & WS_EX_APPWINDOW)  != 0;
            bool hasOwner     = GetWindow(hWnd, GW_OWNER) != IntPtr.Zero;

            if (!isAppWindow && (hasOwner || isToolWindow)) return true;

            var sb = new StringBuilder(textLen + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            result.Add(new WindowInfo(hWnd, sb.ToString(), IsIconic(hWnd)));
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);
        return result;
    }

    private void RefreshWindowChips()
    {
        var activeHwnd = GetForegroundWindow();
        var windows    = EnumerateWindows();

        bool listChanged = windows.Count != _prevWindows.Count ||
                           Enumerable.Range(0, windows.Count)
                                     .Any(i => windows[i] != _prevWindows[i]);
        _prevWindows = windows;

        if (listChanged)
        {
            _chipVms = windows.Select(w => new WindowChipVm
            {
                Handle      = w.Handle,
                Title       = w.Title,
                IsMinimized = w.IsMinimized,
            }).ToList();
            WindowChips.ItemsSource = _chipVms;
        }

        // Always sync active state (foreground window can change between polls)
        foreach (var vm in _chipVms)
            vm.IsActive = vm.Handle == activeHwnd;
    }

    private void ChipButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not IntPtr hwnd) return;
        if (!IsWindow(hwnd)) return;
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        _ = SetForegroundWindow(hwnd); // may fail under Windows focus-stealing restrictions; silent fail is acceptable for MVP
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private APPBARDATA BuildAppBarData(IntPtr hwnd, int heightPx) => new APPBARDATA
    {
        cbSize           = (uint)Marshal.SizeOf<APPBARDATA>(),
        hWnd             = hwnd,
        uCallbackMessage = WM_APPBAR,
        uEdge            = ABE_TOP,
        rc               = new RECT
        {
            Left   = _screen.Bounds.X,
            Top    = _screen.Bounds.Y,
            Right  = _screen.Bounds.X + _screen.Bounds.Width,
            Bottom = _screen.Bounds.Y + heightPx
        }
    };

    private int PhysicalBarHeight() => (int)Math.Round(32 * GetDpiScale()); // 32 DIP → physical px

    private double GetDpiScale()
    {
        int cx  = _screen.Bounds.X + _screen.Bounds.Width  / 2;
        int cy  = _screen.Bounds.Y + _screen.Bounds.Height / 2;
        var mon = MonitorFromPoint(new POINT { X = cx, Y = cy }, 2u); // MONITOR_DEFAULTTONEAREST
        GetDpiForMonitor(mon, 0, out uint dpiX, out _);               // MDT_EFFECTIVE_DPI
        return dpiX > 0 ? dpiX / 96.0 : 1.0;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        DateText.Text  = now.ToString("MM/dd");
        ClockText.Text = now.ToString("HH:mm");
    }

    // ── Launcher ──────────────────────────────────────────────────────────────
    private static string? FindVSCode()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                @"Microsoft VS Code\Code.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Microsoft VS Code\Code.exe"),
        ];
        return Array.Find(candidates, File.Exists);
    }

    private static void Launch(string pathOrUrl)
    {
        try { Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true }); }
        catch { /* silent fail: path gone or access denied */ }
    }

    private void FilesButton_Click(object sender, RoutedEventArgs e) => Launch("explorer.exe");
    private void WebButton_Click(object sender, RoutedEventArgs e)   => Launch("https://www.google.com");
    private void CodeButton_Click(object sender, RoutedEventArgs e)
    {
        var path = FindVSCode();
        if (path is not null) Launch(path);
    }

    // ── Exit ──────────────────────────────────────────────────────────────────
    private void ExitButton_Click(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();
}
