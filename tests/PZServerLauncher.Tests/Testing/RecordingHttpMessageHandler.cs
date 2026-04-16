using System.Net.Http;

namespace PZServerLauncher.Tests.Testing;

internal sealed class RecordingHttpMessageHandler(
    Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public List<RecordedHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedHttpRequest(
            request.Method.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            body));

        return await responder(request, body, cancellationToken);
    }
}

internal sealed record RecordedHttpRequest(string Method, string Uri, string? Body);
