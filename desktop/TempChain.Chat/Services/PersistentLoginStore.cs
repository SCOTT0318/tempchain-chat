using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace TempChain.Chat.Services;

public sealed record SavedLogin(string UserName, string Password, string LockSalt, string LockHash, string LockMode = "");

/// <summary>Secrets are protected by Windows DPAPI for the current Windows user, never settings.json.</summary>
public sealed class PersistentLoginStore
{
    private readonly string path;
    public PersistentLoginStore(string directory) => path = Path.Combine(directory, "login.dat");
    public bool Exists => File.Exists(path);
    public SavedLogin? Load()
    {
        if (!Exists) return null;
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("저장된 로그인 정보가 올바르지 않습니다.");
        var clear = Protect(File.ReadAllBytes(path), false);
        try
        {
            var login = JsonSerializer.Deserialize<SavedLogin>(clear) ?? throw new InvalidDataException();
            if (login.LockMode is not ("" or "pin4" or "pin6" or "text") ||
                string.IsNullOrWhiteSpace(login.UserName) || string.IsNullOrEmpty(login.Password) ||
                Convert.FromBase64String(login.LockSalt).Length != 16 || Convert.FromBase64String(login.LockHash).Length != 32)
                throw new InvalidDataException();
            return login;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Save(SavedLogin login)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(login);
        try
        {
            var encrypted = Protect(clear, true);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path + ".tmp", encrypted);
            File.Move(path + ".tmp", path, true);
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void Clear() => File.Delete(path);

    private static byte[] Protect(byte[] bytes, bool encrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            Blob output;
            var success = encrypt
                ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new CryptographicException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            try { var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result; }
            finally { for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
        finally { for (var i = 0; i < bytes.Length; i++) Marshal.WriteByte(input.Data, i, 0); Marshal.FreeHGlobal(input.Data); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}

public static class AppLockPassword
{
    public static string ModeFor(string password) => System.Linq.Enumerable.All(password, ch => ch is >= '0' and <= '9')
        ? password.Length switch { 4 => "pin4", 6 => "pin6", _ => "text" } : "text";
    public static SavedLogin Create(string userName, string password, string lockPassword)
    {
        var digitsOnly = lockPassword.Length > 0 && System.Linq.Enumerable.All(lockPassword, ch => ch is >= '0' and <= '9');
        if (lockPassword.Length > 128 || (digitsOnly ? lockPassword.Length is not (4 or 6) :
                lockPassword.Length < 6 || string.IsNullOrWhiteSpace(lockPassword)))
            throw new ArgumentException("숫자는 4자리 또는 6자리, 문자를 포함한 암호는 6자 이상으로 입력해 주세요.");
        var salt = RandomNumberGenerator.GetBytes(16);
        return new SavedLogin(userName, password, Convert.ToBase64String(salt),
            Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(lockPassword, salt, 210000, HashAlgorithmName.SHA256, 32)), ModeFor(lockPassword));
    }
    public static bool Verify(SavedLogin login, string password)
    {
        if (password.Length > 128) return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(login.LockSalt), 210000, HashAlgorithmName.SHA256, 32);
        try { return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(login.LockHash)); }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }
}

public sealed class AppLockTimer
{
    private long lastActivity;
    public AppLockTimer(long now) => lastActivity = now;
    public void Touch(long now) => lastActivity = now;
    public bool IsDue(long now, int minutes) => now - lastActivity >= Math.Clamp(minutes, 1, 60) * 60000L;
}
