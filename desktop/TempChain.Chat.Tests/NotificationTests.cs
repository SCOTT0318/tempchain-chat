using System.Text.Json;
using TempChain.Chat;
using TempChain.Chat.Models;
using static Program;

internal static class NotificationTests
{
    private static ChatConversation Room(long id,long at,long unread=1,bool own=false)=>new(id,"방 "+id,"channel",new[]{7L,9L},IsSelf:own,ChannelType:"private",LastPostAt:at,UnreadCount:unread);
    internal static void RunAll()
    {
        Run("Initial unread history does not produce notification",()=>{var tracker=new NotificationTracker();Equal(0,tracker.Observe(new[]{Room(5,100)}).Count);});
        Run("Other room update notifies once and duplicates do not repeat",()=>{
            var t=new NotificationTracker();t.Observe(new[]{Room(5,100),Room(6,100)});
            Equal(6L,t.Observe(new[]{Room(5,100),Room(6,200)}).Single().Id);Equal(0,t.Observe(new[]{Room(5,100),Room(6,200)}).Count);
        });
        Run("Read and self messages are excluded and older timestamps cannot roll back baseline",()=>{
            var t=new NotificationTracker();t.Observe(new[]{Room(5,100),Room(6,100,own:true)});
            Equal(0,t.Observe(new[]{Room(5,200,0),Room(6,200,own:true)}).Count);
            Equal(0,t.Observe(new[]{Room(5,150)}).Count);Equal(0,t.Observe(new[]{Room(5,200)}).Count);
        });
        Run("Notification tracker resets baseline between sessions",()=>{var t=new NotificationTracker();t.Observe(new[]{Room(5,100)});t.Reset();Equal(0,t.Observe(new[]{Room(5,200)}).Count);});
        Run("Simultaneous updates preserve chronological order",()=>{var t=new NotificationTracker();t.Observe(new[]{Room(5,100),Room(6,100)});var changes=t.Observe(new[]{Room(5,300),Room(6,200)});Check(changes.Select(r=>r.Id).SequenceEqual(new[]{6L,5L}));});
        Run("Legacy settings receive notification defaults",()=>{var s=JsonSerializer.Deserialize<AppSettings>("{\"MinimizeToTray\":false}")!;Check(s.EnableNotifications&&s.AnimateNotifications&&!s.NotificationSound);Equal(5,s.NotificationDurationSeconds);Check(!s.MinimizeToTray);});
        Run("Notification duration normalizes corrupt bounds",()=>{Equal(1,(new AppSettings{NotificationDurationSeconds=-10}).Normalize().NotificationDurationSeconds);Equal(60,(new AppSettings{NotificationDurationSeconds=999}).Normalize().NotificationDurationSeconds);});
        Run("Saving invalid duration cannot overwrite existing settings",()=>{
            var root=Path.Combine(Path.GetTempPath(),"TempChain.Chat.Tests",Guid.NewGuid().ToString("N"));
            try { var store=new SettingsStore(root);store.Save(new AppSettings());var original=File.ReadAllText(store.SettingsPath);Throws<ArgumentOutOfRangeException>(()=>store.Save(new AppSettings{NotificationDurationSeconds=0}));Equal(original,File.ReadAllText(store.SettingsPath)); }
            finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        Run("Custom notification settings round-trip without credentials",()=>{
            var settings=new AppSettings{EnableNotifications=false,AnimateNotifications=false,NotificationSound=true,NotificationDurationSeconds=12};
            Equal(settings,JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!);
            Check(!JsonSerializer.Serialize(settings).Contains("password",StringComparison.OrdinalIgnoreCase));
        });
    }
}
