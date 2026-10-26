using System;

namespace TempChain.Chat;

public sealed record AppSettings
{
    public bool MinimizeToTray { get; init; } = true;
    public bool StartWithWindows { get; init; } = false;
    public bool EnableNotifications { get; init; } = true;
    public bool AnimateNotifications { get; init; } = true;
    public bool NotificationSound { get; init; } = false;
    public int NotificationDurationSeconds { get; init; } = 5;

    public AppSettings Normalize() => this with {
        NotificationDurationSeconds = Math.Clamp(NotificationDurationSeconds, 1, 60)
    };
}
