using System;

namespace TempChain.Chat;

public sealed record AppSettings
{
    public bool MinimizeToTray { get; init; } = true;
    public bool StartWithWindows { get; init; } = false;
    public bool ShowNotificationSender { get; init; } = true;
    public int NotificationDurationSeconds { get; init; } = 5;

    public AppSettings Normalize() => this with {
        NotificationDurationSeconds = Math.Clamp(NotificationDurationSeconds, 3, 5)
    };
}
