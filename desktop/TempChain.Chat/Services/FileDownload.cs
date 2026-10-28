using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TempChain.Chat.Services;

public sealed partial class SynologyChatClient
{
    public const long MaximumDownloadBytes = 512L * 1024 * 1024;

    public static string SafeDownloadName(string name)
    {
        name = Path.GetFileName(name.Replace('\\', '/')).Trim().TrimEnd('.');
        foreach (var character in Path.GetInvalidFileNameChars()) name = name.Replace(character, '_');
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9')) name = "_" + name;
        return string.IsNullOrWhiteSpace(name) ? "첨부파일.bin" : name;
    }

    // File content stays in an authenticated POST to the fixed Chat origin; no session URLs or shell launch.
    public async Task DownloadFileAsync(long channelId, long postId, string destination,
        CancellationToken ct = default)
    {
        EnsureSession(); CheckChannel(channelId);
        if (postId <= 0) throw new SynologyApiException(400, "다운로드할 파일을 선택하세요.");
        if (!apis.TryGetValue("SYNO.Chat.Post.File", out var api) || api.Minimum > 2 || api.Maximum < 2)
            throw new SynologyApiException(104, "서버에서 파일 다운로드 API를 제공하지 않습니다.");
        var fields = new Dictionary<string, string> {
            ["api"] = "SYNO.Chat.Post.File", ["method"] = "get", ["version"] = "2",
            ["post_id"] = postId.ToString(CultureInfo.InvariantCulture), ["_sid"] = sid!
        };
        if (!string.IsNullOrEmpty(synoToken)) fields["SynoToken"] = synoToken;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "webapi/" + api.Path))
            { Content = new FormUrlEncodedContent(fields) };
        if (!string.IsNullOrEmpty(synoToken)) request.Headers.TryAddWithoutValidation("X-SYNO-TOKEN", synoToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var target = Path.GetFullPath(destination);
        var temporary = target + ".tchat-" + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new SynologyApiException((int)response.StatusCode, "파일을 다운로드하지 못했습니다. 접근 권한과 연결 상태를 확인해 주세요.");
            if (response.Content.Headers.ContentLength > MaximumDownloadBytes)
                throw new SynologyApiException(413, "다운로드 가능한 파일 크기(512MB)를 초과했습니다.");
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                var buffer = new byte[65536]; long received = 0; int count;
                while ((count = await input.ReadAsync(buffer.AsMemory(), timeout.Token)) != 0)
                {
                    received += count;
                    if (received > MaximumDownloadBytes) throw new SynologyApiException(413, "다운로드 가능한 파일 크기(512MB)를 초과했습니다.");
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                }
                if (response.Content.Headers.ContentLength is long expected && expected != received)
                    throw new IOException("파일 수신이 완료되지 않았습니다.");
            }
            // An API failure can be HTTP 200 JSON. Never replace a user's destination with that error body.
            if (response.Content.Headers.ContentDisposition is null &&
                response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true &&
                new FileInfo(temporary).Length <= MaximumResponseBytes)
            {
                using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(temporary, timeout.Token));
                if (json.RootElement.ValueKind == JsonValueKind.Object &&
                    json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False)
                {
                    var code = json.RootElement.TryGetProperty("error", out var error) ? (int)ReadLong(error, "code") : 0;
                    if (code is 460 or 461) AccessRestricted?.Invoke(code);
                    throw new SynologyApiException(code, "파일을 다운로드하지 못했습니다. 파일이 삭제되었거나 접근 권한이 없을 수 있습니다.");
                }
            }
            timeout.Token.ThrowIfCancellationRequested();
            File.Move(temporary, target, true);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new SynologyApiException(0, "파일 다운로드 시간이 초과했습니다."); }
        catch (HttpRequestException)
        { throw new SynologyApiException(0, "파일 다운로드 연결이 끊어졌습니다. 다시 시도해 주세요."); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
