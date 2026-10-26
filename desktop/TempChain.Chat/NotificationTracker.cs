using System.Collections.Generic;
using System.Linq;
using TempChain.Chat.Models;

namespace TempChain.Chat;

public sealed record NotificationContent(string Title, string Detail)
{
    public static NotificationContent Create(string sender, string room, bool showSender) => showSender
        ? new(string.IsNullOrWhiteSpace(sender) ? "새 메시지" : sender, string.IsNullOrWhiteSpace(room) || room == sender ? "" : room)
        : new("새 메시지", "");
}

/// <summary>Baseline per channel, not the currently displayed message list. Never marks a post read.</summary>
public sealed class NotificationTracker
{
    private readonly Dictionary<long, long> latest = new();

    public IReadOnlyList<ChatConversation> Observe(IReadOnlyList<ChatConversation> rooms)
    {
        var changed = new List<ChatConversation>();
        var present = rooms.Select(room => room.Id).ToHashSet();
        foreach (var room in rooms)
        {
            if (latest.TryGetValue(room.Id, out var previous) && room.LastPostAt > previous && room.UnreadCount > 0 && !room.IsSelf)
                changed.Add(room);
            latest[room.Id] = latest.TryGetValue(room.Id, out previous) ? System.Math.Max(previous, room.LastPostAt) : room.LastPostAt;
        }
        foreach (var id in latest.Keys.Where(id => !present.Contains(id)).ToArray()) latest.Remove(id);
        return changed.OrderBy(room => room.LastPostAt).ToArray();
    }

    public void Reset() => latest.Clear();
}
