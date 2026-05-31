using System.Windows;
using WinFormsScreen = System.Windows.Forms.Screen;

namespace TopBarPoC;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        foreach (var screen in WinFormsScreen.AllScreens)
            new TopBarWindow(screen).Show();
    }
}
