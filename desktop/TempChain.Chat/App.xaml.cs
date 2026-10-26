using System;
using System.Linq;
using System.Windows;

namespace TempChain.Chat;

public partial class App : Application
{
    private SingleInstanceCoordinator? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new SingleInstanceCoordinator();
        if (!instance.IsFirstInstance)
        {
            instance.ActivateExisting();
            Shutdown();
            return;
        }
        var store = new SettingsStore();
        var window = new MainWindow(store);
        MainWindow = window;
        instance.ActivationRequested += () => Dispatcher.BeginInvoke(window.RestoreWindow);
        window.Show();
        if (e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            window.HideToTray(showBalloon: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppUpdateManager.Instance.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
