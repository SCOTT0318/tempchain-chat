using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TempChain.Chat.Models;

namespace TempChain.Chat.Services;

public sealed class SynologyApiException : Exception
{
    public int Code { get; }
    public SynologyApiException(int code, string safeMessage) : base($"{safeMessage} (코드 {code})") => Code = code;
}

/// <summary>Normal employee session API; credentials and session tokens stay in memory.</summary>
public sealed partial class SynologyChatClient : IDisposable
{
    private const int MaximumResponseBytes = 8 * 1024 * 1024;
    private readonly HttpClient http;
    private readonly Uri origin;
    private readonly Dictionary<string, ApiDescription> apis = new(StringComparer.Ordinal);
    private readonly Dictionary<long, ChatUser> users = new();
    private readonly HashSet<long> inviteableUsers = new();
    private readonly Dictionary<long, ChatConversation> conversations = new();
    private readonly Dictionary<long, ConversationVisibility> visibility = new();
    private string? sid;
    private string? synoToken;
    private string? defaultNotificationPreference;
    private bool disposed;
    public ChatUser? CurrentUser { get; private set; }
    public IReadOnlyList<ChatConversation> NotificationConversations => conversations.Values.ToArray();

    public SynologyChatClient(Uri serverUri, HttpMessageHandler? handler = null)
    {
        if (!serverUri.IsAbsoluteUri || !NavigationPolicy.TryGetServerUri(serverUri.AbsoluteUri, out _))
            throw new ArgumentException("올바른 HTTPS NAS 서버 주소가 필요합니다.", nameof(serverUri));
        origin = new Uri(serverUri.GetLeftPart(UriPartial.Authority) + "/");
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task LoginAsync(string username, string password, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new SynologyApiException(400, "사용자명과 비밀번호를 입력하세요.");
        ClearSession();
        apis.Clear();
        var discovery = await SendAsync("SYNO.API.Info", "query", 1,
            new Dictionary<string, object?> { ["query"] = "all" }, false, ct);
        if (discovery.ValueKind != JsonValueKind.Object)
            throw new SynologyApiException(0, "NAS API 목록을 확인하지 못했습니다.");
        foreach (var entry in discovery.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            var path = ReadString(entry.Value, "path");
            if (string.IsNullOrEmpty(path)) continue;
            // Metadata may never redirect account credentials to another host/path.
            if (!IsSafeApiPath(path))
            {
                if (entry.Name == "SYNO.API.Auth" || entry.Name.StartsWith("SYNO.Chat.", StringComparison.Ordinal))
                    throw new SynologyApiException(0, "NAS API 경로를 안전하게 확인하지 못했습니다.");
                continue;
            }
            apis[entry.Name] = new ApiDescription(path,
                (int)ReadLong(entry.Value, "minVersion", 1), (int)ReadLong(entry.Value, "maxVersion", 1),
                string.Equals(ReadString(entry.Value, "requestFormat"), "JSON", StringComparison.OrdinalIgnoreCase));
        }
        var fields = new Dictionary<string, object?>
        {
            ["account"] = username, ["passwd"] = password, ["session"] = "Chat", ["format"] = "sid",
            ["enable_syno_token"] = "yes"
        };
        try
        {
            var authentication = await SendAsync("SYNO.API.Auth", "login", 6, fields, false, ct);
            sid = ReadString(authentication, "sid");
            synoToken = ReadString(authentication, "synotoken", "syno_token", "SynoToken");
            if (string.IsNullOrEmpty(sid)) throw new SynologyApiException(0, "로그인 세션을 확인하지 못했습니다.");
            var handshake = await SendAsync("SYNO.Chat.User", "login", 2, new(), true, ct);
            if (handshake.ValueKind != JsonValueKind.Object || !handshake.TryGetProperty("user", out var signedInUser) ||
                ReadLong(signedInUser, "user_id", "id") <= 0)
                throw new SynologyApiException(0, "로그인한 Chat 사용자 정보를 확인하지 못했습니다.");
            var currentId = ReadLong(signedInUser, "user_id", "id");
            var signedInName = ReadString(signedInUser, "username", "user_name", "name");
            var signedInDisplay = ReadUserDisplayName(signedInUser);
            var employees = await GetUsersAsync(ct);
            CurrentUser = employees.FirstOrDefault(user => user.Id == currentId);
            if (CurrentUser is null && !string.IsNullOrWhiteSpace(signedInName))
                CurrentUser = new ChatUser(currentId, signedInName,
                    string.IsNullOrWhiteSpace(signedInDisplay) ? signedInName : signedInDisplay,
                    Email: ReadProfileString(signedInUser, "email"));
            if (CurrentUser is null) throw new SynologyApiException(0, "로그인한 Chat 사용자 정보를 확인하지 못했습니다.");
        }
        catch
        {
            ClearSession();
            throw;
        }
    }

    public async Task<IReadOnlyList<ChatUser>> GetUsersAsync(CancellationToken ct = default)
    {
        EnsureSession();
        var data = await SendAsync("SYNO.Chat.User", "list", 2, new(), true, ct);
        var result = new List<ChatUser>();
        users.Clear(); inviteableUsers.Clear();
        foreach (var item in ReadArray(data, "users"))
        {
            var id = ReadLong(item, "user_id", "id");
            var name = ReadString(item, "username", "user_name", "name");
            if (id <= 0 || string.IsNullOrWhiteSpace(name)) continue;
            var display = ReadUserDisplayName(item);
            var active = ReadBool(item, true, "is_active", "active", "is_enabled") &&
                !ReadBool(item, false, "is_disabled") && !ReadBool(item, false, "deleted");
            var user = new ChatUser(id, name, string.IsNullOrWhiteSpace(display) ? name : display, active,
                ReadProfileString(item, "email"));
            users[id] = user;
            var type = ReadString(item, "type");
            var humanType = ReadString(item, "human_type");
            if (active && (type.Length == 0 || type == "human") && (humanType.Length == 0 || humanType == "dsm"))
            {
                result.Add(user);
                inviteableUsers.Add(id);
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(CancellationToken ct = default)
    {
        EnsureSession();
        var data = await SendAsync("SYNO.Chat.Channel", "list", 5, new(), true, ct);
        var channelRecords = ReadArray(data, "channels");
        if (defaultNotificationPreference is null && channelRecords.Any(channel =>
            channel.ValueKind == JsonValueKind.Object && channel.TryGetProperty("preferences", out var preferences) &&
            ReadString(preferences, "notification_desktop") == "by_admin_console") &&
            apis.TryGetValue("SYNO.Chat.Admin.Setting", out var settingApi) && settingApi.Maximum >= 5)
        {
            // The official employee client reads this shared notification policy;
            // this endpoint does not change any administrator or user settings.
            var policy = await SendAsync("SYNO.Chat.Admin.Setting", "get", 5, new(), true, ct);
            defaultNotificationPreference = ReadString(policy, "notify_type");
        }
        var result = new List<ChatConversation>();
        var allConversations = new Dictionary<long, ChatConversation>();
        var allVisibility = new Dictionary<long, ConversationVisibility>();
        foreach (var item in channelRecords)
        {
            var id = ReadLong(item, "channel_id", "id");
            if (id <= 0 || !ReadBool(item, true, "is_joined") || ReadBool(item, false, "is_closed")) continue;
            var members = ReadMembers(item);
            var name = ReadString(item, "name", "channel_name", "display_name");
            var type = ReadString(item, "type", "channel_type");
            var directType = type.Length == 0 || type.Contains("anonymous", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("direct", StringComparison.OrdinalIgnoreCase);
            var self = string.Equals(type, "synobot", StringComparison.OrdinalIgnoreCase) ||
                (directType && CurrentUser is not null && members.Count == 1 && members[0] == CurrentUser.Id);
            if (self) name = "나에게 보내기";
            else if (string.IsNullOrWhiteSpace(name) && id == 1) name = "일반";
            else if (string.IsNullOrWhiteSpace(name) && id == 2) name = "자유대화";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = self ? "나에게 보내기" : string.Join(", ", members.Where(member => member != CurrentUser?.Id)
                    .Select(member => users.TryGetValue(member, out var user) ? user.DisplayName : $"사용자 {member}"));
                if (string.IsNullOrWhiteSpace(name)) name = $"대화 {id}";
            }
            var kind = self || type.Contains("anonymous", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("direct", StringComparison.OrdinalIgnoreCase) ? "direct" : "channel";
            var conversation = new ChatConversation(id, name, kind, members,
                ReadBool(item, false, "encrypted", "is_encrypted", "isEncrypted"), self, type,
                ReadLong(item, "last_post_at"), ReadLong(item, "unread"));
            var hidden = IsHiddenFromList(item, type, self, id, defaultNotificationPreference);
            allConversations[id] = conversation;
            allVisibility[id] = new ConversationVisibility(ReadLong(item, "last_hide_at"),
                ReadLong(item, "last_view_at"), ReadLong(item, "last_post_at"), hidden);
            if (!hidden) result.Add(conversation);
        }
        conversations.Clear();
        visibility.Clear();
        foreach (var pair in allConversations) conversations[pair.Key] = pair.Value;
        foreach (var pair in allVisibility) visibility[pair.Key] = pair.Value;
        return result;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(long channelId, CancellationToken ct = default)
    {
        EnsureSession();
        CheckChannel(channelId);
        var data = await SendAsync("SYNO.Chat.Post", "list", 5,
            new() { ["channel_id"] = channelId, ["prev_count"] = 60, ["next_count"] = -1 }, true, ct);
        var result = new List<ChatMessage>();
        foreach (var item in ReadArray(data, "posts", "messages"))
        {
            var id = ReadLong(item, "post_id", "id");
            if (id <= 0) continue;
            var text = ReadString(item, "message", "text");
            if (string.Equals(ReadString(item, "type"), "system", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(text)) continue;
            var userId = ReadLong(item, "creator_id", "user_id", "sender_id");
            var userName = users.TryGetValue(userId, out var user) ? user.DisplayName :
                ReadString(item, "username", "user_name", "nickname");
            if (string.IsNullOrWhiteSpace(userName)) userName = userId > 0 ? $"사용자 {userId}" : "시스템";
            var sentAt = ReadTimestamp(item);
            var hasAttachments = ReadArray(item, "attachments", "files").Any();
            result.Add(new ChatMessage(id, channelId, userId, userName,
                text, sentAt, CurrentUser?.Id == userId, hasAttachments));
        }
        return result.OrderBy(message => message.SentAt).ThenBy(message => message.Id).ToArray();
    }

    public async Task SendMessageAsync(long channelId, string text, CancellationToken ct = default)
    {
        EnsureSession();
        CheckChannel(channelId);
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4000)
            throw new SynologyApiException(400, "메시지는 1~4000자로 입력하세요.");
        await SendAsync("SYNO.Chat.Post", "create", 5,
            new() { ["channel_id"] = channelId, ["type"] = "normal", ["message"] = text,
                ["is_thread"] = false, ["thread_id"] = 0 }, true, ct);
    }

    public async Task<ChatConversation> OpenDirectConversationAsync(long userId, CancellationToken ct = default)
    {
        EnsureSession();
        if (userId <= 0) throw new SynologyApiException(400, "대화할 사용자를 선택하세요.");
        if (userId == CurrentUser!.Id)
        {
            var selfConversation = conversations.Values.FirstOrDefault(conversation => conversation.IsSelf);
            if (selfConversation is null)
                selfConversation = (await GetConversationsAsync(ct)).FirstOrDefault(conversation => conversation.IsSelf);
            return selfConversation ?? throw new SynologyApiException(0, "나에게 보내기 대화를 찾지 못했습니다. 대화 목록을 새로 고쳐 주세요.");
        }
        var participantIds = new[] { userId, CurrentUser!.Id }.Distinct().ToArray();
        var cachedConversation = conversations.Values.FirstOrDefault(conversation =>
            !conversation.IsSelf && !conversation.IsEncrypted && conversation.Kind == "direct" &&
            conversation.MemberIds.Distinct().OrderBy(id => id).SequenceEqual(participantIds.OrderBy(id => id)));
        if (cachedConversation is not null)
        {
            if (visibility.TryGetValue(cachedConversation.Id, out var cachedVisibility) &&
                (cachedVisibility.IsHidden || cachedVisibility.LastHideAt > 0))
                await ShowConversationAsync(cachedConversation.Id, ct);
            return cachedConversation;
        }
        var data = await SendAsync("SYNO.Chat.Channel.Anonymous", "initiate", 2,
            new() { ["user_ids"] = participantIds, ["encrypted"] = false,
                ["channel_key_encs"] = Array.Empty<object>() }, true, ct);
        var id = ReadLong(data, "channel_id", "id");
        if (id <= 0 && data.ValueKind == JsonValueKind.Object && data.TryGetProperty("channel", out var channel))
        {
            data = channel;
            id = ReadLong(channel, "channel_id", "id");
        }
        if (id <= 0) throw new SynologyApiException(0, "대화방 정보를 확인하지 못했습니다.");
        if (ReadLong(data, "last_hide_at") > 0 ||
            (visibility.TryGetValue(id, out var knownVisibility) && knownVisibility.IsHidden))
            await ShowConversationAsync(id, ct);
        if (conversations.TryGetValue(id, out var existing)) return existing;
        var self = CurrentUser?.Id == userId;
        var name = self ? "나에게 보내기" : users.TryGetValue(userId, out var user) ? user.DisplayName : $"사용자 {userId}";
        var members = self ? new[] { userId } : new[] { CurrentUser!.Id, userId };
        var conversation = new ChatConversation(id, name, "direct", members,
            ReadBool(data, false, "encrypted", "is_encrypted", "isEncrypted"), self);
        conversations[id] = conversation;
        return conversation;
    }

    public async Task HideConversationAsync(long channelId, CancellationToken ct = default)
    {
        EnsureSession();
        if (channelId <= 0) throw new SynologyApiException(400, "숨길 대화를 선택하세요.");
        if (!conversations.TryGetValue(channelId, out var conversation))
        {
            await GetConversationsAsync(ct);
            conversations.TryGetValue(channelId, out conversation);
        }
        if (conversation is null) throw new SynologyApiException(403, "숨길 대화를 찾지 못했습니다.");
        if (conversation.IsSelf) throw new SynologyApiException(400, "나에게 보내기는 대화 목록에서 숨길 수 없습니다.");
        var data = await SendAsync("SYNO.Chat.Channel", "hide", 2,
            new() { ["channel_id"] = channelId }, true, ct);
        var previous = visibility.TryGetValue(channelId, out var state) ? state : new ConversationVisibility(0, 0, 0, false);
        visibility[channelId] = previous with { LastHideAt = ReadLong(data, "last_hide_at"), IsHidden = true };
    }

    public async Task MarkConversationReadAsync(long channelId, long lastViewAtMs, CancellationToken ct = default)
    {
        EnsureSession();
        CheckChannel(channelId);
        if (lastViewAtMs <= 0) throw new SynologyApiException(400, "실제로 표시한 메시지 시각이 필요합니다.");
        await SendAsync("SYNO.Chat.Channel", "view", 2,
            new() { ["channel_id"] = channelId, ["last_view_at"] = lastViewAtMs }, true, ct);
        if (visibility.TryGetValue(channelId, out var previous))
            visibility[channelId] = previous with
            {
                LastViewAt = Math.Max(previous.LastViewAt, lastViewAtMs),
                IsHidden = previous.IsHidden && previous.LastHideAt >= lastViewAtMs
            };
    }

    private async Task ShowConversationAsync(long channelId, CancellationToken ct)
    {
        await SendAsync("SYNO.Chat.Channel", "show", 2, new() { ["channel_id"] = channelId }, true, ct);
        if (visibility.TryGetValue(channelId, out var previous))
            visibility[channelId] = previous with { LastHideAt = 0, IsHidden = false };
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        try
        {
            if (!string.IsNullOrEmpty(sid))
                await SendAsync("SYNO.API.Auth", "logout", 6, new() { ["session"] = "Chat" }, true, ct);
        }
        finally { ClearSession(); }
    }

    private async Task<JsonElement> SendAsync(string api, string method, int requestedVersion,
        Dictionary<string, object?> parameters, bool authenticated, CancellationToken ct)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (authenticated && string.IsNullOrEmpty(sid))
            throw new SynologyApiException(119, "먼저 로그인해 주세요.");
        var path = "query.cgi";
        var version = requestedVersion;
        var jsonParameters = false;
        if (api != "SYNO.API.Info")
        {
            if (!apis.TryGetValue(api, out var description) ||
                requestedVersion < description.Minimum || requestedVersion > description.Maximum)
                throw new SynologyApiException(104, "NAS에서 필요한 Chat API 버전을 제공하지 않습니다.");
            path = description.Path;
            jsonParameters = description.JsonParameters && api != "SYNO.API.Auth";
        }
        var fields = new Dictionary<string, string>
        {
            ["api"] = api, ["method"] = method, ["version"] = version.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var pair in parameters)
            fields[pair.Key] = jsonParameters ? JsonSerializer.Serialize(pair.Value) :
                Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? "";
        if (authenticated)
        {
            fields["_sid"] = sid!;
            if (!string.IsNullOrEmpty(synoToken)) fields["SynoToken"] = synoToken;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "webapi/" + path))
        { Content = new FormUrlEncodedContent(fields) };
        if (authenticated && !string.IsNullOrEmpty(synoToken)) request.Headers.TryAddWithoutValidation("X-SYNO-TOKEN", synoToken);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new SynologyApiException((int)response.StatusCode, "NAS 서버가 요청을 처리하지 못했습니다.");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                throw new SynologyApiException(0, "NAS 응답이 허용 크기를 초과했습니다.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[16384];
            int count;
            while ((count = await stream.ReadAsync(chunk.AsMemory(), ct)) != 0)
            {
                if (buffer.Length + count > MaximumResponseBytes)
                    throw new SynologyApiException(0, "NAS 응답이 허용 크기를 초과했습니다.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var envelope = document.RootElement;
            if (envelope.ValueKind != JsonValueKind.Object || !envelope.TryGetProperty("success", out var success) ||
                success.ValueKind != JsonValueKind.True)
            {
                var code = envelope.ValueKind == JsonValueKind.Object && envelope.TryGetProperty("error", out var error) ?
                    (int)ReadLong(error, "code", 0) : 0;
                var message = api == "SYNO.API.Auth" && method == "login" && code is 403 or 404 ?
                    "이 계정으로 로그인할 수 없습니다. 관리자에게 계정 설정을 문의해 주세요." :
                    api.StartsWith("SYNO.Chat.", StringComparison.Ordinal) ? code switch
                    {
                        401 => "해당 대화에 접근할 수 없습니다.",
                        403 => "대화를 찾지 못했습니다.",
                        404 => "이 작업을 수행할 권한이 없습니다.",
                        _ => "NAS 요청을 완료하지 못했습니다."
                    } : "NAS 요청을 완료하지 못했습니다.";
                throw new SynologyApiException(code, message);
            }
            if (!envelope.TryGetProperty("data", out var data))
                return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
            return data.Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new SynologyApiException(0, "NAS 연결 시간이 초과했습니다."); }
        catch (HttpRequestException)
        { throw new SynologyApiException(0, "NAS에 연결하지 못했습니다. 네트워크와 인증서를 확인하세요."); }
        catch (JsonException)
        { throw new SynologyApiException(0, "NAS에서 올바른 API 응답을 받지 못했습니다."); }
        catch (IOException)
        { throw new SynologyApiException(0, "NAS 응답을 읽지 못했습니다."); }
    }

    private static bool IsSafeApiPath(string path)
    {
        if (path.Length > 250 || path.StartsWith('/') || path.Contains("..") || path.Contains('\\') ||
            path.Contains('?') || path.Contains('#') || path.Contains(':') || path.Contains('%')) return false;
        return path.Split('/').All(segment => segment.Length > 0 && segment.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'));
    }

    private static JsonElement[] ReadArray(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object) return Array.Empty<JsonElement>();
        foreach (var name in names)
            if (source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().ToArray();
        return Array.Empty<JsonElement>();
    }

    private static string ReadUserDisplayName(JsonElement source)
    {
        var description = ReadProfileString(source, "description");
        var parts = description.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
            return string.Join(" ", parts);
        return ReadProfileString(source, "nickname", "display_name", "displayName", "full_name", "real_name");
    }

    private static string ReadProfileString(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object) return "";
        foreach (var name in names)
            if (source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!.Trim();
        return source.TryGetProperty("user_props", out var properties)
            ? ReadProfileString(properties, names) : "";
    }

    private static string ReadString(JsonElement source, params string[] names)
    {
        if (source.ValueKind != JsonValueKind.Object) return "";
        foreach (var name in names)
            if (source.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
                if (value.ValueKind == JsonValueKind.Number) return value.GetRawText();
            }
        return "";
    }

    private static long ReadLong(JsonElement source, params string[] names)
    {
        foreach (var name in names)
            if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out number)) return number;
            }
        return 0;
    }

    private static long ReadLong(JsonElement source, string name, long fallback) =>
        source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out _) ? ReadLong(source, name) : fallback;

    private static bool ReadBool(JsonElement source, bool fallback, params string[] names)
    {
        foreach (var name in names)
            if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.False) return false;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number != 0;
                if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var boolean)) return boolean;
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var numeric)) return numeric != 0;
            }
        return fallback;
    }

    private static IReadOnlyList<long> ReadMembers(JsonElement channel)
    {
        var result = new List<long>();
        foreach (var item in ReadArray(channel, "members", "member_ids", "user_ids", "users"))
        {
            long id = 0;
            if (item.ValueKind == JsonValueKind.Number) item.TryGetInt64(out id);
            else if (item.ValueKind == JsonValueKind.String) long.TryParse(item.GetString(), out id);
            else if (item.ValueKind == JsonValueKind.Object) id = ReadLong(item, "user_id", "id");
            if (id > 0 && !result.Contains(id)) result.Add(id);
        }
        return result;
    }

    private static bool IsHiddenFromList(JsonElement channel, string type, bool isSelf, long channelId, string? defaultPreference)
    {
        if (isSelf) return false;
        if ((channelId == 1 || channelId == 2) && ReadLong(channel, "hide_global_at") > 0) return true;
        var lastHide = ReadLong(channel, "last_hide_at");
        if (lastHide <= 0 || lastHide < ReadLong(channel, "last_view_at")) return false;
        var comments = ReadLong(channel, "unread_comment") > 0;
        if (channel.TryGetProperty("unread_thread", out var threads) && threads.ValueKind == JsonValueKind.Object)
            comments |= threads.EnumerateObject().Any(thread =>
                (thread.Value.ValueKind == JsonValueKind.Number && thread.Value.TryGetInt64(out var count) && count > 0) ||
                (thread.Value.ValueKind == JsonValueKind.String && long.TryParse(thread.Value.GetString(), out count) && count > 0));
        var mentions = ReadLong(channel, "unread_mention") > 0;
        var unread = ReadLong(channel, "unread") > 0;
        if (string.Equals(type, "anonymous", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "chatbot", StringComparison.OrdinalIgnoreCase))
            return !(unread || mentions || comments || HasNewPostWithoutUnreadFields(channel, lastHide));
        if (mentions || comments) return false;
        var preference = "mention_subscribe";
        if (channel.TryGetProperty("preferences", out var preferences) && preferences.ValueKind == JsonValueKind.Object)
        {
            var configured = ReadString(preferences, "notification_desktop");
            if (configured.Length > 0) preference = configured;
        }
        if (preference == "by_admin_console")
            preference = string.IsNullOrEmpty(defaultPreference) ? "mention_subscribe" : defaultPreference;
        return preference == "all" ? !(unread || HasNewPostWithoutUnreadFields(channel, lastHide)) :
            !HasNewPostWithoutUnreadFields(channel, lastHide);
    }

    private static bool HasNewPostWithoutUnreadFields(JsonElement channel, long lastHide) =>
        !channel.TryGetProperty("unread", out _) && !channel.TryGetProperty("unread_mention", out _) &&
        !channel.TryGetProperty("unread_thread", out _) && !channel.TryGetProperty("unread_comment", out _) &&
        ReadLong(channel, "last_post_at") > lastHide;

    private static DateTimeOffset ReadTimestamp(JsonElement message)
    {
        var timestamp = ReadLong(message, "create_at", "created_at", "timestamp", "time");
        try { return timestamp > 100_000_000_000L ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp) : DateTimeOffset.FromUnixTimeSeconds(timestamp); }
        catch (ArgumentOutOfRangeException) { return DateTimeOffset.UnixEpoch; }
    }

    private void CheckChannel(long id)
    {
        if (id <= 0) throw new SynologyApiException(400, "대화방을 선택하세요.");
        if (conversations.TryGetValue(id, out var conversation) && conversation.IsEncrypted)
            throw new SynologyApiException(0, "암호화된 대화는 현재 지원하지 않습니다.");
    }

    private void EnsureSession()
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(sid)) throw new SynologyApiException(119, "먼저 로그인해 주세요.");
    }

    private void ClearSession()
    {
        sid = null; synoToken = null; defaultNotificationPreference = null;
        CurrentUser = null; users.Clear(); inviteableUsers.Clear(); conversations.Clear(); visibility.Clear(); roomPermissions.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; ClearSession(); http.Dispose();
    }

    private sealed record ApiDescription(string Path, int Minimum, int Maximum, bool JsonParameters);
    private sealed record ConversationVisibility(long LastHideAt, long LastViewAt, long LastPostAt, bool IsHidden);
}
