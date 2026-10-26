using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TempChain.Chat;
using TempChain.Chat.Services;
using static Program;

internal static class UpdateTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("installer test bytes");
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
    private static readonly Version Current = new(1, 1, 2);
    private static string Feed(string tag = "v1.1.3", string? url = null, string? digest = null, long? size = null, bool preview = false, bool draft = false) => JsonSerializer.Serialize(new {
        tag_name = tag, prerelease = preview, draft,
        assets = new[] { new { name = "TChat-Setup-" + tag.TrimStart('v') + ".exe", state = "uploaded",
            browser_download_url = url ?? $"https://github.com/{ReleaseUpdateClient.Repository}/releases/download/{tag}/TChat-Setup-{tag.TrimStart('v')}.exe",
            digest = digest ?? "sha256:" + Hash, size = size ?? Payload.Length } }
    });

    internal static async Task RunAllAsync()
    {
        Run("Update failures distinguish release packaging, network and local storage errors", () => {
            Check(UpdateFailureMessage.For(new InvalidDataException()).Contains("배포 파일"));
            Check(!UpdateFailureMessage.For(new InvalidDataException()).Contains("인터넷"));
            Check(UpdateFailureMessage.For(new HttpRequestException()).Contains("연결"));
            Check(UpdateFailureMessage.For(new UnauthorizedAccessException()).Contains("권한"));
            Check(UpdateFailureMessage.For(new IOException()).Contains("디스크"));
        });
        await RunAsync("Missing installer keeps the published version for actionable error display", async () => {
            var legacyFeed = Feed("v1.1.5").Replace("TChat-Setup-", "TempChainChat-Setup-");
            using var client = new ReleaseUpdateClient(new Handler(_ => Json(legacyFeed)));
            await ThrowsAsync<InvalidDataException>(() => client.CheckAsync(Current));
            Equal(new Version(1,1,5), client.LatestVersion!);
        });
        Run("Updater compares numeric versions and ignores equal, older and preview releases", () => {
            Check(ReleaseUpdateClient.ParseRelease(Feed("v1.1.10"), Current)!.Version > Current);
            Check(ReleaseUpdateClient.ParseRelease(Feed("v1.1.2"), Current) is null);
            Check(ReleaseUpdateClient.ParseRelease(Feed("v1.1.1"), Current) is null);
            Check(ReleaseUpdateClient.ParseRelease(Feed(preview:true), Current) is null);
            Check(ReleaseUpdateClient.ParseRelease(Feed(draft:true), Current) is null);
        });
        foreach (var json in new[] { Feed("v1.1.3-rc.1"), Feed("v01.1.3"), Feed(url:"https://untrusted.example/setup.exe"), Feed(url:$"http://github.com/{ReleaseUpdateClient.Repository}/setup.exe"), Feed(digest:"sha256:bad"), Feed(size:0), Feed(size:ReleaseUpdateClient.MaximumInstallerBytes+1) })
            Run("Updater rejects unsafe or unverifiable release metadata", () => Throws<InvalidDataException>(() => ReleaseUpdateClient.ParseRelease(json, Current)));

        await RunAsync("Public update check uses fixed HTTPS feed and sends no credentials", async () => {
            using var client = new ReleaseUpdateClient(new Handler(request => {
                Equal($"https://api.github.com/repos/{ReleaseUpdateClient.Repository}/releases/latest",request.RequestUri!.AbsoluteUri);
                Equal(HttpMethod.Get,request.Method); Check(request.Headers.Authorization is null); Check(!request.Headers.Contains("Cookie"));
                return Json(Feed());
            }));
            Equal(new Version(1,1,3),(await client.CheckAsync(Current))!.Version);
        });
        foreach(var status in new[]{HttpStatusCode.NotFound,HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests})
            await RunAsync("Private or rate-limited update feed reports an actionable failure",async()=>{
                using var client=new ReleaseUpdateClient(new Handler(_=>new HttpResponseMessage(status)));
                var error=await ThrowsAsync<InvalidOperationException>(()=>client.CheckAsync(Current));Check(error.Message.Length>10);
            });
        await RunAsync("Version check reports actual published version even if the installed version is newer",async()=>{
            using var client=new ReleaseUpdateClient(new Handler(_=>Json(Feed("v1.1.0"))));
            Check(await client.CheckAsync(Current) is null);Equal(new Version(1,1,0),client.LatestVersion!);
        });

        await WithRoot(async root => await RunAsync("Verified update download follows GitHub asset redirect and preserves unrelated files",async()=>{
            File.WriteAllText(Path.Combine(root,"keep.txt"),"keep");
            var calls=0;
            using var client=new ReleaseUpdateClient(new Handler(request=>{
                Check(request.Headers.Authorization is null);Check(!request.Headers.Contains("Cookie"));
                if(calls++==0){var redirect=new HttpResponseMessage(HttpStatusCode.Found);redirect.Headers.Location=new Uri("https://release-assets.githubusercontent.com/test/installer.exe?signature=test");return redirect;}
                Equal("release-assets.githubusercontent.com",request.RequestUri!.Host);
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Payload)};
            }));
            var path=await client.DownloadAsync(ReleaseUpdateClient.ParseRelease(Feed(),Current)!,root);
            Check(File.ReadAllBytes(path).SequenceEqual(Payload));Equal("keep",File.ReadAllText(Path.Combine(root,"keep.txt")));
            Equal(2,calls);Check(!Directory.EnumerateFiles(root,"*.partial",SearchOption.AllDirectories).Any());
        }));

        foreach(var kind in new[]{"checksum","length","redirect","canceled"})
            await WithRoot(async root=>await RunAsync("Failed update cannot leave an executable: "+kind,async()=>{
                using var client=new ReleaseUpdateClient(new Handler(_=>{
                    if(kind=="redirect"){var r=new HttpResponseMessage(HttpStatusCode.Found);r.Headers.Location=new Uri("http://untrusted.example/installer.exe");return r;}
                    return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(kind=="length"?new byte[1]:Payload)};
                }));
                var release=ReleaseUpdateClient.ParseRelease(Feed(digest:kind=="checksum"?"sha256:"+new string('0',64):null),Current)!;
                using var cancellation=new CancellationTokenSource();if(kind=="canceled")cancellation.Cancel();
                if(kind=="canceled")await ThrowsAsync<OperationCanceledException>(()=>client.DownloadAsync(release,root,ct:cancellation.Token));
                else await ThrowsAsync<InvalidDataException>(()=>client.DownloadAsync(release,root));
                Check(!Directory.EnumerateFiles(root,"*.exe",SearchOption.AllDirectories).Any());
                Check(!Directory.EnumerateFiles(root,"*.partial",SearchOption.AllDirectories).Any());
            }));

        Run("Updater helper quotes Korean paths, apostrophes and shell text as literal data",()=>{
            var script=UpdateInstaller.BuildScript(123,456,@"C:\검사 폴더\O'Brien\$(whoami).exe",Hash,@"C:\검사\result.json",@"C:\앱\TempChain.Chat.exe","1.1.3");
            Check(script.Contains("O''Brien"));Check(script.Contains("-ArgumentList '/S','/UPDATE'"));
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"update-helper-test.ps1"),script);
        });
    }

    private static HttpResponseMessage Json(string value)=>new(HttpStatusCode.OK){Content=new StringContent(value,Encoding.UTF8,"application/json")};
    private static async Task WithRoot(Func<string,Task> action)
    {
        var parent=Path.Combine(Path.GetTempPath(),"TempChain.Update.Tests");
        var root=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{await action(root);}finally{if(Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(respond(request));}
    }
}
