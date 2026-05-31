using System.Windows;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var settings = SettingsService.Load();

        WinFormsScreen[] screens = settings.MonitorMode == "primary" && WinFormsScreen.PrimaryScreen is { } ps
            ? [ps]
            : WinFormsScreen.AllScreens;

        foreach (var screen in screens)
            new TopBarWindow(screen).Show();
    }
}
