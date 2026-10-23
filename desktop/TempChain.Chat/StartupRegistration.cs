using System;
using System.IO;
using Microsoft.Win32;

namespace TempChain.Chat;

public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TempChainChat";

    public static string BuildCommand(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !Path.IsPathFullyQualified(exePath) || exePath.Contains('"') ||
            exePath.Contains('\r') || exePath.Contains('\n'))
            throw new ArgumentException("전체 실행 파일 경로가 필요합니다.", nameof(exePath));
        return $"\"{exePath}\" --minimized";
    }

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (enabled)
            key.SetValue(ValueName, BuildCommand(Environment.ProcessPath ?? throw new IOException("실행 파일 경로를 찾지 못했습니다.")), RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
