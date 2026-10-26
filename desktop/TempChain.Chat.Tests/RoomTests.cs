using System.Text.Json;
using TempChain.Chat.Services;
using static Program;

internal static class RoomTests
{
    private const long Other = 9007199254740993;
    private const long Room = 9007199254740995;
    private static readonly Uri Server = new("https://nas.example.test/");
    private static object Ok => new { success = true };
    private static object Acl(params string[] allowed) => new { success = true, data = new { acls = allowed.Select(name => new { acl_name = name, allow = true }).ToArray() } };
    private static object Rooms(string name = "프로젝트", long id = Room, string type = "private", bool encrypted = false) => new {
        success = true, data = new { channels = new[] { new { channel_id = id, name, type, encrypted, members = new[] { 7L }, is_joined = true } } }
    };
    private static ScriptedHttpHandler Login(bool roomApis = true)
    {
        var apis = new Dictionary<string, object> {
            ["SYNO.API.Auth"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 7 },
            ["SYNO.Chat.User"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 3, requestFormat = "JSON" },
            ["SYNO.Chat.Channel"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 5, requestFormat = "JSON" }
        };
        if (roomApis)
        {
            apis["SYNO.Chat.Channel.Named"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 1, requestFormat = "JSON" };
            apis["SYNO.Chat.Misc"] = new { path = "entry.cgi", minVersion = 1, maxVersion = 1, requestFormat = "JSON" };
        }
        return new ScriptedHttpHandler().Expect("SYNO.API.Info", "query", new { success = true, data = apis })
            .Expect("SYNO.API.Auth", "login", new { success = true, data = new { sid = "private-session" } })
            .Expect("SYNO.Chat.User", "login", new { success = true, data = new { user = new { user_id = 7, username = "me" } } })
            .Expect("SYNO.Chat.User", "list", new { success = true, data = new { users = new object[] {
                new { user_id = 7L, username = "me", type = "human", human_type = "dsm" },
                new { user_id = Other, username = "other", type = "human", human_type = "dsm" },
                new { user_id = 10L, username = "robot", type = "bot" },
                new { user_id = 11L, username = "disabled", is_disabled = true }
            } } });
    }
    internal static async Task RunAllAsync()
    {
        await RunAsync("Named room creates, joins and invites with official JSON schema", async () => {
            var h = Login().Expect("SYNO.Chat.Misc", "acl", Acl("channel_create", "channel_invite"))
                .Expect("SYNO.Chat.Channel.Named", "create", new { success = true, data = new { channel_id = Room } })
                .Expect("SYNO.Chat.Channel.Named", "join", Ok).Expect("SYNO.Chat.Channel.Named", "invite", Ok);
            using var c = new SynologyChatClient(Server, h); await c.LoginAsync("me", "secret");
            var room = await c.CreateRoomAsync(" 프로젝트 ", " 설명 ", false, new[] { Other, Other, 7L });
            Equal(Room, room.Id); Check(room.IsNamedRoom); Equal(2, room.MemberIds.Count);
            var create = h.Requests[5].Fields;
            Equal("private", JsonSerializer.Deserialize<string>(create["type"])); Equal("프로젝트", JsonSerializer.Deserialize<string>(create["name"]));
            Equal("설명", JsonSerializer.Deserialize<string>(create["purpose"])); Equal("false", create["encrypted"]);
            Equal("", JsonSerializer.Deserialize<string>(h.Requests[6].Fields["channel_key_enc"]));
            Equal(Other, JsonSerializer.Deserialize<long[]>(h.Requests[7].Fields["user_ids"])!.Single());
            Equal("[]", h.Requests[7].Fields["channel_key_encs"]); h.AssertComplete();
        });
        await RunAsync("Unsupported room APIs fail closed without mutation", async () => {
            var h=Login(false); using var c=new SynologyChatClient(Server,h); await c.LoginAsync("me","secret");
            await c.RefreshRoomPermissionsAsync(); Check(!c.CanCreateRooms && !c.CanCloseRooms);
            await ThrowsAsync<SynologyApiException>(()=>c.CreateRoomAsync("방","",false,Array.Empty<long>()));
            Equal(4,h.Requests.Count);h.AssertComplete();
        });
        foreach (var permissions in new[] { Array.Empty<string>(), new[] { "channel_create" } })
            await RunAsync("Create and public-room ACL are enforced before mutation", async () => {
                var h=Login().Expect("SYNO.Chat.Misc","acl",Acl(permissions)); using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
                await ThrowsAsync<SynologyApiException>(()=>c.CreateRoomAsync("방","",true,Array.Empty<long>()));
                Equal(5,h.Requests.Count);h.AssertComplete();
            });
        foreach (var ids in new[] { new[] { 0L }, new[] { 10L }, new[] { 11L }, new[] { 999L }, Enumerable.Range(1,201).Select(i=>(long)i).ToArray() })
            await RunAsync("Room invitation rejects unknown, bot, inactive and oversized selections", async () => {
                var h=Login(); using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
                await ThrowsAsync<SynologyApiException>(()=>c.CreateRoomAsync("방","",false,ids));Equal(4,h.Requests.Count);h.AssertComplete();
            });
        foreach (var name in new[] { "", "  ", "bad\nroom", new string('x',65) })
            await RunAsync("Invalid room name never sends a creation request", async () => {
                var h=Login();using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
                await ThrowsAsync<SynologyApiException>(()=>c.CreateRoomAsync(name,"",false,Array.Empty<long>()));h.AssertComplete();
            });
        await RunAsync("Partial creation failure preserves room ID and never retries creation", async () => {
            var h=Login().Expect("SYNO.Chat.Misc","acl",Acl("channel_create","channel_invite"))
                .Expect("SYNO.Chat.Channel.Named","create",new { success=true,data=new {channel_id=Room}})
                .Expect("SYNO.Chat.Channel.Named","join",new {success=false,error=new {code=404}});
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
            var error=await ThrowsAsync<RoomOperationException>(()=>c.CreateRoomAsync("방","",false,new[]{Other}));
            Equal(Room,error.ChannelId);Equal(1,h.Requests.Count(r=>r.Fields["method"]=="create"));h.AssertComplete();
        });
        await RunAsync("Invite failure after join is reported as partial success without rollback or replay", async () => {
            var h=Login().Expect("SYNO.Chat.Misc","acl",Acl("channel_create","channel_invite"))
                .Expect("SYNO.Chat.Channel.Named","create",new {success=true,data=new {channel_id=Room}})
                .Expect("SYNO.Chat.Channel.Named","join",Ok).Expect("SYNO.Chat.Channel.Named","invite",new {success=false,error=new{code=404}});
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
            Equal(Room,(await ThrowsAsync<RoomOperationException>(()=>c.CreateRoomAsync("방","",false,new[]{Other}))).ChannelId);
            Check(c.NotificationConversations.Any(r=>r.Id==Room));h.AssertComplete();
        });
        await RunAsync("Existing room invites only new members and preserves 64-bit user IDs",async()=>{
            var h=Login().Expect("SYNO.Chat.Channel","list",Rooms()).Expect("SYNO.Chat.Misc","acl",Acl("channel_invite"))
                .Expect("SYNO.Chat.Channel.Named","invite",Ok);
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await c.GetConversationsAsync();
            await c.InviteMembersAsync(Room,new[]{Other,Other,7L});Equal(Other,JsonSerializer.Deserialize<long[]>(h.Requests[^1].Fields["user_ids"])!.Single());
            await ThrowsAsync<SynologyApiException>(()=>c.InviteMembersAsync(Room,new[]{Other}));h.AssertComplete();
        });
        await RunAsync("Leave changes membership using Named.disjoin, never socket leave or delete",async()=>{
            var h=Login().Expect("SYNO.Chat.Channel","list",Rooms()).Expect("SYNO.Chat.Misc","acl",Acl("channel_disjoin"))
                .Expect("SYNO.Chat.Channel.Named","disjoin",Ok);
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await c.GetConversationsAsync();await c.LeaveRoomAsync(Room);
            Check(c.NotificationConversations.All(r=>r.Id!=Room));h.AssertComplete();
        });
        foreach (var type in new[]{"anonymous","synobot","public","private"})
            await RunAsync("System, direct and encrypted rooms cannot be changed by named-room actions",async()=>{
                var id=type=="public"?1L:Room;var encrypted=type=="private";
                var h=Login().Expect("SYNO.Chat.Channel","list",Rooms(id:id,type:type,encrypted:encrypted));
                using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await c.GetConversationsAsync();
                await ThrowsAsync<SynologyApiException>(()=>c.InviteMembersAsync(id,new[]{Other}));
                await ThrowsAsync<SynologyApiException>(()=>c.LeaveRoomAsync(id));h.AssertComplete();
            });
        await RunAsync("Close re-fetches current name and rejects stale confirmation without closing",async()=>{
            var h=Login().Expect("SYNO.Chat.Channel","list",Rooms("새 이름"));
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");
            await ThrowsAsync<SynologyApiException>(()=>c.CloseRoomAsync(Room,"이전 이름"));h.AssertComplete();
        });
        await RunAsync("Close rechecks ACL and honors revoked permissions",async()=>{
            var h=Login().Expect("SYNO.Chat.Misc","acl",Acl("channel_close"))
                .Expect("SYNO.Chat.Channel","list",Rooms()).Expect("SYNO.Chat.Misc","acl",Acl());
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await c.RefreshRoomPermissionsAsync();Check(c.CanCloseRooms);
            await ThrowsAsync<SynologyApiException>(()=>c.CloseRoomAsync(Room,"프로젝트"));Check(!c.CanCloseRooms);h.AssertComplete();
        });
        await RunAsync("Confirmed authorized close calls official Channel.close version 4 once",async()=>{
            var h=Login().Expect("SYNO.Chat.Channel","list",Rooms()).Expect("SYNO.Chat.Misc","acl",Acl("channel_close"))
                .Expect("SYNO.Chat.Channel","close",Ok);
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await c.CloseRoomAsync(Room,"프로젝트");
            Equal("4",h.Requests[^1].Fields["version"]);Equal(Room,JsonSerializer.Deserialize<long>(h.Requests[^1].Fields["channel_id"]));h.AssertComplete();
        });
        await RunAsync("Server close denial is not replayed and keeps room state",async()=>{
            var h=Login().Expect("SYNO.Chat.Channel","list",Rooms()).Expect("SYNO.Chat.Misc","acl",Acl("channel_close"))
                .Expect("SYNO.Chat.Channel","close",new{success=false,error=new{code=404}});
            using var c=new SynologyChatClient(Server,h);await c.LoginAsync("me","secret");await ThrowsAsync<SynologyApiException>(()=>c.CloseRoomAsync(Room,"프로젝트"));
            Check(c.NotificationConversations.Any(r=>r.Id==Room));h.AssertComplete();
        });
    }
}
