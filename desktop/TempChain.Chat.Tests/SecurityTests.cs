using System.Security.Cryptography;
using System.Text;
using TempChain.Chat;
using TempChain.Chat.Services;
using static Program;

internal static class SecurityTests
{
    internal static void RunAll()
    {
        Run("App lock hashes use fresh salts, reject wrong passwords and preserve NAS credentials", () => {
            var first = AppLockPassword.Create("fixture-user", "fixture-nas-password", "fixture-lock-password");
            var second = AppLockPassword.Create("fixture-user", "fixture-nas-password", "fixture-lock-password");
            Check(first.LockHash != second.LockHash && first.LockSalt != second.LockSalt);
            Check(AppLockPassword.Verify(first, "fixture-lock-password"));
            Check(!AppLockPassword.Verify(first, "wrong-password"));
            Equal("fixture-nas-password", first.Password);
            Throws<ArgumentException>(() => AppLockPassword.Create("u", "p", "12345"));
            Throws<ArgumentException>(() => AppLockPassword.Create("u", "p", new string('x',129)));
        });
        Run("Idle lock honors activity and exact timeout boundaries without depending on wall clock", () => {
            var timer = new AppLockTimer(1000);
            Check(!timer.IsDue(300999,5)); Check(timer.IsDue(301000,5));
            timer.Touch(200000); Check(!timer.IsDue(301000,5)); Check(timer.IsDue(500000,5));
            Check(timer.IsDue(260000,1)); Check(!timer.IsDue(260000,60));
            Equal(1,(new AppSettings{AutoLockMinutes=-1}).Normalize().AutoLockMinutes);
            Equal(60,(new AppSettings{AutoLockMinutes=999}).Normalize().AutoLockMinutes);
        });
        if (!OperatingSystem.IsWindows()) return;
        Run("Windows vault survives app restart, encrypts credentials and fails closed on tampering", () => {
            WithStore((store, root) => {
                var login = AppLockPassword.Create("fixture-user", "fixture-nas-password", "fixture-lock-password");
                store.Save(login);
                Equal(login, new PersistentLoginStore(root).Load()!);
                var path = Path.Combine(root,"login.dat");
                var bytes = File.ReadAllBytes(path);
                Check(!Encoding.UTF8.GetString(bytes).Contains("fixture-nas-password"));
                Check(!Encoding.Unicode.GetString(bytes).Contains("fixture-nas-password"));
                bytes[^1] ^= 0xff; File.WriteAllBytes(path,bytes);
                Throws<CryptographicException>(() => store.Load());
                store.Clear(); Check(store.Load() is null);
            });
        });
        Run("Updating saved login cannot reset notification duration or lock preferences", () => {
            WithStore((vault, root) => {
                var settings = new SettingsStore(root);
                settings.Save(new AppSettings { NotificationDurationSeconds=3, AutoLockMinutes=10, ShowNotificationSender=false });
                var before = File.ReadAllBytes(settings.SettingsPath);
                vault.Save(AppLockPassword.Create("fixture-user","fixture-nas-password","fixture-lock-password"));
                Check(before.SequenceEqual(File.ReadAllBytes(settings.SettingsPath)));
                Equal(3,new SettingsStore(root).Load().NotificationDurationSeconds);
                Equal(10,new SettingsStore(root).Load().AutoLockMinutes);
                vault.Clear(); Check(before.SequenceEqual(File.ReadAllBytes(settings.SettingsPath)));
            });
        });
    }
    private static void WithStore(Action<PersistentLoginStore,string> test)
    {
        var parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"TempChain.Security.Tests"));
        var root=Path.Combine(parent,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(new PersistentLoginStore(root), root); }
        finally { if (Path.GetFullPath(root).StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) Directory.Delete(root,true); }
    }
}
