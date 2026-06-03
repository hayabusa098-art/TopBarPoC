using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static TopBarPoC.NativeMethods;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

// Bar height is configured in DIPs (device-independent pixels). Physical pixels = DIP height * dpiScale per monitor.
// Known limitations (out of MVP scope):
//   - Monitor hot-plug / disconnect after startup is not handled; restart required.
//   - Runtime display scaling / resolution changes refresh existing monitor bars only.
//   - If Windows taskbar occupies the top edge, TopBar is placed below it (acceptable).
//   - Full-screen exclusive apps: AppBar z-order policy not implemented; bar may be hidden.
public partial class TopBarWindow : Window
{
    // ── Fields ────────────────────────────────────────────────────────────────
    private          WinFormsScreen  _screen;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _windowPollTimer;
    private readonly double _barHeightDip;
    private          List<WindowChipVm> _chipVms     = [];
    private          List<WindowInfo>   _prevWindows = [];
    private static readonly List<IntPtr> _windowOrder = [];
    private static readonly List<string> _appOrder = [];
    private readonly Dictionary<IntPtr, ImageSource?> _iconCache = new();
    private readonly Dictionary<string, IntPtr> _lastGroupCycleHandles =
        new(StringComparer.OrdinalIgnoreCase);
    private bool             _appBarRegistered;
    private bool             _closing;
    private bool             _displayRefreshQueued;
    private bool             _shellStateRefreshQueued;
    private DispatcherTimer? _shellSettleTimer;
    private bool             _displaySettingsSubscribed;
    private IntPtr _lastExternalForeground;
    private IntPtr _lastRevealedForeground;
    private IntPtr _dragCandidateHwnd;
    private IntPtr _suppressClickHwnd;
    private System.Windows.Point _dragStartPoint;
    private IntPtr _clickSnapshot = IntPtr.Zero;
    private IntPtr               _hoverHwnd;
    private DispatcherTimer?     _hoverTimer;
    private HoverPreviewWindow?  _previewWindow;
    private DispatcherTimer?     _hideTimer;
    private double               _currentChipGap = 3.0;
    private ChipDensityMode _chipDensityMode = ChipDensityMode.Balanced;

    private enum ChipDensityMode
    {
        Expanded,
        Balanced,
        Compact,
    }

    // ── Construction ──────────────────────────────────────────────────────────
    public TopBarWindow(WinFormsScreen screen, double barHeightDip)
    {
        _screen       = screen;
        _barHeightDip = barHeightDip;
        InitializeComponent();
        Height = _barHeightDip;

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
        Closing += (_, _) =>
        {
            _closing = true;
            _clock?.Stop();
            _windowPollTimer?.Stop();
            _shellSettleTimer?.Stop();
            CancelHoverTimer();
            CancelHideTimer();
            _previewWindow?.Close();
            if (_displaySettingsSubscribed)
            {
                SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
                _displaySettingsSubscribed = false;
            }
            UnregisterAppBar();
        };

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => UpdateClock();

        _windowPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _windowPollTimer.Tick += (_, _) => RefreshWindowChips();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RegisterAppBar();
        if (!_displaySettingsSubscribed)
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            _displaySettingsSubscribed = true;
        }
        UpdateClock();
        _clock.Start();
        CodeButton.IsEnabled = FindVSCode() is not null;
        CenterGrid.SizeChanged += (_, _) => RecalcChipWidth();
        RefreshWindowChips();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshWindowChipOverflowFades));
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
        var hwnd     = new WindowInteropHelper(this).Handle;
        int heightPx = PhysicalBarHeight();
        if (_appBarRegistered)
        {
            QueryAndApplyPosition(hwnd, heightPx);
            return;
        }
        // Fallback: AppBar shell registration unavailable; maintain position directly.
        SetWindowPos(hwnd, IntPtr.Zero,
            _screen.Bounds.X, _screen.Bounds.Y,
            _screen.Bounds.Width, heightPx,
            SWP_NOACTIVATE | SWP_NOZORDER);
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        => QueueDisplayRefresh();

    private void QueueDisplayRefresh()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(QueueDisplayRefresh));
            return;
        }

        if (_displayRefreshQueued) return;
        _displayRefreshQueued = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _displayRefreshQueued = false;
            if (!IsLoaded) return;

            var oldScreen = _screen;
            var refreshedScreen = WinFormsScreen.AllScreens.FirstOrDefault(
                screen => screen.DeviceName.Equals(
                    oldScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
            bool usedFallback = refreshedScreen is null;
            _screen = refreshedScreen ?? oldScreen;

            Debug.WriteLine(
                $"[DisplayRefresh] old={oldScreen.DeviceName} bounds={oldScreen.Bounds} " +
                $"refreshed={_screen.DeviceName} bounds={_screen.Bounds} fallback={usedFallback}");

            Width = _screen.Bounds.Width / GetDpiScale();
            RefreshPosition();
            RecalcChipWidth();
            QueueWindowChipOverflowFadeRefresh();
        }));
    }

    private void QueueShellStateRefresh()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(QueueShellStateRefresh));
            return;
        }
        if (_shellStateRefreshQueued) return;
        _shellStateRefreshQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _shellStateRefreshQueued = false;
            if (!IsLoaded) return;
            Debug.WriteLine("[ShellStateRefresh] re-querying AppBar position");
            RefreshPosition();
            // Lazy-init reusable settle timer; Stop/Start restarts the window on rapid bursts.
            if (_shellSettleTimer is null)
            {
                _shellSettleTimer = new DispatcherTimer(DispatcherPriority.Normal)
                    { Interval = TimeSpan.FromMilliseconds(500) };
                _shellSettleTimer.Tick += (_, _) =>
                {
                    _shellSettleTimer.Stop();
                    if (!IsLoaded) return;
                    Debug.WriteLine("[ShellStateRefresh.settle] re-querying AppBar position");
                    RefreshPosition();
                };
            }
            _shellSettleTimer.Stop();
            _shellSettleTimer.Start();
        }));
    }

    // ABM_QUERYPOS → clamp height → ABM_SETPOS → SetWindowPos
    private void QueryAndApplyPosition(IntPtr hwnd, int heightPx)
    {
        var data = BuildAppBarData(hwnd, heightPx);
        SHAppBarMessage(ABM_QUERYPOS, ref data);
        data.rc.Bottom = data.rc.Top + heightPx; // maintain configured DIP height regardless of shell adjustment
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
                case ABN_STATECHANGE:
                {
                    var sd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>(), hWnd = hwnd };
                    uint abs = (uint)SHAppBarMessage(ABM_GETSTATE, ref sd);
                    Debug.WriteLine($"[ABN_STATECHANGE] ABS=0x{abs:X} autoHide={(abs & ABS_AUTOHIDE) != 0}");
                    QueueShellStateRefresh();
                    break;
                }
                case ABN_POSCHANGED:
                    // Skip re-query. ABM_SETPOS itself triggers ABN_POSCHANGED, and
                    // re-querying while our own bar is registered causes the shell to return
                    // rc.Top = barHeight (treating our own reservation as an obstacle), which
                    // shifts the bar down by one bar-height per notification cycle.
                    // Fixed-top bar with bottom taskbar (primary environment) needs no adjustment.
                    break;
                case ABN_FULLSCREENAPP:
                    // Intentional: no auto-hide policy for full-screen apps.
                    // Bar remains visible; full-screen z-order handling is out of scope.
                    break;
            }
        }
        else if (uMsg == WM_TASKBARCREATED)
        {
            // Explorer restarted; shell lost our registration — re-register.
            // Guard: if the window is already closing, skip to avoid leaking a
            // ghost AppBar reservation (UnregisterAppBar already ran in Closing).
            if (!_closing)
            {
                _appBarRegistered = false;
                RegisterAppBar();
            }
        }
        else if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED)
        {
            QueueDisplayRefresh();
        }
        else if (msg == WM_SETTINGCHANGE)
        {
            string? lParamStr = null;
            if (lParam != IntPtr.Zero)
                try { lParamStr = Marshal.PtrToStringAuto(lParam); } catch { }
            Debug.WriteLine($"[WM_SETTINGCHANGE] wParam=0x{(uint)wParam:X4} lParam=\"{lParamStr ?? "(null)"}\"");
            QueueShellStateRefresh();
        }
        else if (msg == WM_MOUSEACTIVATE)
        {
            var fg = GetForegroundWindow();
            if (fg != hwnd)
            {
                _lastExternalForeground = fg;
                _clickSnapshot = fg;
            }
            else
            {
                _clickSnapshot = IntPtr.Zero;
            }
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
        else if (msg == WM_WINDOWPOSCHANGING)
        {
            var wpos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            int expectedTop  = _screen.Bounds.Top;
            int displacedTop = expectedTop + PhysicalBarHeight();
            bool isNoMove    = (wpos.flags & 0x0002u) != 0; // SWP_NOMOVE
            // Corrects obstacle-displacement: shell pushes bar down by its own height.
            // 1-pixel tolerance handles any rounding difference between our height
            // calculation and the shell's (relevant after runtime DPI changes).
            if (_appBarRegistered
                && !isNoMove
                && Math.Abs(wpos.y - displacedTop) <= 1)
            {
                Debug.WriteLine($"[WM_WINDOWPOSCHANGING] obstacle correction: y={wpos.y}→{expectedTop}");
                wpos.y = expectedTop;
                Marshal.StructureToPtr(wpos, lParam, false);
                // handled stays false — default processing runs with corrected position
            }
        }
        else if (msg == WM_WINDOWPOSCHANGED)
        {
            if (_appBarRegistered)
            {
                var data = new APPBARDATA
                {
                    cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
                    hWnd   = hwnd
                };
                SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref data);
            }
        }

        return IntPtr.Zero;
    }

    // ── Adaptive chip width ───────────────────────────────────────────────────
    private void RecalcChipWidth()
    {
        int n = _chipVms.Count;
        if (n == 0) return;

        double available = CenterGrid.ActualWidth
                         - LauncherPill.ActualWidth
                         - ChipSeparator.ActualWidth
                         - ChipSeparator.Margin.Left
                         - ChipSeparator.Margin.Right;

        if (!double.IsFinite(available) || available <= 0) return;

        double rawShare = available / n - 3.0;
        _chipDensityMode = ResolveChipDensityMode(available, n, rawShare);

        double chipGap;
        Thickness chipPadding;
        Thickness chipMargin;
        double inactiveWidth;
        double activeWidth;

        switch (_chipDensityMode)
        {
            case ChipDensityMode.Expanded:
                // Spacious feel through wider gaps, not wider pills
                chipGap      = 12.0;
                chipPadding  = new Thickness(12, 0, 12, 0);
                chipMargin   = new Thickness(0, 0, 12, 0);
                break;
            case ChipDensityMode.Compact:
                chipGap      = 2.0;
                chipPadding  = new Thickness(6, 0, 6, 0);
                chipMargin   = new Thickness(0, 0, 2, 0);
                break;
            default: // Balanced
                chipGap      = 3.0;
                chipPadding  = new Thickness(8, 0, 8, 0);
                chipMargin   = new Thickness(0, 0, 3, 0);
                break;
        }

        _currentChipGap = chipGap;
        double chipWidth = available / n - chipGap;

        switch (_chipDensityMode)
        {
            case ChipDensityMode.Expanded:
                // Same caps as Balanced — air comes from gaps, not pill size
                inactiveWidth = Math.Clamp(chipWidth, 52.0, 95.0);
                activeWidth   = Math.Clamp(chipWidth, 68.0, 120.0);
                break;
            case ChipDensityMode.Compact:
                inactiveWidth = Math.Clamp(chipWidth, 52.0, 60.0);
                activeWidth   = Math.Clamp(chipWidth, 68.0, 84.0);
                break;
            default:
                inactiveWidth = Math.Clamp(chipWidth, 52.0, 95.0);
                activeWidth   = Math.Clamp(chipWidth, 68.0, 120.0);
                break;
        }

        foreach (var vm in _chipVms)
        {
            vm.Width         = vm.IsActive ? activeWidth : inactiveWidth;
            vm.ChipGapWidth  = new GridLength(chipGap);
            vm.ChipMargin    = chipMargin;
            vm.ChipPadding   = chipPadding;
        }
    }

    private ChipDensityMode ResolveChipDensityMode(double available, int chipCount, double chipWidth)
    {
        var nextMode = _chipDensityMode switch
        {
            ChipDensityMode.Expanded when chipWidth <= 72.0 => ChipDensityMode.Compact,
            ChipDensityMode.Expanded when chipWidth <  96.0 => ChipDensityMode.Balanced,
            ChipDensityMode.Balanced when chipWidth >= 104.0 => ChipDensityMode.Expanded,
            ChipDensityMode.Balanced when chipWidth <=  72.0 => ChipDensityMode.Compact,
            ChipDensityMode.Compact  when chipWidth >= 104.0 => ChipDensityMode.Expanded,
            ChipDensityMode.Compact  when chipWidth >   80.0 => ChipDensityMode.Balanced,
            _ => _chipDensityMode,
        };

        if (nextMode != _chipDensityMode)
            Debug.WriteLine(
                $"[ChipDensity] {_chipDensityMode} -> {nextMode} " +
                $"available={available:F1} chips={chipCount} share={chipWidth:F1}");

        return nextMode;
    }

    // ── Window switcher ───────────────────────────────────────────────────────
    private List<WindowInfo> EnumerateWindows()
    {
        var result   = new List<WindowInfo>(16);
        var shellWnd = GetShellWindow();
        var selfPid  = (uint)Environment.ProcessId;
        var appIdentityCache = new Dictionary<uint, (string Key, string Label)>();
        var pidDenyCache     = new Dictionary<uint, bool>();  // true = denied system process

        EnumWindowsProc callback = (hWnd, _) =>
        {
            bool isMinimized = IsIconic(hWnd);
            if ((!IsWindowVisible(hWnd) && !isMinimized) || hWnd == shellWnd) return true;

            var textLen = GetWindowTextLength(hWnd);
            if (textLen == 0) return true;

            // Style checks are cheap Win32 calls; filter tool/owned windows before the
            // more expensive IsCloaked (DWM COM) and IsDeniedSystemProcess (process handle).
            int  exStyle      = GetWindowLong(hWnd, GWL_EXSTYLE);
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow  = (exStyle & WS_EX_APPWINDOW)  != 0;
            bool isNoActivate = (exStyle & WS_EX_NOACTIVATE) != 0;
            bool hasOwner     = NativeMethods.GetWindow(hWnd, GW_OWNER) != IntPtr.Zero;
            if (!isAppWindow && (hasOwner || isToolWindow || isNoActivate)) return true;

            var classBuf = new StringBuilder(64);
            GetClassName(hWnd, classBuf, classBuf.Capacity);
            if (classBuf.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
                                    or "DV2ControlHost" or "WorkerW") return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == selfPid) return true;
            if (IsCloaked(hWnd)) return true;

            // Cache the deny result per PID to avoid repeated Process.GetProcessById
            // calls for apps with multiple windows (e.g. File Explorer, browsers).
            if (!pidDenyCache.TryGetValue(pid, out bool isDenied))
                pidDenyCache[pid] = isDenied = IsDeniedSystemProcess(pid);
            if (isDenied) return true;

            var sb = new StringBuilder(textLen + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            Debug.WriteLine(
                $"[WindowEnum] include hwnd=0x{hWnd:X8} proc={pid} owner={hasOwner} " +
                $"app={isAppWindow} tool={isToolWindow} noActivate={isNoActivate} " +
                $"iconic={isMinimized} title=\"{title}\"");

            if (!appIdentityCache.TryGetValue(pid, out var identity))
            {
                identity = ResolveAppIdentity(pid, hWnd);
                if (!identity.Key.StartsWith("hwnd:", StringComparison.Ordinal))
                    appIdentityCache[pid] = identity;
            }
            var (appKey, appLabel) = identity;
            result.Add(new WindowInfo(hWnd, title, isMinimized, appKey, appLabel));
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);
        return result;
    }

    private static (string Key, string Label) ResolveAppIdentity(uint pid, IntPtr hwnd)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string processName = process.ProcessName;
            string label = FormatAppLabel(processName);
            if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("dllhost",              StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("RuntimeBroker",        StringComparison.OrdinalIgnoreCase))
                return ($"hwnd:{hwnd}", label);

            try
            {
                string? executablePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executablePath))
                    return ($"path:{executablePath}", label);
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(processName))
                return ($"process:{processName}", label);
        }
        catch { }

        return ($"hwnd:{hwnd}", "Window");
    }

    private static string FormatAppLabel(string processName)
        => string.IsNullOrEmpty(processName)
            ? "Window"
            : char.ToUpperInvariant(processName[0]) + processName[1..];

    private static bool IsCloaked(IntPtr hwnd)
    {
        uint cloaked = 0;
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, sizeof(uint)) == 0 &&
               cloaked != 0;
    }

    private static bool IsDeniedSystemProcess(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string processName = process.ProcessName;
            return processName.Equals("TextInputHost",           StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("dwm",                    StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("SearchApp",              StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("SearchHost",             StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("ShellExperienceHost",    StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void RefreshWindowChips()
    {
        var foreground = GetForegroundWindow();
        var selfHwnd   = new WindowInteropHelper(this).Handle;
        if (foreground != selfHwnd)
            _lastExternalForeground = foreground;
        var enumeratedWindows = EnumerateWindows();
        var byHandle = enumeratedWindows.ToDictionary(w => w.Handle);
        var previousAppOrder = _prevWindows
            .Select(w => w.AppKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _windowOrder.RemoveAll(h => !byHandle.ContainsKey(h));
        var orderedHandles = new HashSet<IntPtr>(_windowOrder);
        foreach (var window in enumeratedWindows)
            if (orderedHandles.Add(window.Handle))
                _windowOrder.Add(window.Handle);

        var orderedWindows = _windowOrder.Select(h => byHandle[h]).ToList();
        var windowsByApp = orderedWindows
            .GroupBy(w => w.AppKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        if (_appOrder.Count == 0)
            _appOrder.AddRange(previousAppOrder.Where(windowsByApp.ContainsKey));
        _appOrder.RemoveAll(appKey => !windowsByApp.ContainsKey(appKey));
        var stableAppKeys = new HashSet<string>(_appOrder, StringComparer.OrdinalIgnoreCase);
        foreach (var window in orderedWindows)
            if (stableAppKeys.Add(window.AppKey))
                _appOrder.Add(window.AppKey);

        var windows = _appOrder.SelectMany(appKey => windowsByApp[appKey]).ToList();
        _windowOrder.Clear();
        _windowOrder.AddRange(windows.Select(w => w.Handle));

        // Structural change: handle set or titles changed — requires full rebuild.
        // IsMinimized-only changes are handled via INPC without ItemsSource rebind.
        bool structuralChange =
            windows.Count != _prevWindows.Count ||
            Enumerable.Range(0, windows.Count)
                      .Any(i => windows[i].Handle != _prevWindows[i].Handle ||
                                windows[i].AppKey != _prevWindows[i].AppKey);
        _prevWindows = windows;

        if (structuralChange)
        {
            // Drop cache entries for handles no longer eligible
            var current = new HashSet<IntPtr>(enumeratedWindows.Select(w => w.Handle));
            foreach (var stale in _iconCache.Keys.Where(k => !current.Contains(k)).ToList())
                _iconCache.Remove(stale);

            _chipVms = windows.GroupBy(w => w.AppKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
            {
                var members = group.ToList();
                var first = members[0];
                return new WindowChipVm
                {
                    Handle  = first.Handle,
                    Handles = members.Select(w => w.Handle).ToArray(),
                    Title   = members.Count == 1 ? first.Title : $"{first.AppLabel} ×{members.Count}",
                    Icon    = GetCachedIcon(first.Handle),
                };
            }).ToList();
            WindowChips.ItemsSource = _chipVms;
            RecalcChipWidth();
            QueueWindowChipOverflowFadeRefresh();
            // ItemsSource replacement resets scroll position; clear the reveal cache so
            // RevealActiveChipIfNeeded re-scrolls the active chip into view.
            _lastRevealedForeground = IntPtr.Zero;
        }

        // Sync IsMinimized and Title via INPC (no ItemsSource rebind needed)
        foreach (var vm in _chipVms)
        {
            var infos = vm.Handles.Where(byHandle.ContainsKey).Select(h => byHandle[h]).ToList();
            vm.IsMinimized = infos.Count > 0 && infos.All(info => info.IsMinimized);
            if (!vm.IsGrouped && infos.Count == 1)
                vm.Title = infos[0].Title;
        }

        // Always sync active state (foreground window can change between polls)
        foreach (var vm in _chipVms)
            vm.IsActive = vm.Handles.Contains(_lastExternalForeground);

        RecalcChipWidth();
        RevealActiveChipIfNeeded();
    }

    private void WindowChipsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (WindowChipsScrollViewer.ScrollableWidth <= 0) return;
        // Halve the raw delta so one standard notch (120) scrolls ~60 px instead of a full
        // chip-width-plus; smooth-scroll mice send smaller deltas and remain fine-grained.
        WindowChipsScrollViewer.ScrollToHorizontalOffset(
            WindowChipsScrollViewer.HorizontalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private void WindowChipsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => RefreshWindowChipOverflowFades();

    private void RefreshWindowChipOverflowFades()
    {
        if (WindowChipsLeftFade is null || WindowChipsRightFade is null) return;

        const double endpointTolerance = 0.5;
        WindowChipsLeftFade.Visibility = WindowChipsScrollViewer.HorizontalOffset > endpointTolerance
            ? Visibility.Visible
            : Visibility.Collapsed;
        WindowChipsRightFade.Visibility =
            WindowChipsScrollViewer.HorizontalOffset <
            WindowChipsScrollViewer.ScrollableWidth - endpointTolerance
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void QueueWindowChipOverflowFadeRefresh()
        => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshWindowChipOverflowFades));

    private void ChipButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressClickHwnd = IntPtr.Zero;
        _dragCandidateHwnd = sender is Button { Tag: IntPtr hwnd } ? hwnd : IntPtr.Zero;
        _dragStartPoint = e.GetPosition(this);
    }

    private void ChipButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        => _dragCandidateHwnd = IntPtr.Zero;

    private void ChipButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not Button btn ||
            btn.Tag is not IntPtr hwnd ||
            hwnd != _dragCandidateHwnd)
            return;

        var point = e.GetPosition(this);
        if (Math.Abs(point.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragCandidateHwnd = IntPtr.Zero;
        CancelHoverTimer();
        CancelHideTimer();
        _previewWindow?.HidePreview();
        var data = new DataObject("TopBarPoC.WindowChipHwnd", hwnd.ToInt64());
        var effect = DragDrop.DoDragDrop(btn, data, DragDropEffects.Move);
        ClearWindowChipDropCue();
        if (effect == DragDropEffects.Move)
            _suppressClickHwnd = hwnd;
    }

    private void ChipButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button { Tag: IntPtr hwnd } btn) return;
        _hoverHwnd = hwnd;
        CancelHideTimer();
        CancelHoverTimer();
        _hoverTimer = new DispatcherTimer(DispatcherPriority.Normal)
            { Interval = TimeSpan.FromMilliseconds(450) };
        _hoverTimer.Tick += (_, _) =>
        {
            CancelHoverTimer();
            ShowHoverPreview(_hoverHwnd, btn);
        };
        _hoverTimer.Start();
    }

    private void ChipButton_MouseLeave(object sender, MouseEventArgs e)
    {
        CancelHoverTimer();
        _hoverHwnd = IntPtr.Zero;
        HideHoverPreview();
    }

    private void ChipButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        e.Handled = true;
        if (sender is not Button { Tag: IntPtr hwnd } btn) return;
        if (btn.DataContext is WindowChipVm { IsGrouped: true }) return;
        if (!IsWindow(hwnd)) return;
        TryCloseWindow(hwnd, "[MiddleClose]");
    }

    // ── Hover preview (Build40 — DWM thumbnail) ──────────────────────────────────
    private void ShowHoverPreview(IntPtr hwnd, Button sourceChip)
    {
        if (hwnd == IntPtr.Zero) return;
        var vm = _chipVms.FirstOrDefault(v => v.Handle == hwnd);
        if (vm is null || vm.IsGrouped) return;

        _previewWindow ??= CreatePreviewWindow();

        var chipOrigin = sourceChip.PointToScreen(new System.Windows.Point(0, 0));
        double previewW = _previewWindow.Width;
        double left = chipOrigin.X + sourceChip.ActualWidth / 2.0 - previewW / 2.0;
        double top  = _screen.Bounds.Top + PhysicalBarHeight() / GetDpiScale() + 4.0;
        left = Math.Max(_screen.Bounds.Left, Math.Min(left, _screen.Bounds.Right - previewW));

        string title = _prevWindows.FirstOrDefault(w => w.Handle == hwnd).Title ?? vm.Title;
        _previewWindow.ShowForHwnd(hwnd, title, left, top, GetDpiScale());
    }

    private HoverPreviewWindow CreatePreviewWindow()
    {
        var win = new HoverPreviewWindow();
        win.MouseEnter += (_, _) => CancelHideTimer();
        win.MouseLeave += (_, _) => StartHideTimer();
        return win;
    }

    private void HideHoverPreview() => StartHideTimer();

    private void CancelHoverTimer()
    {
        _hoverTimer?.Stop();
        _hoverTimer = null;
    }

    private void CancelHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer = null;
    }

    private void StartHideTimer()
    {
        CancelHideTimer();
        _hideTimer = new DispatcherTimer(DispatcherPriority.Normal)
            { Interval = TimeSpan.FromMilliseconds(200) };
        _hideTimer.Tick += (_, _) =>
        {
            CancelHideTimer();
            _previewWindow?.HidePreview();
        };
        _hideTimer.Start();
    }

    private void WindowChipsScrollViewer_Drop(object sender, DragEventArgs e)
    {
        ClearWindowChipDropCue();
        if (!e.Data.GetDataPresent("TopBarPoC.WindowChipHwnd") ||
            e.Data.GetData("TopBarPoC.WindowChipHwnd") is not long value)
            return;

        var hwnd = (IntPtr)value;
        var target = FindAncestor<Button>(e.OriginalSource as DependencyObject);
        if (target?.Tag is IntPtr sameHwnd && sameHwnd == hwnd)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        var sourceVm = _chipVms.FirstOrDefault(vm => vm.Handle == hwnd);
        if (sourceVm is null) return;
        var sourceSet = new HashSet<IntPtr>(sourceVm.Handles);
        var sourceHandles = _windowOrder.Where(sourceSet.Contains).ToList();
        if (sourceHandles.Count == 0) return;
        _windowOrder.RemoveAll(sourceSet.Contains);

        bool inserted = false;
        if (target?.Tag is IntPtr targetHwnd &&
            _chipVms.FirstOrDefault(vm => vm.Handle == targetHwnd) is { } targetVm &&
            _windowOrder.FindIndex(targetVm.Handles.Contains) is int targetIndex &&
            targetIndex >= 0)
        {
            if (e.GetPosition(target).X >= target.ActualWidth / 2)
                targetIndex = _windowOrder.FindLastIndex(targetVm.Handles.Contains) + 1;
            _windowOrder.InsertRange(targetIndex, sourceHandles);
            inserted = true;
        }
        if (!inserted)
            _windowOrder.AddRange(sourceHandles);

        var appKeyByHandle = _prevWindows.ToDictionary(window => window.Handle, window => window.AppKey);
        _appOrder.Clear();
        _appOrder.AddRange(_windowOrder
            .Where(appKeyByHandle.ContainsKey)
            .Select(hwnd => appKeyByHandle[hwnd])
            .Distinct(StringComparer.OrdinalIgnoreCase));

        _prevWindows = [];
        RefreshWindowChips();
        QueueWindowChipOverflowFadeRefresh();
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void WindowChipsScrollViewer_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TopBarPoC.WindowChipHwnd") ||
            e.Data.GetData("TopBarPoC.WindowChipHwnd") is not long value)
        {
            ClearWindowChipDropCue();
            return;
        }

        var target = FindAncestor<Button>(e.OriginalSource as DependencyObject);
        if (target?.Tag is IntPtr targetHwnd && targetHwnd == (IntPtr)value)
        {
            ClearWindowChipDropCue();
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (target?.Tag is IntPtr)
            SetWindowChipDropCue(target, e.GetPosition(target).X >= target.ActualWidth / 2);

        const double edgeZone = 24.0;
        const double scrollIncrement = 24.0;
        double pointerX = e.GetPosition(WindowChipsScrollViewer).X;
        if (pointerX <= edgeZone)
            WindowChipsScrollViewer.ScrollToHorizontalOffset(
                WindowChipsScrollViewer.HorizontalOffset - scrollIncrement);
        else if (pointerX >= WindowChipsScrollViewer.ActualWidth - edgeZone)
            WindowChipsScrollViewer.ScrollToHorizontalOffset(
                WindowChipsScrollViewer.HorizontalOffset + scrollIncrement);

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void WindowChipsScrollViewer_DragLeave(object sender, DragEventArgs e)
    {
        var point = e.GetPosition(WindowChipsScrollViewer);
        bool isInside =
            point.X >= 0 && point.X <= WindowChipsScrollViewer.ActualWidth &&
            point.Y >= 0 && point.Y <= WindowChipsScrollViewer.ActualHeight;

        if (!isInside)
            ClearWindowChipDropCue();
    }

    private void SetWindowChipDropCue(Button target, bool after)
    {
        var chipPosition = target.TranslatePoint(new System.Windows.Point(), WindowChipsOverlayGrid);
        double half = _currentChipGap / 2.0 - 1.5;   // center 3px cue in gap
        double cueX = after
            ? chipPosition.X + target.ActualWidth + half
            : chipPosition.X - half - 3.0;
        WindowChipDropCue.Margin = new Thickness(cueX, 0, 0, 0);
        WindowChipDropCue.Visibility = Visibility.Visible;
    }

    private void ClearWindowChipDropCue()
        => WindowChipDropCue.Visibility = Visibility.Collapsed;

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void RevealActiveChipIfNeeded()
    {
        var hwnd = _lastExternalForeground;
        if (hwnd == _lastRevealedForeground) return;

        var vm = _chipVms.FirstOrDefault(item => item.Handles.Contains(hwnd));
        if (vm is null) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_lastExternalForeground != hwnd) return;
            if (WindowChips.ItemContainerGenerator.ContainerFromItem(vm) is not FrameworkElement container) return;
            container.BringIntoView();
            _lastRevealedForeground = hwnd;
            QueueWindowChipOverflowFadeRefresh();
        }));
    }

    // Returns cached icon; fetches and caches on first access per handle
    private ImageSource? GetCachedIcon(IntPtr hwnd)
    {
        if (!_iconCache.TryGetValue(hwnd, out var icon))
            _iconCache[hwnd] = icon = ExtractIcon(hwnd);
        return icon;
    }

    // Fallback chain: ICON_SMALL2 → ICON_SMALL → class small icon → class icon
    private static ImageSource? ExtractIcon(IntPtr hwnd)
    {
        var hIcon = SendIconMsg(hwnd, ICON_SMALL2);
        if (hIcon == IntPtr.Zero) hIcon = SendIconMsg(hwnd, ICON_SMALL);
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtrSafe(hwnd, GCLP_HICONSM);
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtrSafe(hwnd, GCLP_HICON);
        if (hIcon == IntPtr.Zero) return null;
        try
        {
            return Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch { return null; }
    }

    // Taskbar-style toggle: clicking the active window minimizes it; other chips restore/activate.
    private void ChipButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button btn || btn.Tag is not IntPtr hwnd) return;
            if (hwnd == _suppressClickHwnd)
            {
                _suppressClickHwnd = IntPtr.Zero;
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
                btn.DataContext is WindowChipVm chip &&
                TryGetOpenNewWindowLaunch(chip, out var launch))
            {
                OpenNewWindow(launch);
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                btn.DataContext is WindowChipVm { IsGrouped: true } ctrlGroup)
            {
                CycleGroupedWindowFocus(ctrlGroup);
                return;
            }
            if (btn.DataContext is WindowChipVm { IsGrouped: true } group)
            {
                OpenGroupedWindowMenu(btn, group);
                return;
            }
            if (!IsWindow(hwnd)) return;
            bool isActive = _clickSnapshot != IntPtr.Zero
                ? hwnd == _clickSnapshot
                : btn.DataContext is WindowChipVm { IsActive: true };
            if (isActive)
            {
                ShowWindow(hwnd, SW_MINIMIZE);
                return;
            }
            bool iconicBefore = IsIconic(hwnd);
            if (iconicBefore) ShowWindow(hwnd, SW_RESTORE);
            bool iconicAfter = IsIconic(hwnd);

            GetWindowThreadProcessId(hwnd, out uint targetPid);
            string targetProc = "(unknown)";
            try { using var p = Process.GetProcessById((int)targetPid); targetProc = p.ProcessName; } catch { }

            var fgBefore = GetForegroundWindow();
            var sw16 = Stopwatch.StartNew();
            bool sfwOk = SetForegroundWindow(hwnd);
            int sfwErr = Marshal.GetLastWin32Error();
            sw16.Stop();
            var fgAfter      = GetForegroundWindow();
            bool fgMatch      = fgAfter == hwnd;
            bool elevMismatch = !sfwOk && sfwErr == 5; // probable elevation/UIPI denial

            Debug.WriteLine(
                $"[ChipClick] hwnd=0x{hwnd:X8} proc={targetProc} " +
                $"snap=0x{_clickSnapshot:X8} fg_before=0x{fgBefore:X8} " +
                $"iconic={iconicBefore}→{iconicAfter} " +
                $"sfw={sfwOk} err={sfwErr} elev={elevMismatch} " +
                $"elapsed={sw16.ElapsedMilliseconds}ms " +
                $"fg_after=0x{fgAfter:X8} fg_match={fgMatch}");

            if (elevMismatch)
            {
                Debug.WriteLine($"[ChipClick] probable elevation/UIPI denial — retry skipped hwnd=0x{hwnd:X8}");
                return;
            }

            // Build17: optimistic active update — instant chip highlight on success
            if (sfwOk)
            {
                foreach (var vm in _chipVms)
                    vm.IsActive = vm.Handles.Contains(hwnd);
                RecalcChipWidth();
            }

            // Build16: one-shot 50ms retry if SFW failed or foreground did not switch
            if (!sfwOk || !fgMatch)
                ScheduleForegroundRetry(hwnd, "[ChipClick.retry]");
        }
        finally
        {
            _clickSnapshot = IntPtr.Zero;
        }
    }

    private static bool TryGetContextWindow(object sender, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (sender is not MenuItem item)
        {
            const string message = "[ContextWindow] target resolution failed: sender not MenuItem";
            Debug.WriteLine(message);
            return false;
        }
        if (ContextMenu.ItemsControlFromItemContainer(item) is not ContextMenu menu)
        {
            const string message = "[ContextWindow] target resolution failed: no ContextMenu";
            Debug.WriteLine(message);
            return false;
        }
        if (menu.PlacementTarget is not Button btn)
        {
            const string message = "[ContextWindow] target resolution failed: no PlacementTarget Button";
            Debug.WriteLine(message);
            return false;
        }
        if (btn.Tag is not IntPtr target)
        {
            const string message = "[ContextWindow] target resolution failed: no IntPtr target";
            Debug.WriteLine(message);
            return false;
        }
        if (!IsWindow(target))
        {
            string message = $"[ContextWindow] target resolution failed: !IsWindow(target) hwnd=0x{target:X}";
            Debug.WriteLine(message);
            return false;
        }
        hwnd = target;
        return true;
    }

    private static bool TryCloseWindow(IntPtr hwnd, string source)
    {
        bool isWindow = hwnd != IntPtr.Zero && IsWindow(hwnd);
        if (!isWindow)
        {
            string invalidWindowMessage = $"{source} hwnd=0x{hwnd:X} isWindow={isWindow} result=False win32Error=0";
            Debug.WriteLine(invalidWindowMessage);
            return false;
        }

        bool result = PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        int win32Error = result ? 0 : Marshal.GetLastWin32Error();
        string postMessageResult = $"{source} hwnd=0x{hwnd:X} isWindow={isWindow} result={result} win32Error={win32Error}";
        Debug.WriteLine(postMessageResult);
        return result;
    }

    private void OpenGroupedWindowMenu(Button button, WindowChipVm group)
    {
        var menu = new ContextMenu
        {
            Style = (Style)FindResource("ChipContextMenuStyle"),
            PlacementTarget = button,
            Placement = PlacementMode.Bottom,
        };
        foreach (var hwnd in group.Handles)
        {
            var info = _prevWindows.FirstOrDefault(window => window.Handle == hwnd);
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = info.Handle == IntPtr.Zero ? $"0x{hwnd:X8}" : info.Title,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            header.Children.Add(title);

            var closeButton = new Button
            {
                Content = "\u00D7",
                Tag = hwnd,
                Style = (Style)FindResource("GroupedSelectorCloseButtonStyle"),
            };
            closeButton.Click += GroupWindowCloseButton_Click;
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);

            var item = new MenuItem
            {
                Header = header,
                Tag = hwnd,
                Style = (Style)FindResource("ChipContextMenuItemStyle"),
            };
            item.Click += GroupWindowMenuItem_Click;
            menu.Items.Add(item);
        }
        if (TryGetOpenNewWindowLaunch(group, out var launch))
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new Separator { Style = (Style)FindResource("ChipContextMenuSeparatorStyle") });
            var item = new MenuItem
            {
                Header = "Open New Window",
                Tag = launch,
                Style = (Style)FindResource("ChipContextMenuItemStyle"),
            };
            item.Click += OpenNewWindowMenuItem_Click;
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void GroupWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindAncestor<Button>(source) is Button { Tag: IntPtr })
            return;

        if (sender is MenuItem { Tag: IntPtr hwnd } && IsWindow(hwnd))
            RestoreAndActivateWindow(hwnd, "[GroupChipSelect]");
    }

    private void GroupWindowCloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: IntPtr hwnd } button) return;

        if (FindAncestor<MenuItem>(button) is MenuItem item &&
            ContextMenu.ItemsControlFromItemContainer(item) is ContextMenu menu)
            menu.IsOpen = false;

        TryCloseWindow(hwnd, "[GroupClose]");
    }

    private void CycleGroupedWindowFocus(WindowChipVm group)
    {
        var handles = group.Handles.Where(IsWindow).ToList();
        if (handles.Count == 0) return;

        var info = _prevWindows.FirstOrDefault(window => handles.Contains(window.Handle));
        if (string.IsNullOrEmpty(info.AppKey)) return;

        int currentIndex = handles.IndexOf(_clickSnapshot);
        if (currentIndex < 0)
            currentIndex = handles.IndexOf(_lastExternalForeground);
        if (currentIndex < 0 &&
            _lastGroupCycleHandles.TryGetValue(info.AppKey, out var lastCycleHandle))
            currentIndex = handles.IndexOf(lastCycleHandle);
        if (currentIndex < 0)
            currentIndex = 0;

        var target = handles[(currentIndex + 1) % handles.Count];
        _lastGroupCycleHandles[info.AppKey] = target;
        RestoreAndActivateWindow(target, "[GroupCtrlClick]");
    }

    private readonly record struct OpenNewWindowLaunch(string Path, string? Argument);

    private bool TryGetOpenNewWindowLaunch(WindowChipVm chip, out OpenNewWindowLaunch launch)
    {
        launch = default;
        var info = _prevWindows.FirstOrDefault(window => chip.Handles.Contains(window.Handle));
        const string pathPrefix = "path:";
        if (string.IsNullOrEmpty(info.AppKey) ||
            !info.AppKey.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string path = info.AppKey[pathPrefix.Length..];
        if (!File.Exists(path)) return false;

        string processName = Path.GetFileNameWithoutExtension(path);
        string? argument = processName.ToLowerInvariant() switch
        {
            "explorer" => null,
            "chrome"   => "--new-window",
            "brave"    => "--new-window",
            "msedge"   => "--new-window",
            "code"     => "--new-window",
            _          => null,
        };
        if (argument is null && !processName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            return false;

        launch = new OpenNewWindowLaunch(path, argument);
        return true;
    }

    private void OpenNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        OpenNewWindowLaunch launch;
        if (item.Tag is OpenNewWindowLaunch groupedLaunch)
            launch = groupedLaunch;
        else if (item.Tag is "openNewWindow" &&
                 ContextMenu.ItemsControlFromItemContainer(item) is ContextMenu menu &&
                 menu.PlacementTarget is Button { DataContext: WindowChipVm chip } &&
                 TryGetOpenNewWindowLaunch(chip, out var singletonLaunch))
            launch = singletonLaunch;
        else
            return;

        OpenNewWindow(launch);
    }

    private static void OpenNewWindow(OpenNewWindowLaunch launch)
    {
        try
        {
            var startInfo = new ProcessStartInfo(launch.Path) { UseShellExecute = true };
            if (launch.Argument is not null) startInfo.ArgumentList.Add(launch.Argument);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenNewWindow] launch failed path={launch.Path} error={ex.Message}");
        }
    }

    private void ChipButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Button { DataContext: WindowChipVm { IsGrouped: true } group } btn)
            return;

        e.Handled = true;
        OpenGroupedWindowMenu(btn, group);
    }

    private void ChipMinimizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextWindow(sender, out var hwnd))
            ShowWindow(hwnd, SW_MINIMIZE);
    }

    private void ChipRestoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetContextWindow(sender, out var hwnd)) return;
        RestoreAndActivateWindow(hwnd, "[ChipRestore]");
    }

    private void RestoreAndActivateWindow(IntPtr hwnd, string tag)
    {
        bool iconicBefore = IsIconic(hwnd);
        if (iconicBefore) ShowWindow(hwnd, SW_RESTORE);
        bool iconicAfter = IsIconic(hwnd);

        GetWindowThreadProcessId(hwnd, out uint targetPid);
        string targetProc = "(unknown)";
        try { targetProc = Process.GetProcessById((int)targetPid).ProcessName; } catch { }

        var fgBefore = GetForegroundWindow();
        var sw16 = Stopwatch.StartNew();
        bool sfwOk = SetForegroundWindow(hwnd);
        int sfwErr = Marshal.GetLastWin32Error();
        sw16.Stop();
        var fgAfter      = GetForegroundWindow();
        bool fgMatch      = fgAfter == hwnd;
        bool elevMismatch = !sfwOk && sfwErr == 5; // probable elevation/UIPI denial

        Debug.WriteLine(
            $"{tag} hwnd=0x{hwnd:X8} proc={targetProc} " +
            $"iconic={iconicBefore}→{iconicAfter} " +
            $"sfw={sfwOk} err={sfwErr} elev={elevMismatch} " +
            $"elapsed={sw16.ElapsedMilliseconds}ms " +
            $"fg_before=0x{fgBefore:X8} fg_after=0x{fgAfter:X8} fg_match={fgMatch}");

        if (elevMismatch)
        {
            Debug.WriteLine($"{tag} probable elevation/UIPI denial — retry skipped hwnd=0x{hwnd:X8}");
            return;
        }

        // Build17: optimistic active update — instant chip highlight on success
        if (sfwOk)
        {
            foreach (var vm in _chipVms)
                vm.IsActive = vm.Handles.Contains(hwnd);
            RecalcChipWidth();
        }

        // Build16: one-shot 50ms retry if SFW failed or foreground did not switch
        if (!sfwOk || !fgMatch)
            ScheduleForegroundRetry(hwnd, $"{tag}.retry");
    }

    private void ChipCloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetContextWindow(sender, out var hwnd))
            TryCloseWindow(hwnd, "[ChipClose]");
    }

    private void ChipContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || menu.PlacementTarget is not Button btn) return;
        bool isMinimized = btn.DataContext is WindowChipVm { IsMinimized: true };
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is "minimize") item.Visibility = isMinimized ? Visibility.Collapsed : Visibility.Visible;
            else if (item.Tag is "restore")  item.Visibility = isMinimized ? Visibility.Visible  : Visibility.Collapsed;
            else if (item.Tag is "openNewWindow")
                item.Visibility = btn.DataContext is WindowChipVm chip &&
                                  TryGetOpenNewWindowLaunch(chip, out _)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    // One-shot 50ms DispatcherTimer retry for SetForegroundWindow; Stop() is the first Tick action.
    private void ScheduleForegroundRetry(IntPtr hwnd, string tag)
    {
        var retryTimer = new DispatcherTimer(DispatcherPriority.Normal)
            { Interval = TimeSpan.FromMilliseconds(50) };
        retryTimer.Tick += (_, _) =>
        {
            retryTimer.Stop();
            if (!IsWindow(hwnd) || GetForegroundWindow() == hwnd) return;
            bool retrySfw     = SetForegroundWindow(hwnd);
            int  retryErr     = Marshal.GetLastWin32Error();
            bool retrySuccess = GetForegroundWindow() == hwnd;
            Debug.WriteLine(
                $"{tag} hwnd=0x{hwnd:X8} delay=50ms sfw={retrySfw} err={retryErr} " +
                $"fg_after=0x{GetForegroundWindow():X8} success={retrySuccess}");
        };
        retryTimer.Start();
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

    private int PhysicalBarHeight() => (int)Math.Round(_barHeightDip * GetDpiScale());

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
