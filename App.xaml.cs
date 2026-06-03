using System.Windows;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        DiagnosticsLog.Initialize();
        Exit += (_, _) => DiagnosticsLog.Shutdown();

        var settings = SettingsService.Load();

        WinFormsScreen[] screens = settings.MonitorMode == "primary" && WinFormsScreen.PrimaryScreen is { } ps
            ? [ps]
            : WinFormsScreen.AllScreens;

        double barHeightDip = settings.DensityPreset switch
        {
            "comfortable" => 36,
            "large"       => 40,
            _             => 32,
        };

        foreach (var screen in screens)
            new TopBarWindow(screen, barHeightDip).Show();
    }
}
