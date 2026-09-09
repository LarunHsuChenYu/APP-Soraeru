using System.Net;
using System.Text;
using Shouldly;
using Soraeru.Curator.Api;

namespace Soraeru.Curator.Tests.Api;

public sealed class CuratorApiClientTests
{
    [Fact]
    public async Task ListAsync_forbidden_maps_error()
    {
        var handler = new StubHandler(
            HttpStatusCode.Forbidden,
            """{"code":"FORBIDDEN","message":"僅策展授權帳號可管理已驗證空耳。"}""");
        var client = new CuratorApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

        var result = await client.ListAsync("token", null, null);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe("FORBIDDEN");
        result.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task CreateAsync_posts_body_and_parses_dto()
    {
        var handler = new StubHandler(
            HttpStatusCode.Created,
            """
            {
              "id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "language":"en",
              "sourceText":"hello",
              "normalizedSource":"hello",
              "displayText":"哈囉核定",
              "notationText":"ㄏㄚ",
              "explanation":"策展",
              "isEnabled":true,
              "createdAtUtc":"2026-09-04T00:00:00Z",
              "updatedAtUtc":"2026-09-04T00:00:00Z"
            }
            """);
        var client = new CuratorApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test/") });

        var result = await client.CreateAsync(
            "token",
            new CreateVerifiedMnemonicRequest("en", "hello", "哈囉核定", "ㄏㄚ", "策展", true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.DisplayText.ShouldBe("哈囉核定");
        handler.LastMethod.ShouldBe(HttpMethod.Post);
        handler.LastPath.ShouldBe("/api/v1/curator/verified-mnemonics");
        handler.LastAuth.ShouldBe("Bearer token");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastAuth { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPath = request.RequestUri?.PathAndQuery;
            LastAuth = request.Headers.Authorization?.ToString();
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
