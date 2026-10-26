using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TempChain.Chat;

public sealed class SettingsStore
{
    public string BaseDirectory { get; }
    public string SettingsPath => Path.Combine(BaseDirectory, "settings.json");
    public string? LoadWarning { get; private set; }

    public SettingsStore(string? baseDirectory = null) => BaseDirectory = baseDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TempChain", "Chat");

    public AppSettings Load()
    {
        LoadWarning = null;
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            if (new FileInfo(SettingsPath).Length > 64 * 1024) throw new JsonException();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
            if (settings is null) throw new JsonException();
            return settings.Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadWarning = "설정 파일을 읽지 못해 기본 환경으로 시작했습니다. 설정에서 확인해 주세요.";
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        if (settings.AutoLockMinutes is < 1 or > 60)
            throw new ArgumentOutOfRangeException(nameof(settings), "자동 잠금 시간은 1~60분입니다.");
        if (settings.NotificationDurationSeconds is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(settings), "알림 시간은 3~5초입니다.");
        Directory.CreateDirectory(BaseDirectory);
        var temporary = SettingsPath + ".tmp";
        var content = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, SettingsPath, overwrite: true);
    }
}
