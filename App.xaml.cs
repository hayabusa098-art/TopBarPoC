using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

public partial class App : Application
{
    private readonly Dictionary<string, TopBarWindow> _topBars =
        new(StringComparer.OrdinalIgnoreCase);
    private AppSettings _settings = new();
    private double _barHeightDip;
    private DispatcherTimer? _displayChangeTimer;
    private bool _displaySettingsSubscribed;

    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        DiagnosticsLog.Initialize();
        Exit += App_OnExit;

        _settings = SettingsService.Load();
        _barHeightDip = _settings.DensityPreset switch
        {
            "comfortable" => 36,
            "large"       => 40,
            _             => 32,
        };

        _displayChangeTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(750),
        };
        _displayChangeTimer.Tick += DisplayChangeTimer_Tick;

        SynchronizeTopBars();
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        _displaySettingsSubscribed = true;
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
        {
            _displayChangeTimer?.Stop();
            _displayChangeTimer?.Start();
        }));
    }

    private void DisplayChangeTimer_Tick(object? sender, EventArgs e)
    {
        _displayChangeTimer?.Stop();
        SynchronizeTopBars();
    }

    private void SynchronizeTopBars()
    {
        var expectedScreens = GetExpectedScreens()
            .GroupBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        if (expectedScreens.Count == 0) return;

        foreach (var (deviceName, screen) in expectedScreens)
        {
            if (_topBars.ContainsKey(deviceName)) continue;

            var window = new TopBarWindow(screen, _barHeightDip);
            _topBars.Add(deviceName, window);
            window.Closed += (_, _) =>
            {
                if (_topBars.TryGetValue(deviceName, out var current) && ReferenceEquals(current, window))
                    _topBars.Remove(deviceName);
            };
            window.Show();
        }

        foreach (var deviceName in _topBars.Keys
                     .Where(deviceName => !expectedScreens.ContainsKey(deviceName))
                     .ToList())
        {
            var window = _topBars[deviceName];
            _topBars.Remove(deviceName);
            window.Close();
        }

        foreach (var window in _topBars.Values)
            window.QueueDisplayRefresh();
    }

    private WinFormsScreen[] GetExpectedScreens()
    {
        if (_settings.MonitorMode != "primary")
            return WinFormsScreen.AllScreens;

        return WinFormsScreen.PrimaryScreen is { } primary
            ? [primary]
            : WinFormsScreen.AllScreens.Take(1).ToArray();
    }

    private void App_OnExit(object sender, ExitEventArgs e)
    {
        _displayChangeTimer?.Stop();
        if (_displaySettingsSubscribed)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
            _displaySettingsSubscribed = false;
        }
        DiagnosticsLog.Shutdown();
    }
}
