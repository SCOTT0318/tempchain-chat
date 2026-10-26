using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TempChain.Chat.Services;

namespace TempChain.Chat;

public partial class MainWindow
{
    private PersistentLoginStore loginStore = null!;
    private SavedLogin? savedLogin;
    private (string User, string Password)? pendingLogin;
    private bool securityLocked, settingLockPassword, unlocking;
    private long nextUnlockAttempt;
    private long nextSessionRecovery;
    private bool sessionRecoveryRejected;
    private bool directoryAccessBlocked, accessRevocationPending;
    private string lockInputMode = "pin4";
    private readonly AppLockTimer lockTimer = new(Environment.TickCount64);
    private readonly DispatcherTimer securityTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private void InitializeSecurity()
    {
        loginStore = new PersistentLoginStore(store.BaseDirectory);
        InputManager.Current.PreProcessInput += OnSecurityInput;
        SystemEvents.SessionSwitch += OnWindowsSessionSwitch;
        securityTimer.Tick += (_, _) => {
            if (client is not null && !securityLocked && lockTimer.IsDue(Environment.TickCount64, settings.AutoLockMinutes)) LockApp();
        };
        securityTimer.Start();
        Closed += (_, _) => {
            securityTimer.Stop();
            InputManager.Current.PreProcessInput -= OnSecurityInput;
            SystemEvents.SessionSwitch -= OnWindowsSessionSwitch;
            pendingLogin = null; savedLogin = null;
        };
    }

    private void OnSecurityInput(object sender, PreProcessInputEventArgs e)
    {
        if (!securityLocked && (IsActive || OwnedWindows.Cast<Window>().Any(window => window.IsActive)) &&
            e.StagingItem.Input is KeyboardEventArgs or MouseEventArgs or TouchEventArgs)
            lockTimer.Touch(Environment.TickCount64);
    }

    private void OnWindowsSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff)
            Dispatcher.BeginInvoke(LockApp);
    }

    private void RestoreSavedLogin()
    {
        try
        {
            savedLogin = loginStore.Load();
            if (savedLogin is not null) ShowSecurityGate(false);
        }
        catch (Exception)
        {
            // Fail closed: saved secrets are never used when the vault cannot be read.
            LoginErrorText.Text = "저장된 로그인 정보를 열지 못했습니다. ERP 계정으로 다시 로그인해 주세요.";
        }
    }

    private void ShowSecurityGate(bool setup)
    {
        securityLocked = true; settingLockPassword = setup;
        notification?.Close(); notification = null;
        foreach (var window in OwnedWindows.Cast<Window>().ToArray()) window.Close();
        ChatView.Visibility = LoginView.Visibility = Visibility.Collapsed;
        LockHeading.Text = setup ? "앱 잠금 암호 설정" : "";
        LockHeading.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
        LockPasswordLabel.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
        LockConfirmPanel.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
        LockSubmitButton.Content = setup ? "암호 설정 및 시작" : "잠금 해제";
        LockErrorText.Text = "";
        LockModePanel.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
        lockInputMode = !setup && string.IsNullOrEmpty(savedLogin?.LockMode) ? "text" :
            savedLogin?.LockMode is "pin6" or "text" ? savedLogin.LockMode : "pin4";
        Pin4Option.IsChecked = lockInputMode == "pin4";
        Pin6Option.IsChecked = lockInputMode == "pin6";
        TextPasswordOption.IsChecked = lockInputMode == "text";
        ConfigureLockInput();
        LockOverlay.Visibility = Visibility.Visible;
        FocusLockInput();
    }

    private void LockMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!settingLockPassword || LockPinInput is null || AppLockPasswordBox is null) return;
        lockInputMode = Pin6Option.IsChecked == true ? "pin6" : TextPasswordOption.IsChecked == true ? "text" : "pin4";
        ConfigureLockInput();
        FocusLockInput();
    }

    private void ConfigureLockInput()
    {
        var numeric = lockInputMode != "text";
        var length = lockInputMode == "pin6" ? 6 : 4;
        LockPinInput.Length = LockPinConfirmInput.Length = length;
        LockPinInput.Visibility = LockPinConfirmInput.Visibility = numeric ? Visibility.Visible : Visibility.Collapsed;
        AppLockPasswordBox.Visibility = AppLockConfirmBox.Visibility = numeric ? Visibility.Collapsed : Visibility.Visible;
        AppLockPasswordBox.Clear(); AppLockConfirmBox.Clear();
        LockErrorText.Text = "";
    }

    private void FocusLockInput()
    {
        if (lockInputMode == "text") AppLockPasswordBox.Focus();
        else LockPinInput.FocusFirst();
    }

    private void LockApp()
    {
        if (client is not null && !securityLocked && savedLogin is not null) ShowSecurityGate(false);
    }

    private void Lock_Click(object sender, RoutedEventArgs e) => LockApp();

    private void UnlockApp()
    {
        securityLocked = settingLockPassword = false; pendingLogin = null;
        LockOverlay.Visibility = Visibility.Collapsed;
        AppLockPasswordBox.Clear(); AppLockConfirmBox.Clear();
        LockPinInput.Clear(); LockPinConfirmInput.Clear();
        ChatView.Visibility = client is null ? Visibility.Collapsed : Visibility.Visible;
        LoginView.Visibility = client is null ? Visibility.Visible : Visibility.Collapsed;
        lockTimer.Touch(Environment.TickCount64);
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (unlocking) return;
        if (Environment.TickCount64 < nextUnlockAttempt) { LockErrorText.Text = "잠시 후 다시 입력해 주세요."; return; }
        var password = lockInputMode == "text" ? AppLockPasswordBox.Password : LockPinInput.Password;
        if (lockInputMode != "text" && password.Length != LockPinInput.Length)
        { LockErrorText.Text = $"숫자 {LockPinInput.Length}자리를 모두 입력해 주세요."; return; }
        unlocking = true; LockSubmitButton.IsEnabled = false;
        try
        {
            if (settingLockPassword)
            {
                var confirmation = lockInputMode == "text" ? AppLockConfirmBox.Password : LockPinConfirmInput.Password;
                if (password != confirmation) { LockErrorText.Text = "두 암호가 일치하지 않습니다."; return; }
                if (lockInputMode == "text" && AppLockPassword.ModeFor(password) != "text")
                { LockErrorText.Text = "문자를 포함해 6자 이상 입력해 주세요."; return; }
                if (pendingLogin is not { } pending) return;
                var created = await Task.Run(() => AppLockPassword.Create(pending.User, pending.Password, password));
                loginStore.Save(created); savedLogin = created;
                UnlockApp();
            }
            else if (savedLogin is { } login)
            {
                if (!await Task.Run(() => AppLockPassword.Verify(login, password)))
                {
                    nextUnlockAttempt = Environment.TickCount64 + 3000;
                    LockErrorText.Text = "잠금 암호가 올바르지 않습니다."; AppLockPasswordBox.Clear(); LockPinInput.Clear(); FocusLockInput(); return;
                }
                if (string.IsNullOrEmpty(login.LockMode))
                { savedLogin = login with { LockMode = AppLockPassword.ModeFor(password) }; loginStore.Save(savedLogin); }
                if (client is null) await SignInAsync(login.UserName, login.Password);
                if (sessionRecoveryRejected)
                {
                    var reason = LoginErrorText.Text;
                    await SignOutAsync();
                    LoginErrorText.Text = string.IsNullOrWhiteSpace(reason) ? "ERP 계정 정보가 변경되어 다시 로그인이 필요합니다." : reason;
                    return;
                }
                if (directoryAccessBlocked && client is { } active)
                {
                    try { await active.GetUsersAsync(); directoryAccessBlocked = false; }
                    catch (Exception ex) { LockErrorText.Text = FriendlyError(ex); return; }
                }
                if (client is not null) UnlockApp();
                else LockErrorText.Text = LoginErrorText.Text;
            }
        }
        catch (ArgumentException ex) { LockErrorText.Text = ex.Message; }
        catch (Exception) { LockErrorText.Text = "로그인 정보를 안전하게 저장하지 못했습니다. 다시 시도해 주세요."; }
        finally { unlocking = false; LockSubmitButton.IsEnabled = true; }
    }

    private void LockPassword_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; Unlock_Click(sender, e); }
    }

    private void ChangeLockPassword_Click(object sender, RoutedEventArgs e)
    {
        if (savedLogin is null || securityLocked) return;
        pendingLogin = (savedLogin.UserName, savedLogin.Password);
        ShowSecurityGate(true);
    }

    private void OnServerAccessRestriction(SynologyChatClient active, int code)
    {
        if (code == 461)
        {
            Dispatcher.BeginInvoke(() => {
                if (!ReferenceEquals(client, active)) return;
                directoryAccessBlocked = true; LockApp();
                LockErrorText.Text = "계정 정보를 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            });
            return;
        }
        if (accessRevocationPending) return;
        accessRevocationPending = true;
        Dispatcher.BeginInvoke(async () => {
            try
            {
                if (!ReferenceEquals(client, active)) return;
                await SignOutAsync();
                LoginErrorText.Text = "ERP 그룹에 포함된 계정만 사용할 수 있습니다.";
            }
            finally { accessRevocationPending = false; }
        });
    }

    private async Task RecoverSessionAsync(SynologyChatClient active, System.Threading.CancellationToken token)
    {
        if (savedLogin is null || sessionRecoveryRejected || Environment.TickCount64 < nextSessionRecovery) return;
        nextSessionRecovery = Environment.TickCount64 + 60000;
        try { await active.LoginAsync(savedLogin.UserName, savedLogin.Password, token); }
        catch (SynologyApiException ex) when (ex.Code is 400 or 401 or 403 or 404 or 407 or 408 or 409)
        {
            sessionRecoveryRejected = true;
            LockApp();
            LockErrorText.Text = "ERP 계정 정보가 변경되었습니다. ERP 계정으로 다시 로그인해 주세요.";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { StatusText.Text = "로그인 연결을 복구하지 못했습니다. 잠시 후 자동으로 다시 연결합니다."; }
    }
}
