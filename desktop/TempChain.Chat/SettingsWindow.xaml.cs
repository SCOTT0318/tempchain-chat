using System;
using System.IO;
using System.Security;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TempChain.Chat;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    private NotificationWindow? preview;
    public AppSettings Result { get; private set; }
    public bool ChangeLockPasswordRequested { get; private set; }
    public bool LockRequested { get; private set; }

    public SettingsWindow(SettingsStore store, AppSettings current)
    {
        InitializeComponent();
        this.store = store;
        Result = current;
        TrayCheckBox.IsChecked = current.MinimizeToTray;
        StartupCheckBox.IsChecked = current.StartWithWindows;
        AutoUpdateCheckBox.IsChecked = current.AutoUpdate;
        DurationBox.SelectedIndex = current.Normalize().NotificationDurationSeconds - 3;
        SenderBox.SelectedIndex = current.ShowNotificationSender ? 0 : 1;
        AutoLockMinutesBox.Text = current.AutoLockMinutes.ToString(CultureInfo.InvariantCulture);
        DurationBox.SelectionChanged += NotificationPreference_Changed;
        SenderBox.SelectionChanged += NotificationPreference_Changed;
        Activated += (_, _) => RefreshShortcuts();
        RefreshShortcuts();
        AppUpdateManager.Instance.Changed += RefreshUpdate;
        RefreshUpdate();
        Closed += (_, _) => { preview?.Close(); AppUpdateManager.Instance.Changed -= RefreshUpdate; };
    }

    private void NotificationPreference_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!TryGetDuration(out var seconds)) return;
        try
        {
            Result = store.Load() with { NotificationDurationSeconds = seconds, ShowNotificationSender = SenderBox.SelectedIndex == 0 };
            store.Save(Result);
        }
        catch (Exception) { ErrorText.Text = "알림 설정을 저장하지 못했습니다. 다시 시도해 주세요."; }
    }

    private void ChangeLock_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings()) return;
        ChangeLockPasswordRequested = true; DialogResult = true;
    }
    private void LockNow_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings()) return;
        LockRequested = true; DialogResult = true;
    }

    private void RefreshUpdate()
    {
        var updates = AppUpdateManager.Instance;
        CurrentVersionText.Text = "v" + AppUpdateManager.CurrentVersionText;
        LatestVersionText.Text = updates.LatestVersionText;
        UpdateStatusText.Text = updates.Status;
        UpdateStatusText.Visibility = string.IsNullOrWhiteSpace(updates.Status) ? Visibility.Collapsed : Visibility.Visible;
        CheckUpdateButton.IsEnabled = !updates.IsBusy;
        ApplyUpdateButton.IsEnabled = updates.CanInstall;
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) => await AppUpdateManager.Instance.CheckAndDownloadAsync();
    private void ApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings()) return;
        DialogResult = true;
        Dispatcher.BeginInvoke(new Action(() => AppUpdateManager.Instance.InstallAndRestart()));
    }

    private bool TryGetDuration(out int seconds)
    {
        if (DurationBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out seconds) && seconds is >= 3 and <= 5)
        { ErrorText.Text = ""; return true; }
        seconds = 5;
        ErrorText.Text = "알림 표시 시간을 선택해 주세요.";
        DurationBox.Focus(); return false;
    }

    private void PreviewNotification_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDuration(out var seconds)) return;
        preview?.Close();
        var content = NotificationContent.Create("홍길동", "팀 채팅방", SenderBox.SelectedIndex == 0);
        preview = new NotificationWindow(content.Title, content.Detail, seconds, true);
        preview.Show();
    }

    private void DesktopShortcut_Click(object sender, RoutedEventArgs e) => CreateShortcut(true);
    private void StartShortcut_Click(object sender, RoutedEventArgs e) => CreateShortcut(false);
    private void RefreshShortcuts()
    {
        var desktopExists = File.Exists(ShortcutRegistration.GetPath(true));
        var startExists = File.Exists(ShortcutRegistration.GetPath(false));
        DesktopShortcutButton.IsEnabled = !desktopExists;
        StartShortcutButton.IsEnabled = !startExists;
        DesktopShortcutStatus.Text = desktopExists ? "추가됨" : "없음";
        StartShortcutStatus.Text = startExists ? "추가됨" : "없음";
    }
    private void CreateShortcut(bool desktop)
    {
        try
        {
            if (File.Exists(ShortcutRegistration.GetPath(desktop))) { RefreshShortcuts(); return; }
            ShortcutRegistration.Create(desktop, overwrite: false);
            ShortcutStatusText.Text = desktop ? "바탕화면에 추가했습니다." : "시작 메뉴에 추가했습니다.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or SecurityException)
        { ErrorText.Text = "바로가기를 만들지 못했습니다. 설치된 앱인지, Windows 폴더 쓰기 권한이 있는지 확인해 주세요."; }
        finally { RefreshShortcuts(); }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSettings()) DialogResult = true;
    }

    private bool SaveSettings()
    {
        if (!TryGetDuration(out var seconds)) return false;
        if (!int.TryParse(AutoLockMinutesBox.Text, out var lockMinutes) || lockMinutes is < 1 or > 60)
        { ErrorText.Text = "자동 잠금 시간은 1~60분으로 입력해 주세요."; AutoLockMinutesBox.Focus(); return false; }
        var updated = store.Load() with {
            MinimizeToTray = TrayCheckBox.IsChecked == true,
            StartWithWindows = StartupCheckBox.IsChecked == true,
            AutoUpdate = AutoUpdateCheckBox.IsChecked == true,
            ShowNotificationSender = SenderBox.SelectedIndex == 0,
            NotificationDurationSeconds = seconds,
            AutoLockMinutes = lockMinutes
        };
        try
        {
            StartupRegistration.Apply(updated.StartWithWindows);
            try { store.Save(updated); }
            catch { StartupRegistration.Apply(Result.StartWithWindows); throw; }
            Result = updated;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
        {
            ErrorText.Text = "설정을 저장하지 못했습니다. 사용자 폴더와 자동 실행 권한을 확인하고 다시 시도하세요.";
            return false;
        }
    }
}
