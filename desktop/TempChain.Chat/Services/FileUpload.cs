using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TempChain.Chat.Services;

public sealed partial class SynologyChatClient
{
    public const long MaximumFileBytes = 10 * 1024 * 1024;
    public const int MaximumFilesPerBatch = 20;

    public static void ValidateUploadFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new IOException("선택한 파일을 찾을 수 없습니다.");
        if (info.Length > MaximumFileBytes)
            throw new SynologyApiException(400, "파일 하나의 크기는 10MB 이하여야 합니다.");
    }

    public async Task SendFileAsync(long channelId, string path, CancellationToken ct = default)
    {
        EnsureSession();
        CheckChannel(channelId);
        ValidateUploadFile(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > MaximumFileBytes)
            throw new SynologyApiException(400, "파일 하나의 크기는 10MB 이하여야 합니다.");
        await SendAsync("SYNO.Chat.Post", "create", 5,
            new() { ["channel_id"] = channelId, ["type"] = "file", ["message"] = "",
                ["is_thread"] = false, ["thread_id"] = 0 }, true, ct, file);
    }
}
