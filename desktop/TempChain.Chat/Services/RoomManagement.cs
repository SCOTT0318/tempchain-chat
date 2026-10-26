using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TempChain.Chat.Models;

namespace TempChain.Chat.Services;

public sealed class RoomOperationException : Exception
{
    public long ChannelId { get; }
    public RoomOperationException(long channelId, string stage) : base(
        $"채팅방(ID {channelId})은 생성됐지만 {stage}을 완료하지 못했습니다. 새 방을 다시 만들지 말고 목록을 새로 고치거나 공식 Chat에서 해당 방을 확인해 주세요.") => ChannelId = channelId;
}

public sealed partial class SynologyChatClient
{
    private readonly HashSet<string> roomPermissions = new(StringComparer.Ordinal);
    private bool HasApi(string api, int version) => apis.TryGetValue(api, out var info) && info.Minimum <= version && info.Maximum >= version && info.JsonParameters;
    public bool CanCreateRooms => HasApi("SYNO.Chat.Channel.Named", 1) && roomPermissions.Contains("channel_create");
    public bool CanCreatePublicRooms => CanCreateRooms && roomPermissions.Contains("channel_public");
    public bool CanInviteMembers => HasApi("SYNO.Chat.Channel.Named", 1) && roomPermissions.Contains("channel_invite");
    public bool CanLeaveRooms => HasApi("SYNO.Chat.Channel.Named", 1) && roomPermissions.Contains("channel_disjoin");
    public bool CanCloseRooms => HasApi("SYNO.Chat.Channel", 4) && roomPermissions.Contains("channel_close");

    public async Task RefreshRoomPermissionsAsync(CancellationToken ct = default)
    {
        EnsureSession();
        roomPermissions.Clear();
        if (!HasApi("SYNO.Chat.Misc", 1)) return;
        var data = await SendAsync("SYNO.Chat.Misc", "acl", 1, new(), true, ct);
        foreach (var entry in ReadArray(data, "acls"))
            if (ReadBool(entry, false, "allow")) roomPermissions.Add(ReadString(entry, "acl_name"));
    }

    public async Task<ChatConversation> CreateRoomAsync(string name, string purpose, bool isPublic,
        IReadOnlyList<long> participantIds, CancellationToken ct = default)
    {
        EnsureSession();
        name = (name ?? "").Trim(); purpose = (purpose ?? "").Trim();
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl) || purpose.Length > 256)
            throw new SynologyApiException(400, "방 이름은 1~64자, 설명은 256자 이내로 입력하세요.");
        var invitees = ValidateInvitees(participantIds);
        await RefreshRoomPermissionsAsync(ct);
        if (!CanCreateRooms || (isPublic && !CanCreatePublicRooms) || (invitees.Length > 0 && !CanInviteMembers))
            throw new SynologyApiException(404, "채팅방 생성·공개방·초대 권한을 확인해 주세요.");
        var data = await SendAsync("SYNO.Chat.Channel.Named", "create", 1,
            new() { ["type"] = isPublic ? "public" : "private", ["name"] = name, ["purpose"] = purpose, ["encrypted"] = false }, true, ct);
        var id = ReadLong(data, "channel_id");
        if (id <= 0) throw new SynologyApiException(0, "생성 결과가 불확실합니다. 중복 생성하지 말고 공식 Chat에서 방을 확인하세요.");
        try
        {
            await SendAsync("SYNO.Chat.Channel.Named", "join", 1,
                new() { ["channel_id"] = id, ["channel_key_enc"] = "" }, true, ct);
        }
        catch (Exception ex) when (ex is SynologyApiException or OperationCanceledException)
        { throw new RoomOperationException(id, "내 계정 참여"); }
        var room = new ChatConversation(id, name, "channel", new[] { CurrentUser!.Id }, ChannelType: isPublic ? "public" : "private");
        conversations[id] = room;
        if (invitees.Length > 0)
        {
            try { await InviteCoreAsync(id, invitees, ct); }
            catch (Exception ex) when (ex is SynologyApiException or OperationCanceledException)
            { throw new RoomOperationException(id, "선택한 직원 초대"); }
            room = room with { MemberIds = new[] { CurrentUser!.Id }.Concat(invitees).ToArray() };
            conversations[id] = room;
        }
        return room;
    }

    public async Task InviteMembersAsync(long channelId, IReadOnlyList<long> participantIds, CancellationToken ct = default)
    {
        EnsureSession();
        var room = RequireNamedRoom(channelId);
        var invitees = ValidateInvitees(participantIds).Where(id => !room.MemberIds.Contains(id)).ToArray();
        if (invitees.Length == 0) throw new SynologyApiException(400, "아직 참여하지 않은 직원을 선택해 주세요.");
        await RefreshRoomPermissionsAsync(ct);
        if (!CanInviteMembers) throw new SynologyApiException(404, "직원 초대 권한이 없습니다.");
        await InviteCoreAsync(channelId, invitees, ct);
        conversations[channelId] = room with { MemberIds = room.MemberIds.Concat(invitees).Distinct().ToArray() };
    }

    private Task<System.Text.Json.JsonElement> InviteCoreAsync(long channelId, long[] ids, CancellationToken ct) =>
        SendAsync("SYNO.Chat.Channel.Named", "invite", 1,
            new() { ["channel_id"] = channelId, ["user_ids"] = ids, ["channel_key_encs"] = Array.Empty<object>() }, true, ct);

    public async Task LeaveRoomAsync(long channelId, CancellationToken ct = default)
    {
        EnsureSession(); RequireNamedRoom(channelId);
        await RefreshRoomPermissionsAsync(ct);
        if (!CanLeaveRooms) throw new SynologyApiException(404, "채팅방 나가기 권한이 없습니다.");
        // Named.disjoin changes membership. Channel.leave only leaves a socket's active view.
        await SendAsync("SYNO.Chat.Channel.Named", "disjoin", 1, new() { ["channel_id"] = channelId }, true, ct);
        conversations.Remove(channelId); visibility.Remove(channelId);
    }

    public async Task CloseRoomAsync(long channelId, string confirmedName, CancellationToken ct = default)
    {
        EnsureSession();
        // Re-fetch membership and name before a destructive operation. Never trust stale UI metadata.
        await GetConversationsAsync(ct);
        var room = RequireNamedRoom(channelId);
        if (!string.Equals(room.Name, confirmedName, StringComparison.Ordinal))
            throw new SynologyApiException(400, "방 이름이 일치하지 않습니다. 새로 고친 이름을 확인하세요.");
        await RefreshRoomPermissionsAsync(ct);
        if (!CanCloseRooms) throw new SynologyApiException(404, "전체 채팅방을 닫을 권한이 없습니다.");
        // Official close v4 is not a guarantee of physical erasure; NAS retention policy still applies.
        await SendAsync("SYNO.Chat.Channel", "close", 4, new() { ["channel_id"] = channelId }, true, ct);
        conversations.Remove(channelId); visibility.Remove(channelId);
    }

    private ChatConversation RequireNamedRoom(long id)
    {
        if (!conversations.TryGetValue(id, out var room) || !room.IsNamedRoom || room.IsEncrypted)
            throw new SynologyApiException(400, "참여 중인 일반 공개·비공개 채팅방에서만 가능합니다. 기본방·나에게 보내기·일대일·암호화 대화는 제외됩니다.");
        return room;
    }

    private long[] ValidateInvitees(IReadOnlyList<long> ids)
    {
        if (ids is null || ids.Count > 200) throw new SynologyApiException(400, "한 번에 최대 200명의 직원을 선택하세요.");
        var result = ids.Distinct().Where(id => id != CurrentUser!.Id).ToArray();
        if (result.Any(id => id <= 0 || !inviteableUsers.Contains(id)))
            throw new SynologyApiException(400, "활성 직원 목록에서 초대할 사용자를 선택하세요.");
        return result;
    }
}
