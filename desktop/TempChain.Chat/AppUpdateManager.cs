using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TempChain.Chat.Services;

namespace TempChain.Chat;

public sealed class AppUpdateManager : IDisposable
{
    public static AppUpdateManager Instance { get; } = new();
    public static Version CurrentVersion { get; } = GetVersion();
    public static string CurrentVersionText => CurrentVersion.ToString(3);
    private readonly ReleaseUpdateClient client = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool busy, autoEnabled, started;
    private string? installer;
    private UpdateRelease? release;
    public bool IsBusy => busy;
    public bool IsReady => installer is not null;
    public bool CanInstall => IsReady && !busy && UpdateInstaller.IsInstalledApp;
    public string LatestVersionText { get; private set; } = "미확인";
    public string Status { get; private set; } = "";
    public event Action? Changed;

    private static Version GetVersion()
    {
        var version = typeof(AppUpdateManager).Assembly.GetName().Version ?? new Version(1, 0, 0);
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    public void Configure(bool enabled)
    {
        var wasEnabled = autoEnabled;
        autoEnabled = enabled;
        if (!started) { started = true; _ = AutomaticLoopAsync(); }
        if (enabled && !wasEnabled) _ = CheckAndDownloadAsync();
    }

    private async Task AutomaticLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
            while (await timer.WaitForNextTickAsync(lifetime.Token))
                if (autoEnabled) await CheckAndDownloadAsync();
        }
        catch (OperationCanceledException) { }
    }

    public async Task CheckAndDownloadAsync()
    {
        if (busy || lifetime.IsCancellationRequested) return;
        busy = true;
        Status = "확인 중…"; Changed?.Invoke();
        try
        {
            var latest = await client.CheckAsync(CurrentVersion, lifetime.Token);
            if (latest is null)
            {
                // A prepared newer version remains usable if a release is temporarily rolled back.
                LatestVersionText = IsReady ? release!.Version.ToString(3) : client.LatestVersion?.ToString(3) ?? "정식 릴리스 없음";
                Status = IsReady ? "업데이트 준비됨" : "최신 버전입니다.";
                return;
            }
            LatestVersionText = latest.Version.ToString(3);
            if (installer is null || release?.Sha256 != latest.Sha256)
            {
                var progress = new Progress<int>(value => { Status = $"v{LatestVersionText} 다운로드 중 · {value}%"; Changed?.Invoke(); });
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempChain", "Chat", "Updates");
                var downloaded = await client.DownloadAsync(latest, root, progress, lifetime.Token);
                installer = downloaded;
                release = latest;
            }
            Status = UpdateInstaller.IsInstalledApp ? "업데이트 준비됨" : "설치 버전에서 업데이트할 수 있습니다.";
        }
        catch (OperationCanceledException) { Status = "업데이트 확인이 취소되었거나 응답 시간이 초과됐습니다."; }
        catch (InvalidOperationException ex) { Status = ex.Message; }
        catch (Exception) { Status = "업데이트를 확인하지 못했습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요."; }
        finally { busy = false; Changed?.Invoke(); }
    }

    public void InstallAndRestart()
    {
        if (!CanInstall || release is null || installer is null) return;
        if (Application.Current.MainWindow is not MainWindow window || !window.CanRestartForUpdate)
        { Status = "진행 중인 작업과 작성 중인 메시지를 먼저 마무리해 주세요."; Changed?.Invoke(); return; }
        try
        {
            UpdateInstaller.Start(installer, release.Sha256, release.Version.ToString(3));
            window.ShutdownForUpdate();
        }
        catch (Exception) { Status = "업데이트를 시작하지 못했습니다. 다시 확인한 뒤 시도해 주세요."; Changed?.Invoke(); }
    }

    public void Dispose() { lifetime.Cancel(); client.Dispose(); lifetime.Dispose(); }
}
