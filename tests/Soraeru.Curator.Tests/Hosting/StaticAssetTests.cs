using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace Soraeru.Curator.Tests.Hosting;

public sealed partial class StaticAssetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StaticAssetTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseStaticWebAssets();
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        _client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
    }

    [Fact]
    public async Task Production_serves_blazor_boot_script()
    {
        var response = await _client.GetAsync("/_framework/blazor.web.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/javascript");

        var content = await response.Content.ReadAsStringAsync();
        content.Length.ShouldBeGreaterThan(10_000);
        content.ShouldContain("Blazor");
    }

    [Fact]
    public async Task Production_serves_every_local_script_and_stylesheet_referenced_by_login()
    {
        var loginResponse = await _client.GetAsync("/login");

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await loginResponse.Content.ReadAsStringAsync();
        var assetPaths = AssetReferenceRegex()
            .Matches(html)
            .Select(match => match.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        assetPaths.ShouldNotBeEmpty();
        assetPaths.ShouldContain(path => path.Contains("blazor.web", StringComparison.Ordinal));

        foreach (var assetPath in assetPaths)
        {
            var assetResponse = await _client.GetAsync(new Uri(new Uri("https://localhost/login"), assetPath).PathAndQuery);
            assetResponse.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"Login references unavailable static asset '{assetPath}'.");
        }
    }

    [GeneratedRegex("<(?:script|link)\\b[^>]*\\b(?:src|href)=\"(?<path>(?!https?:|//|data:|#)[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AssetReferenceRegex();
}
