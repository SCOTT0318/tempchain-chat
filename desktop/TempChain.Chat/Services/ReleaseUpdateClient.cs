using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TempChain.Chat.Services;

public sealed record UpdateRelease(Version Version, Uri DownloadUri, string Sha256, long Size);

/// <summary>Public release feed only. Never sends NAS or GitHub credentials.</summary>
public sealed class ReleaseUpdateClient : IDisposable
{
    public const string Repository = "SCOTT0318/tempchain-chat";
    public const long MaximumInstallerBytes = 300L * 1024 * 1024;
    private readonly HttpClient http;
    public Version? LatestVersion { get; private set; }

    public ReleaseUpdateClient(HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TempChainChat-Updater/1.0");
    }

    public static string InstallerName(Version version) => $"TChat-Setup-{version.ToString(3)}.exe";

    public static UpdateRelease? ParseRelease(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant) ||
            !Version.TryParse(tag[1..], out var version)) throw new InvalidDataException("릴리스 버전 형식이 올바르지 않습니다.");
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
        var name = InstallerName(version);
        var matches = root.GetProperty("assets").EnumerateArray().Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("업데이트 설치 파일을 확인하지 못했습니다.");
        var asset = matches[0];
        var digest = asset.GetProperty("digest").GetString() ?? "";
        var size = asset.GetProperty("size").GetInt64();
        var expectedUrl = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        if (asset.GetProperty("state").GetString() != "uploaded" || size is <= 0 or > MaximumInstallerBytes ||
            !Regex.IsMatch(digest, "^sha256:[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant) ||
            asset.GetProperty("browser_download_url").GetString() != expectedUrl)
            throw new InvalidDataException("업데이트 파일의 주소·크기·체크섬을 확인하지 못했습니다.");
        return new UpdateRelease(version, new Uri(expectedUrl), digest[7..].ToLowerInvariant(), size);
    }

    public async Task<UpdateRelease?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        ct = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("릴리스를 조회할 수 없습니다. 저장소 공개 상태와 정식 릴리스 게시 여부를 확인해 주세요.");
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("업데이트 서버의 요청 한도에 도달했습니다. 잠시 후 다시 확인해 주세요.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 1048576) throw new InvalidDataException("릴리스 응답이 너무 큽니다.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var bytes = new byte[16384];
        int count;
        while ((count = await source.ReadAsync(bytes, ct)) > 0)
        {
            if (buffer.Length + count > 1048576) throw new InvalidDataException("릴리스 응답이 너무 큽니다.");
            buffer.Write(bytes, 0, count);
        }
        var json = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        var result = ParseRelease(json, current);
        using var parsed = JsonDocument.Parse(json);
        LatestVersion = !parsed.RootElement.GetProperty("draft").GetBoolean() && !parsed.RootElement.GetProperty("prerelease").GetBoolean()
            ? Version.Parse(parsed.RootElement.GetProperty("tag_name").GetString()![1..]) : null;
        return result;
    }

    public async Task<string> DownloadAsync(UpdateRelease release, string stagingRoot, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        ct = timeout.Token;
        var expectedUrl = $"https://github.com/{Repository}/releases/download/v{release.Version.ToString(3)}/{InstallerName(release.Version)}";
        if (release.DownloadUri.AbsoluteUri != expectedUrl || release.Size is <= 0 or > MaximumInstallerBytes ||
            !Regex.IsMatch(release.Sha256, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("검증되지 않은 업데이트입니다.");
        var directory = Path.Combine(stagingRoot, release.Version.ToString(3) + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var partial = Path.Combine(directory, "installer.partial");
        var destination = Path.Combine(directory, InstallerName(release.Version));
        try
        {
            using var response = await DownloadResponseAsync(release.DownloadUri, ct);
            if (response.Content.Headers.ContentLength is long length && length != release.Size)
                throw new InvalidDataException("업데이트 파일 크기가 일치하지 않습니다.");
            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[65536];
                long total = 0;
                var lastProgress = -1;
                int count;
                while ((count = await source.ReadAsync(buffer, ct)) > 0)
                {
                    total += count;
                    if (total > release.Size) throw new InvalidDataException("업데이트 파일 크기가 일치하지 않습니다.");
                    hash.AppendData(buffer, 0, count);
                    await target.WriteAsync(buffer.AsMemory(0, count), ct);
                    var percent = (int)(total * 100 / release.Size);
                    if (percent != lastProgress) { lastProgress = percent; progress?.Report(percent); }
                }
                if (total != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("업데이트 파일 체크섬이 일치하지 않습니다. 설치하지 않았습니다.");
            }
            ct.ThrowIfCancellationRequested();
            File.Move(partial, destination);
            return destination;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private async Task<HttpResponseMessage> DownloadResponseAsync(Uri uri, CancellationToken ct)
    {
        for (var redirects = 0; redirects <= 3; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new InvalidDataException("다운로드 경로를 확인하지 못했습니다.");
                uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                    uri.Host is not ("release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new InvalidDataException("허용하지 않은 다운로드 경로입니다.");
                continue;
            }
            if (!response.IsSuccessStatusCode) { response.Dispose(); throw new HttpRequestException("업데이트 파일을 다운로드하지 못했습니다."); }
            return response;
        }
        throw new InvalidDataException("다운로드 리디렉션이 너무 많습니다.");
    }

    public void Dispose() => http.Dispose();
}
