using System.Net;
using System.Text;
using System.Text.Json;

internal sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly Queue<Step> _steps = new();
    public List<CapturedRequest> Requests { get; } = new();

    public ScriptedHttpHandler Expect(string api, string method, object response)
    {
        _steps.Enqueue(new Step(api, method, JsonSerializer.Serialize(response), HttpStatusCode.OK));
        return this;
    }

    public ScriptedHttpHandler ExpectRaw(string api, string method, string response, HttpStatusCode status = HttpStatusCode.OK)
    {
        _steps.Enqueue(new Step(api, method, response, status));
        return this;
    }

    public void AssertComplete()
    {
        if (_steps.Count != 0)
            throw new InvalidOperationException($"Expected {_steps.Count} more API calls.");
    }

    public ScriptedHttpHandler ExpectFile(byte[] bytes, string contentType = "application/octet-stream", long? declaredLength = null)
    {
        _steps.Enqueue(new Step("SYNO.Chat.Post.File", "get", "", HttpStatusCode.OK, bytes, contentType, declaredLength));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<string, string> fields;
        if (request.Content is MultipartFormDataContent multipart)
        {
            fields = new(StringComparer.Ordinal);
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                if (name == "file")
                {
                    fields["file_name"] = part.Headers.ContentDisposition.FileNameStar ?? part.Headers.ContentDisposition.FileName!.Trim('"');
                    fields["file_base64"] = Convert.ToBase64String(await part.ReadAsByteArrayAsync(cancellationToken));
                }
                else fields.Add(name, await part.ReadAsStringAsync(cancellationToken));
            }
        }
        else fields = ReadForm(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        var captured = new CapturedRequest(request.Method, request.RequestUri!, fields);
        Requests.Add(captured);
        if (_steps.Count == 0)
            throw new InvalidOperationException("Unexpected API request; no network call was made.");
        var next = _steps.Dequeue();
        if (!fields.TryGetValue("api", out var api) || api != next.Api ||
            !fields.TryGetValue("method", out var method) || method != next.Method)
            throw new InvalidOperationException($"Expected API {next.Api}/{next.Method}; request routing differed.");
        HttpContent content = next.Bytes is null ? new StringContent(next.Body, Encoding.UTF8, "application/json") : new ByteArrayContent(next.Bytes);
        if (next.Bytes is not null)
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(next.ContentType);
            content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
            if (next.DeclaredLength is long size) content.Headers.ContentLength = size;
        }
        return new HttpResponseMessage(next.Status)
        {
            RequestMessage = request,
            Content = content
        };
    }

    private static Dictionary<string, string> ReadForm(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length == 2 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
            if (!fields.TryAdd(name, value))
                throw new InvalidOperationException("Duplicate API form field.");
        }
        return fields;
    }

    private sealed record Step(string Api, string Method, string Body, HttpStatusCode Status,
        byte[]? Bytes = null, string ContentType = "application/octet-stream", long? DeclaredLength = null);
}

internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Fields);
