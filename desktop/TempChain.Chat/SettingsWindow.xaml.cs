using System;
using System.IO;
using System.Security;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;

namespace TempChain.Chat;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    private NotificationWindow? preview;
    public AppSettings Result { get; private set; }

    public SettingsWindow(SettingsStore store, AppSettings current)
    {
        InitializeComponent();
        this.store = store;
        Result = current;
        TrayCheckBox.IsChecked = current.MinimizeToTray;
        StartupCheckBox.IsChecked = current.StartWithWindows;
        NotificationsCheckBox.IsChecked = current.EnableNotifications;
        AnimationCheckBox.IsChecked = current.AnimateNotifications;
        SoundCheckBox.IsChecked = current.NotificationSound;
        DurationBox.Text = current.Normalize().NotificationDurationSeconds.ToString(CultureInfo.InvariantCulture);
        Closed += (_, _) => preview?.Close();
    }

    private bool TryGetDuration(out int seconds)
    {
        if (int.TryParse(DurationBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out seconds) && seconds is >= 1 and <= 60)
        { ErrorText.Text = ""; return true; }
        ErrorText.Text = "알림 표시 시간은 1~60 사이의 정수로 입력해 주세요.";
        DurationBox.Focus(); return false;
    }

    private void PreviewNotification_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDuration(out var seconds)) return;
        preview?.Close();
        preview = new NotificationWindow("알림 미리 보기", $"새 메시지 알림은 {seconds}초 동안 표시됩니다.", seconds, AnimationCheckBox.IsChecked == true);
        preview.Show();
    }

    private void DesktopShortcut_Click(object sender, RoutedEventArgs e) => CreateShortcut(true);
    private void StartShortcut_Click(object sender, RoutedEventArgs e) => CreateShortcut(false);
    private void CreateShortcut(bool desktop)
    {
        try
        {
            var overwrite = File.Exists(ShortcutRegistration.GetPath(desktop));
            if (overwrite && MessageBox.Show(this, "같은 이름의 바로가기가 있습니다. 현재 앱으로 다시 연결할까요?", "바로가기 만들기",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            ShortcutRegistration.Create(desktop, overwrite);
            ShortcutStatusText.Text = desktop ? "바탕화면 아이콘을 만들었습니다." : "시작 메뉴 아이콘을 만들었습니다.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or SecurityException)
        { ErrorText.Text = "바로가기를 만들지 못했습니다. 설치된 앱인지, Windows 폴더 쓰기 권한이 있는지 확인해 주세요."; }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDuration(out var seconds)) return;
        var updated = new AppSettings {
            MinimizeToTray = TrayCheckBox.IsChecked == true,
            StartWithWindows = StartupCheckBox.IsChecked == true,
            EnableNotifications = NotificationsCheckBox.IsChecked == true,
            AnimateNotifications = AnimationCheckBox.IsChecked == true,
            NotificationSound = SoundCheckBox.IsChecked == true,
            NotificationDurationSeconds = seconds
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
