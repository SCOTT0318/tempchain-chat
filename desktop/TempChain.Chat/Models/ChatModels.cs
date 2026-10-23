using System;
using System.Collections.Generic;

namespace TempChain.Chat.Models;

public sealed record ChatUser(long Id, string UserName, string DisplayName, bool IsActive = true);

public sealed record ChatConversation(long Id, string Name, string Kind,
    IReadOnlyList<long> MemberIds, bool IsEncrypted = false, bool IsSelf = false);

public sealed record ChatMessage(long Id, long ChannelId, long UserId, string UserName,
    string Text, DateTimeOffset SentAt, bool IsOwn, bool HasAttachments = false);
