using System.Windows;
using System.Windows.Threading;
using PandaPet.Services;

namespace PandaPet;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        var mainWindow = new MainWindow(settings, settingsService);
        MainWindow = mainWindow;
        mainWindow.Show();

        if (string.IsNullOrWhiteSpace(settings.ChildName))
        {
            mainWindow.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => mainWindow.OpenSettings(isFirstRun: true)));
        }
    }
}
