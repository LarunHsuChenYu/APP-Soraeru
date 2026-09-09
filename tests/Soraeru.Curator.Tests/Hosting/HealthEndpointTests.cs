using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Soraeru.Curator.Tests.Hosting;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Get_health_over_http_in_production_returns_curator_status()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        health.ShouldNotBeNull();
        health.Status.ShouldBe("ok");
        health.Service.ShouldBe("Soraeru.Curator");
    }

    [Fact]
    public async Task Get_root_behind_https_proxy_redirects_to_https_login()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location.ShouldBe(new Uri("https://localhost/login"));
    }

    private sealed record HealthResponse(string Status, string Service);
}
