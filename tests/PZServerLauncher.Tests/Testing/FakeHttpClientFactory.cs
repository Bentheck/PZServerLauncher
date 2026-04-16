using System.Net.Http;

namespace PZServerLauncher.Tests.Testing;

internal sealed class FakeHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => httpClient;
}
