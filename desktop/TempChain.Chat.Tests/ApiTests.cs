using System.Net;
using System.Text.Json;
using TempChain.Chat.Services;
using static Program;

internal static class ApiTests
{
    private const long OtherId = 9007199254740993;
    private const long ChannelId = 9007199254740995;
    private const string Password = "pass+&한글-test-secret";
    private const string Sid = "session-test-secret";
    private const string Token = "syno-test-secret";
    private static readonly Uri Server = new("https://nas.example.test:5001/");

    internal static async Task RunAllAsync()
    {
        foreach (var unsafeUrl in new[] { "http://nas.example.test/", "https://admin:pass@nas.example.test/", "file:///C:/Windows/win.ini" })
            Run("Reject unsafe API server before requests", () => Throws<ArgumentException>(() => new SynologyChatClient(new Uri(unsafeUrl))));

        await RunAsync("Discovery, Auth, Chat handshake and list preserve body credentials and aliases", async () =>
        {
            var handler = LoginHandler();
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("박범준+ops&", Password);
            Equal(7L, client.CurrentUser!.Id);
            Equal("박범준", client.CurrentUser.DisplayName);
            Equal("박범준+ops&", handler.Requests[1].Fields["account"]);
            Equal(Password, handler.Requests[1].Fields["passwd"]);
            Check(!handler.Requests[1].Fields.ContainsKey("otp_code"));
            Equal("Chat", handler.Requests[1].Fields["session"]);
            foreach (var request in handler.Requests)
            {
                Equal(HttpMethod.Post, request.Method);
                Equal("", request.Uri.Query);
                Equal(Server.Host, request.Uri.Host);
                foreach (var secret in new[] { Password, Sid, Token, "123456" }) Check(!request.Uri.AbsoluteUri.Contains(secret));
            }
            Equal(Sid, handler.Requests[2].Fields["_sid"]);
            handler.AssertComplete();
        });
        await RunAsync("Users keep 64-bit IDs and exclude inactive, deleted and bot contacts", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.User", "list", Users());
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("alias-login", Password);
            var users = await client.GetUsersAsync();
            Equal(2, users.Count);
            Check(users.Any(user => user.Id == OtherId && user.DisplayName == "홍길동"));
            Check(users.All(user => user.IsActive));
            handler.AssertComplete();
        });
        await RunAsync("User profiles preserve names and nested emails without inventing missing values", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.User", "list", new { success = true, data = new { users = new object[] {
                new { user_id = 7, username = "p.park", nickname = "박범준", user_props = new { email = "park@example.test" } },
                new { user_id = OtherId, username = "hong", nickname = " ", display_name = "홍길동", email = "", user_props = new { email = "hong@example.test" } },
                new { user_id = 21, username = "kim", nickname = "김직원", email = "kim@example.test" },
                new { user_id = 22, username = "unconfigured" }
            } } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            var users = await client.GetUsersAsync();
            Equal("박범준", users.Single(user => user.Id == 7).DisplayName);
            Equal("park@example.test", users.Single(user => user.Id == 7).Email);
            Equal("홍길동", users.Single(user => user.Id == OtherId).DisplayName);
            Equal("hong@example.test", users.Single(user => user.Id == OtherId).Email);
            Equal("kim@example.test", users.Single(user => user.Id == 21).Email);
            Equal("unconfigured", users.Single(user => user.Id == 22).DisplayName);
            Equal("", users.Single(user => user.Id == 22).Email);
            handler.AssertComplete();
        });
        await RunAsync("Messages normalize long IDs, timestamps, sender names and attachments", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", Channels())
                .Expect("SYNO.Chat.Post", "list", new { success = true, data = new { posts = new object[] {
                    new { post_id = 1, creator_id = 7, type = "system", message = "", create_at = 1792700000000L },
                    new { post_id = "9007199254740999", creator_id = 7, message = "두 번째", create_at = 1792800001000L },
                    new { post_id = 9007199254740997L, creator_id = OtherId, message = "첫 번째 & <문자>", create_at = 1792800000000L, files = new[] { new { file_id = 1 } } }
                } } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            var channels = await client.GetConversationsAsync();
            Equal("일반", channels.Single(value => value.Id == 1).Name);
            Equal("자유대화", channels.Single(value => value.Id == 2).Name);
            Equal("홍길동", channels.Single(value => value.Id == ChannelId).Name);
            Check(channels.Single(value => value.Id == 19).IsSelf);
            var messages = await client.GetMessagesAsync(ChannelId);
            Equal(2, messages.Count);
            Equal(9007199254740997L, messages[0].Id);
            Equal("홍길동", messages[0].UserName);
            Equal("첫 번째 & <문자>", messages[0].Text);
            Check(messages[0].HasAttachments && messages[1].IsOwn);
            Equal(DateTimeOffset.FromUnixTimeMilliseconds(1792800000000L), messages[0].SentAt);
            Equal("-1", handler.Requests[^1].Fields["next_count"]);
            handler.AssertComplete();
        });
        await RunAsync("Unicode sends once using official normal post schema", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Post", "create", new { success = true, data = new { post_id = 31 } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            const string text = "안녕하세요 + & = \"확인\"\n둘째 줄 ✅";
            await client.SendMessageAsync(ChannelId, text);
            var fields = handler.Requests[^1].Fields;
            Equal(text, JsonSerializer.Deserialize<string>(fields["message"]));
            Equal("normal", JsonSerializer.Deserialize<string>(fields["type"]));
            Equal(ChannelId, JsonSerializer.Deserialize<long>(fields["channel_id"]));
            Equal("false", fields["is_thread"]);
            Equal("0", fields["thread_id"]);
            Equal(5, handler.Requests.Count);
            handler.AssertComplete();
        });
        await RunAsync("Direct conversation includes both users and encryption fields", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel.Anonymous", "initiate", new { success = true, data = new { channel_id = ChannelId } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            Equal(ChannelId, (await client.OpenDirectConversationAsync(OtherId)).Id);
            var fields = handler.Requests[^1].Fields;
            Check(JsonSerializer.Deserialize<long[]>(fields["user_ids"])!.SequenceEqual(new[] { OtherId, 7L }));
            Equal("false", fields["encrypted"]);
            Equal("[]", fields["channel_key_encs"]);
            handler.AssertComplete();
        });
        await RunAsync("Encrypted channels and invalid text never create posts", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", Channels());
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.GetConversationsAsync();
            foreach (var invalid in new[] { "", " ", new string('x', 4001) })
                await ThrowsAsync<SynologyApiException>(() => client.SendMessageAsync(ChannelId, invalid));
            await ThrowsAsync<SynologyApiException>(() => client.SendMessageAsync(20, "test"));
            Equal(5, handler.Requests.Count);
            handler.AssertComplete();
        });
        await RunAsync("Direct response preserves returned encryption", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel.Anonymous", "initiate",
                new { success = true, data = new { channel_id = 20, encrypted = true } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            var channel = await client.OpenDirectConversationAsync(OtherId);
            Check(!channel.IsSelf && channel.IsEncrypted);
            await ThrowsAsync<SynologyApiException>(() => client.SendMessageAsync(20, "blocked"));
            handler.AssertComplete();
        });
        await RunAsync("Self conversation discovers and reuses synobot without anonymous initiate", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", new { success = true, data = new { channels = new[] {
                new { channel_id = 3, name = "", type = "synobot", members = Array.Empty<long>() }
            } } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            var channel = await client.OpenDirectConversationAsync(7);
            Equal(3L, channel.Id);
            Equal("나에게 보내기", channel.Name);
            Check(channel.IsSelf);
            Equal(channel, await client.OpenDirectConversationAsync(7));
            Equal(5, handler.Requests.Count);
            Check(handler.Requests.All(request => request.Fields["api"] != "SYNO.Chat.Channel.Anonymous"));
            handler.AssertComplete();
        });
        await RunAsync("Missing self conversation reports error without creating invalid one-user DM", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", new { success = true, data = new { channels = Array.Empty<object>() } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.OpenDirectConversationAsync(7)));
            Equal(5, handler.Requests.Count);
            handler.AssertComplete();
        });
        foreach (var path in new[] { "https://evil.example.test/leak", "../auth.cgi", "/webapi/auth.cgi", "entry.cgi?token=bad", "%2e%2e/auth.cgi" })
            await RunAsync("Discovered unsafe paths never receive credentials", async () =>
            {
                var handler = new ScriptedHttpHandler().Expect("SYNO.API.Info", "query", Discovery(path));
                using var client = new SynologyChatClient(Server, handler);
                AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.LoginAsync("park", Password)));
                Equal(1, handler.Requests.Count);
                handler.AssertComplete();
            });
        await RunAsync("Auth errors expose code and safe message without NAS details", async () =>
        {
            var handler = new ScriptedHttpHandler().Expect("SYNO.API.Info", "query", Discovery())
                .Expect("SYNO.API.Auth", "login", new { success = false, error = new { code = 403, detail = Password + Sid + Token } });
            using var client = new SynologyChatClient(Server, handler);
            var error = await ThrowsAsync<SynologyApiException>(() => client.LoginAsync("park", Password));
            Equal(403, error.Code);
            AssertSafe(error);
            Check(client.CurrentUser is null);
            await ThrowsAsync<SynologyApiException>(() => client.GetUsersAsync());
            handler.AssertComplete();
        });
        await RunAsync("Invalid Chat handshake fails safely and drops session", async () =>
        {
            var handler = new ScriptedHttpHandler().Expect("SYNO.API.Info", "query", Discovery())
                .Expect("SYNO.API.Auth", "login", new { success = true, data = new { sid = Sid, synotoken = Token } })
                .ExpectRaw("SYNO.Chat.User", "login", "{\"success\":true,\"data\":null}");
            using var client = new SynologyChatClient(Server, handler);
            AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.LoginAsync("park", Password)));
            Check(client.CurrentUser is null);
            await ThrowsAsync<SynologyApiException>(() => client.GetUsersAsync());
            handler.AssertComplete();
        });
        foreach (var raw in new[] { "<html>" + Password + Sid + "</html>", "{\"success\":\"true\"}", new string('x', 8 * 1024 * 1024 + 1) })
            await RunAsync("Malformed or oversized NAS response is safely rejected", async () =>
            {
                var handler = new ScriptedHttpHandler().ExpectRaw("SYNO.API.Info", "query", raw);
                using var client = new SynologyChatClient(Server, handler);
                AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.LoginAsync("park", Password)));
                handler.AssertComplete();
            });
        await RunAsync("Redirect response to post mutation fails without retry", async () =>
        {
            var handler = LoginHandler().ExpectRaw("SYNO.Chat.Post", "create", Password + Sid, HttpStatusCode.Redirect);
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.SendMessageAsync(ChannelId, "한 번만")));
            Equal(5, handler.Requests.Count);
            handler.AssertComplete();
        });
        await RunAsync("Logout uses own session and clears authenticated state", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.API.Auth", "logout", new { success = true });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.LogoutAsync();
            Equal(Sid, handler.Requests[^1].Fields["_sid"]);
            Equal("Chat", handler.Requests[^1].Fields["session"]);
            Check(client.CurrentUser is null);
            await ThrowsAsync<SynologyApiException>(() => client.GetUsersAsync());
            handler.AssertComplete();
        });
        await RunAsync("Failed logout still clears local authenticated state", async () =>
        {
            var handler = LoginHandler().ExpectRaw("SYNO.API.Auth", "logout", Password + Sid, HttpStatusCode.InternalServerError);
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            AssertSafe(await ThrowsAsync<SynologyApiException>(() => client.LogoutAsync()));
            Check(client.CurrentUser is null);
            await ThrowsAsync<SynologyApiException>(() => client.GetUsersAsync());
            handler.AssertComplete();
        });
        await RunAsync("Hide only changes current user's list and leaves message reads available", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord()))
                .Expect("SYNO.Chat.Channel", "hide", new { success = true, data = new { channel_id = ChannelId, last_hide_at = 2000 } })
                .Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(2000, 1000, 1500)))
                .Expect("SYNO.Chat.Post", "list", new { success = true, data = new { posts = new[] {
                    new { post_id = 31, creator_id = OtherId, message = "기존 메시지", create_at = 1000 }
                } } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.GetConversationsAsync();
            await client.HideConversationAsync(ChannelId);
            var hide = handler.Requests[^1];
            Equal("2", hide.Fields["version"]);
            Equal(ChannelId, JsonSerializer.Deserialize<long>(hide.Fields["channel_id"]));
            Equal(Sid, hide.Fields["_sid"]);
            Check(!(await client.GetConversationsAsync()).Any(channel => channel.Id == ChannelId));
            Equal("기존 메시지", (await client.GetMessagesAsync(ChannelId)).Single().Text);
            Check(handler.Requests.All(request => request.Fields["method"] is not "delete" and not "close" and not "hide_global" and not "disjoin"));
            handler.AssertComplete();
        });
        await RunAsync("Hidden list follows NAS unread, mention, view and preference rules", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(
                new { channel_id = 30, type = "anonymous", last_hide_at = 2000, last_view_at = 1000, last_post_at = 1500, unread = 0 },
                new { channel_id = 31, type = "anonymous", last_hide_at = 2000, last_view_at = 1000, last_post_at = 2500, unread = 1 },
                new { channel_id = 32, type = "anonymous", last_hide_at = 2000, last_view_at = 1000, unread = 0, unread_thread = new Dictionary<string, int> { ["post-1"] = 1 } },
                new { channel_id = 33, type = "anonymous", last_hide_at = 2000, last_view_at = 2500, unread = 0 },
                new { channel_id = 34, type = "public", last_hide_at = 2000, last_view_at = 1000, unread = 1, unread_mention = 0, preferences = new { notification_desktop = "mention_subscribe" } },
                new { channel_id = 35, type = "private", last_hide_at = 2000, last_view_at = 1000, unread = 1, preferences = new { notification_desktop = "all" } },
                new { channel_id = 36, type = "public", last_hide_at = 2000, last_view_at = 1000, unread = 1, unread_mention = 1 },
                new { channel_id = 37, type = "anonymous", last_hide_at = 2000, last_view_at = 1000, last_post_at = 2500 },
                new { channel_id = 3, type = "synobot", last_hide_at = 2000, last_view_at = 1000, unread = 0 },
                new { channel_id = 1, type = "public", hide_global_at = 1000 }
            ));
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            var ids = (await client.GetConversationsAsync()).Select(channel => channel.Id).OrderBy(id => id).ToArray();
            Check(ids.SequenceEqual(new long[] { 3, 31, 32, 33, 35, 36, 37 }));
            handler.AssertComplete();
        });
        await RunAsync("Inherited server notification policy is read once without changing settings", async () =>
        {
            var inheritedChannel = new { channel_id = 40, type = "public", last_hide_at = 2000,
                last_view_at = 1000, unread = 1, preferences = new { notification_desktop = "by_admin_console" } };
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(inheritedChannel))
                .Expect("SYNO.Chat.Admin.Setting", "get", new { success = true, data = new { notify_type = "all" } })
                .Expect("SYNO.Chat.Channel", "list", ConversationList(inheritedChannel));
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            Check((await client.GetConversationsAsync()).Any(channel => channel.Id == 40));
            Check((await client.GetConversationsAsync()).Any(channel => channel.Id == 40));
            Equal(1, handler.Requests.Count(request => request.Fields["api"] == "SYNO.Chat.Admin.Setting"));
            Check(handler.Requests.All(request => request.Fields["method"] != "set"));
            handler.AssertComplete();
        });
        await RunAsync("Hide rejection preserves cached visible conversation and permission error", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord()))
                .Expect("SYNO.Chat.Channel", "hide", new { success = false, error = new { code = 404 } });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.GetConversationsAsync();
            var error = await ThrowsAsync<SynologyApiException>(() => client.HideConversationAsync(ChannelId));
            Equal(404, error.Code);
            Check(!error.Message.Contains("OTP"));
            Equal(ChannelId, (await client.OpenDirectConversationAsync(OtherId)).Id);
            Equal(6, handler.Requests.Count);
            handler.AssertComplete();
        });
        await RunAsync("Reopening cached hidden DM uses show without creating a new room", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(2000, 1000, 1500)))
                .Expect("SYNO.Chat.Channel", "show", new { success = true })
                .Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(0, 1000, 1500)));
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            Equal(0, (await client.GetConversationsAsync()).Count);
            Equal(ChannelId, (await client.OpenDirectConversationAsync(OtherId)).Id);
            Equal("2", handler.Requests[^1].Fields["version"]);
            Check((await client.GetConversationsAsync()).Any(channel => channel.Id == ChannelId));
            Check(handler.Requests.All(request => request.Fields["api"] != "SYNO.Chat.Channel.Anonymous"));
            handler.AssertComplete();
        });
        await RunAsync("Show failure keeps hidden state for a later explicit reopen", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(2000, 1000, 1500)))
                .Expect("SYNO.Chat.Channel", "show", new { success = false, error = new { code = 404 } })
                .Expect("SYNO.Chat.Channel", "show", new { success = true });
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.GetConversationsAsync();
            await ThrowsAsync<SynologyApiException>(() => client.OpenDirectConversationAsync(OtherId));
            Equal(ChannelId, (await client.OpenDirectConversationAsync(OtherId)).Id);
            handler.AssertComplete();
        });
        await RunAsync("Self hide is rejected without calling any hide API", async () =>
        {
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(new { channel_id = 3, type = "synobot" }));
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await ThrowsAsync<SynologyApiException>(() => client.HideConversationAsync(3));
            Equal(5, handler.Requests.Count);
            handler.AssertComplete();
        });
        await RunAsync("Displayed-message read receipt precedes hide and prevents old unread resurfacing", async () =>
        {
            const long displayedAt = 1792800000000;
            var handler = LoginHandler().Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(0, 0, displayedAt, 1)))
                .Expect("SYNO.Chat.Channel", "view", new { success = true })
                .Expect("SYNO.Chat.Channel", "hide", new { success = true, data = new { last_hide_at = displayedAt + 1000 } })
                .Expect("SYNO.Chat.Channel", "list", ConversationList(DirectRecord(displayedAt + 1000, displayedAt, displayedAt, 0)));
            using var client = new SynologyChatClient(Server, handler);
            await client.LoginAsync("park", Password);
            await client.GetConversationsAsync();
            await client.MarkConversationReadAsync(ChannelId, displayedAt);
            var view = handler.Requests[^1];
            Equal("2", view.Fields["version"]);
            Equal(displayedAt, JsonSerializer.Deserialize<long>(view.Fields["last_view_at"]));
            await client.HideConversationAsync(ChannelId);
            Equal(0, (await client.GetConversationsAsync()).Count);
            handler.AssertComplete();
        });
        await RunAsync("Cancellation and disposal prevent requests", async () =>
        {
            var handler = new ScriptedHttpHandler();
            using var client = new SynologyChatClient(Server, handler);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => client.LoginAsync("park", Password, ct: cancelled.Token));
            Equal(0, handler.Requests.Count);
            client.Dispose();
            await ThrowsAsync<ObjectDisposedException>(() => client.LoginAsync("park", Password));
        });
    }

    private static ScriptedHttpHandler LoginHandler() => new ScriptedHttpHandler()
        .Expect("SYNO.API.Info", "query", Discovery())
        .Expect("SYNO.API.Auth", "login", new { success = true, data = new { sid = Sid, synotoken = Token } })
        .Expect("SYNO.Chat.User", "login", new { success = true, data = new { current_time = 1792800000, first_time = false, user = new { user_id = 7, username = "p.park", nickname = "박범준" } } })
        .Expect("SYNO.Chat.User", "list", Users());

    private static object Discovery(string authPath = "entry.cgi") => new { success = true, data = new Dictionary<string, object>
    {
        ["SYNO.API.Auth"] = new { path = authPath, minVersion = 1, maxVersion = 7 },
        ["SYNO.Chat.User"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 3, requestFormat = "JSON" },
        ["SYNO.Chat.Channel"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 5, requestFormat = "JSON" },
        ["SYNO.Chat.Post"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 5, requestFormat = "JSON" },
        ["SYNO.Chat.Channel.Anonymous"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 2, requestFormat = "JSON" },
        ["SYNO.Chat.Admin.Setting"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 5, requestFormat = "JSON" }
    } };
    private static object Users() => new { success = true, data = new { users = new object[]
    {
        new { user_id = 7, username = "p.park", nickname = "박범준", type = "human", human_type = "dsm", is_disabled = false, deleted = false },
        new { user_id = OtherId.ToString(), username = "hong", nickname = "홍길동", type = "human", human_type = "dsm" },
        new { user_id = 10, username = "bot", nickname = "ERP봇", type = "bot" },
        new { user_id = 11, username = "disabled", is_disabled = true },
        new { user_id = 12, username = "deleted", is_disabled = false, deleted = true }
    } } };
    private static object Channels() => new { success = true, data = new { channels = new object[]
    {
        new { channel_id = 1, name = "", type = "public" },
        new { channel_id = 2, name = "", type = "public" },
        new { channel_id = ChannelId.ToString(), name = "", type = "anonymous", members = new[] { 7L, OtherId }, encrypted = false, is_joined = true },
        new { channel_id = 19, name = "", members = new[] { 7 }, encrypted = false },
        new { channel_id = 20, name = "보호된 대화", members = new[] { 7 }, encrypted = true },
        new { channel_id = 21, name = "참여 안함", is_joined = false }
    } } };
    private static object ConversationList(params object[] channels) => new { success = true, data = new { channels } };
    private static object DirectRecord(long lastHide = 0, long lastView = 0, long lastPost = 0, int unread = 0) => new
    {
        channel_id = ChannelId, name = "", type = "anonymous", members = new[] { 7L, OtherId },
        last_hide_at = lastHide, last_view_at = lastView, last_post_at = lastPost, unread
    };
    private static void AssertSafe(Exception error)
    {
        foreach (var secret in new[] { Password, Sid, Token, "123456" }) Check(!error.ToString().Contains(secret, StringComparison.Ordinal));
    }
}
