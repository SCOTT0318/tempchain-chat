using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TempChain.Chat;

public static class ShortcutRegistration
{
    public static string GetPath(bool desktop) => desktop
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TChat.lnk")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TChat", "TChat.lnk");

    public static void MigrateLegacyShortcuts()
    {
        if (!UpdateInstaller.IsInstalledApp) return;
        var executable = UpdateInstaller.InstalledExecutable;
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var oldFolder = Path.Combine(programs, "TempChain Chat");
        Migrate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TempChain Chat.lnk"), GetPath(true), executable);
        Migrate(Path.Combine(oldFolder, "TempChain Chat.lnk"), GetPath(false), executable);
        Migrate(Path.Combine(oldFolder, "제거.lnk"), Path.Combine(programs, "TChat", "제거.lnk"), Path.Combine(Path.GetDirectoryName(executable)!, "Uninstall.exe"));
        try { if (Directory.Exists(oldFolder) && Directory.GetFileSystemEntries(oldFolder).Length == 0) Directory.Delete(oldFolder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    private static void Migrate(string oldPath, string newPath, string expectedTarget)
    {
        try
        {
            if (!File.Exists(oldPath) || !string.Equals(ReadTarget(oldPath), expectedTarget, StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(newPath))
            {
                if (string.Equals(ReadTarget(newPath), expectedTarget, StringComparison.OrdinalIgnoreCase)) File.Delete(oldPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Move(oldPath, newPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or System.Security.SecurityException) { }
    }

    private static string ReadTarget(string path)
    {
        object? shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!);
            dynamic automation = shell!;
            shortcut = automation.CreateShortcut(path);
            return (string)((dynamic)shortcut).TargetPath;
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    public static void Create(bool desktop, bool overwrite)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("실행 파일을 찾지 못했습니다.");
        if (!File.Exists(executable) || !executable.EndsWith("TempChain.Chat.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("TChat 앱에서 실행해 주세요.");
        var path = GetPath(desktop);
        if (File.Exists(path) && !overwrite) throw new IOException("같은 이름의 바로가기가 있습니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!);
            dynamic automation = shell!;
            shortcut = automation.CreateShortcut(path);
            dynamic link = shortcut;
            link.TargetPath = executable;
            link.Arguments = "";
            link.WorkingDirectory = Path.GetDirectoryName(executable);
            link.IconLocation = executable + ",0";
            link.Description = "TChat";
            link.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
