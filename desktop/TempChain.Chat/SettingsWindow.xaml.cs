using System;
using System.IO;
using System.Security;
using System.Windows;

namespace TempChain.Chat;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    public AppSettings Result { get; private set; }

    public SettingsWindow(SettingsStore store, AppSettings current)
    {
        InitializeComponent();
        this.store = store;
        Result = current;
        TrayCheckBox.IsChecked = current.MinimizeToTray;
        StartupCheckBox.IsChecked = current.StartWithWindows;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var updated = new AppSettings {
            MinimizeToTray = TrayCheckBox.IsChecked == true,
            StartWithWindows = StartupCheckBox.IsChecked == true
        };
        try
        {
            StartupRegistration.Apply(updated.StartWithWindows);
            try { store.Save(updated); }
            catch { StartupRegistration.Apply(Result.StartWithWindows); throw; }
            Result = updated;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            ErrorText.Text = "설정을 저장하지 못했습니다. 사용자 폴더와 자동 실행 권한을 확인하고 다시 시도하세요.";
        }
    }
}
