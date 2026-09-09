using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Soraeru.Curator.Tests.Hosting;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        _client = _factory
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

    [Fact]
    public void Production_uses_stable_app_name_and_host_only_antiforgery_cookie()
    {
        var dataProtection = _factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
        var antiforgery = _factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        dataProtection.ApplicationDiscriminator.ShouldBe("Soraeru.Curator");
        antiforgery.Cookie.Name.ShouldBe("__Host-SoraeruCurator.Antiforgery.v2");
        antiforgery.Cookie.Path.ShouldBe("/");
        antiforgery.Cookie.Domain.ShouldBeNull();
        antiforgery.Cookie.HttpOnly.ShouldBeTrue();
        antiforgery.Cookie.SameSite.ShouldBe(SameSiteMode.Strict);
        antiforgery.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.Always);
    }

    [Fact]
    public void Development_http_uses_non_host_cookie_with_request_security()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var antiforgery = factory.Services.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        antiforgery.Cookie.Name.ShouldBe("SoraeruCurator.Antiforgery.v2");
        antiforgery.Cookie.SecurePolicy.ShouldBe(CookieSecurePolicy.SameAsRequest);
    }

    [Fact]
    public void Configured_data_protection_path_receives_key_ring()
    {
        var keysPath = Path.Combine(Path.GetTempPath(), $"soraeru-curator-keys-{Guid.NewGuid():N}");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Production");
                    builder.UseSetting("DataProtection:KeysPath", keysPath);
                });

            var provider = factory.Services.GetRequiredService<IDataProtectionProvider>();
            provider.CreateProtector("persistence-test").Protect("probe").ShouldNotBeNullOrWhiteSpace();

            Directory.Exists(keysPath).ShouldBeTrue();
            Directory.EnumerateFiles(keysPath, "key-*.xml").ShouldNotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(keysPath))
            {
                Directory.Delete(keysPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Get_login_ignores_unreadable_cookie_from_previous_name()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add(
            "Cookie",
            ".AspNetCore.Antiforgery.VyLW6ORzMgk=unreadable-token-from-retired-key-ring");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed record HealthResponse(string Status, string Service);
}
