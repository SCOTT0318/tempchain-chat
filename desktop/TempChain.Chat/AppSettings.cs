namespace TempChain.Chat;

public sealed record AppSettings
{
    public bool MinimizeToTray { get; init; } = true;
    public bool StartWithWindows { get; init; } = false;
}
