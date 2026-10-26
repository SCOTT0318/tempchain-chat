using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TempChain.Chat;

public static class ShortcutRegistration
{
    public static string GetPath(bool desktop) => desktop
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TempChain Chat.lnk")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TempChain Chat", "TempChain Chat.lnk");

    public static void Create(bool desktop, bool overwrite)
    {
        var executable = Environment.ProcessPath ?? throw new IOException("실행 파일을 찾지 못했습니다.");
        if (!File.Exists(executable) || !executable.EndsWith("TempChain.Chat.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("설치 또는 ZIP 버전의 TempChain.Chat.exe에서 실행해 주세요.");
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
            link.Description = "TEMPCHAIN 사내 메신저";
            link.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
