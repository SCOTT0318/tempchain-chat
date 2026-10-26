using TempChain.Chat;
using System.Text.Json;

internal static class Program
{
    private static int _passed;
    private static int _failed;
    private static async Task<int> Main()
    {
        Run("Fixed company connection is valid HTTPS", () => Check(NavigationPolicy.TryGetServerUri(ServerConfiguration.DefaultServerUrl, out _)));
        Run("Startup command quotes paths with spaces and Korean", () =>
        {
            var path = OperatingSystem.IsWindows() ? @"C:\Program Files\템프체인\TempChain.Chat.exe" : "/opt/템프체인 Chat/TempChain.Chat.exe";
            Equal($"\"{path}\" --minimized", StartupRegistration.BuildCommand(path));
        });
        foreach (var path in new[] { "", @"relative\chat.exe", "C:\\Chat.exe\" --argument", "C:\\Chat.exe\r\n" })
            Run("Reject unsafe startup path", () => Throws<ArgumentException>(() => StartupRegistration.BuildCommand(path)));
        Run("Personal settings preserve unrelated data and ignore legacy server overrides", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "TempChain.Chat.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var store = new SettingsStore(root);
                var unrelated = Path.Combine(root, "user-data.keep");
                File.WriteAllText(unrelated, "preserve");
                var saved = new AppSettings { MinimizeToTray = false, StartWithWindows = true };
                store.Save(saved);
                Equal(saved, new SettingsStore(root).Load());
                Equal("preserve", File.ReadAllText(unrelated));
                Check(!File.Exists(store.SettingsPath + ".tmp"));
                var fixedConnection = ServerConfiguration.DefaultServerUrl;
                foreach (var legacyUrl in new[] { "\"https://untrusted.example.test/\"", "\"file:///C:/Windows/win.ini\"", "null", "{\"host\":\"untrusted.example.test\"}" })
                {
                    var legacyJson = "{\"ServerUrl\":" + legacyUrl + ",\"MinimizeToTray\":false,\"StartWithWindows\":true}";
                    File.WriteAllText(store.SettingsPath, legacyJson);
                    Equal(saved, store.Load());
                    Check(store.LoadWarning is null);
                    Equal(fixedConnection, ServerConfiguration.DefaultServerUrl);
                    Equal(legacyJson, File.ReadAllText(store.SettingsPath));
                    store.Save(store.Load());
                    using var serialized = JsonDocument.Parse(File.ReadAllText(store.SettingsPath));
                    Equal(5, serialized.RootElement.EnumerateObject().Count());
                    Check(!serialized.RootElement.TryGetProperty("ServerUrl", out _));
                    Check(serialized.RootElement.TryGetProperty("MinimizeToTray", out _));
                    Check(serialized.RootElement.TryGetProperty("StartWithWindows", out _));
                }
                foreach (var damaged in new[] { "{invalid", "null", "{\"MinimizeToTray\":\"yes\"}", new string('x', 65537) })
                {
                    File.WriteAllText(store.SettingsPath, damaged);
                    Equal(new AppSettings(), store.Load());
                    Check(!string.IsNullOrWhiteSpace(store.LoadWarning));
                    Equal(damaged, File.ReadAllText(store.SettingsPath));
                }
            }
            finally
            {
                var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TempChain.Chat.Tests")) + Path.DirectorySeparatorChar;
                Check(Path.GetFullPath(root).StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase));
                Directory.Delete(root, recursive: true);
            }
        });
        await ApiTests.RunAllAsync();
        await RoomTests.RunAllAsync();
        NotificationTests.RunAll();
        await UpdateTests.RunAllAsync();
        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }
    internal static void Run(string name, Action test) => RunAsync(name, () => { test(); return Task.CompletedTask; }).GetAwaiter().GetResult();
    internal static async Task RunAsync(string name, Func<Task> test)
    {
        try { await test(); _passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception exception) { _failed++; Console.Error.WriteLine($"FAIL {name}: {exception.GetType().Name}: {exception.Message}"); }
    }
    internal static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Expected condition to be true."); }
    internal static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
    internal static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    internal static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T exception) { return exception; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
