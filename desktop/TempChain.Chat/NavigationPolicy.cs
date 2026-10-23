using System;

namespace TempChain.Chat;

public static class NavigationPolicy
{
    public static bool TryGetServerUri(string? input, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 4096 || input.Contains('\\')) return false;
        foreach (var character in input)
            if (char.IsControl(character) || char.IsWhiteSpace(character)) return false;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(candidate.Host) ||
            candidate.UserInfo.Length > 0 || candidate.Fragment.Length > 0) return false;
        uri = candidate;
        return true;
    }

    public static bool IsSameOrigin(Uri trusted, Uri candidate) =>
        candidate.IsAbsoluteUri && string.Equals(trusted.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(trusted.IdnHost, candidate.IdnHost, StringComparison.OrdinalIgnoreCase) &&
        trusted.Port == candidate.Port && candidate.UserInfo.Length == 0;

    public static bool CanOpenExternal(string? candidate) =>
        Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) &&
        uri.UserInfo.Length == 0 && !string.IsNullOrWhiteSpace(uri.Host);
}
