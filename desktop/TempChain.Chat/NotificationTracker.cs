using System.Collections.Generic;
using System.Linq;
using TempChain.Chat.Models;

namespace TempChain.Chat;

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
