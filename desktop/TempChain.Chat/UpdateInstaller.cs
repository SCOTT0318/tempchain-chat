using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TempChain.Chat;

internal static class UpdateInstaller
{
    public static string InstalledExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TempChainChat", "TempChain.Chat.exe");
    public static bool IsInstalledApp => string.Equals(Environment.ProcessPath, InstalledExecutable, StringComparison.OrdinalIgnoreCase) &&
        File.Exists(Path.Combine(Path.GetDirectoryName(InstalledExecutable)!, ".tempchain-chat-install"));

    internal static string BuildScript(int parentId, long parentStarted, string installer, string digest, string resultPath, string executable, string version)
    {
        static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
        // Data is single-quoted; only this fixed helper is executed. No user or feed text becomes PowerShell code.
        return $$"""
            $ErrorActionPreference = 'Stop'
            $installer = {{Literal(installer)}}
            $digest = {{Literal(digest)}}
            $resultPath = {{Literal(resultPath)}}
            $executable = {{Literal(executable)}}
            $version = {{Literal(version)}}
            try {
                $parent = Get-Process -Id {{parentId}} -ErrorAction SilentlyContinue
                if ($parent) {
                    try { $sameParent = $parent.StartTime.ToUniversalTime().Ticks -eq {{parentStarted}} } catch { $sameParent = $false }
                    if ($sameParent -and -not $parent.WaitForExit(60000)) { throw 'app did not exit' }
                }
                if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash -ne $digest) { throw 'checksum' }
                $setup = Start-Process -FilePath $installer -ArgumentList '/S','/UPDATE' -WindowStyle Hidden -Wait -PassThru
                if ($setup.ExitCode -ne 0) { throw "installer exit $($setup.ExitCode)" }
                $installedVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion.Split('+')[0]
                if ($installedVersion -ne $version) { throw 'installed version mismatch' }
                @{success=$true; version=$version; checkedAt=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
                Start-Process -FilePath $executable
            } catch {
                @{success=$false; checkedAt=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
                Add-Type -AssemblyName System.Windows.Forms
                [System.Windows.Forms.MessageBox]::Show('업데이트를 완료하지 못했습니다. 기존 앱을 다시 실행하고 설정에서 업데이트를 확인해 주세요.','TempChain Chat 업데이트') | Out-Null
                exit 1
            }
            """;
    }

    public static void Start(string installer, string digest, string version)
    {
        if (!IsInstalledApp) throw new InvalidOperationException("자동 설치는 설치된 TempChain Chat에서 사용할 수 있습니다.");
        using (var stream = File.OpenRead(installer))
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(digest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("설치 파일이 변경되어 업데이트를 중단했습니다.");
        using var process = Process.GetCurrentProcess();
        var script = BuildScript(process.Id, process.StartTime.ToUniversalTime().Ticks, installer, digest,
            Path.Combine(Path.GetDirectoryName(installer)!, "update-result.json"), InstalledExecutable, version);
        var shellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(shellPath) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using var helper = Process.Start(start) ?? throw new IOException("업데이트 설치 도우미를 시작하지 못했습니다.");
    }
}
