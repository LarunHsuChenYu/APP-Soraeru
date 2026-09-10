using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Soraeru.Curator;
using Soraeru.Curator.Api;
using Soraeru.Curator.Components;
using Soraeru.Curator.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (int.TryParse(port, out var railwayPort) && railwayPort is > 0 and <= 65535)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}
else if (!string.IsNullOrWhiteSpace(port))
{
    throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
}

builder.Services.Configure<CuratorOptions>(builder.Configuration.GetSection(CuratorOptions.SectionName));
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("Soraeru.Curator");

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (builder.Environment.IsProduction() && !string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var keysDirectory = new DirectoryInfo(dataProtectionKeysPath);
    keysDirectory.Create();
    dataProtection.PersistKeysToFileSystem(keysDirectory);
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// AddRazorComponents registers antiforgery services. Configure the same options
// afterwards so Razor Components uses this app-specific, versioned cookie.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsProduction()
        ? "__Host-SoraeruCurator.Antiforgery.v2"
        : "SoraeruCurator.Antiforgery.v2";
    options.Cookie.Path = "/";
    options.Cookie.Domain = null;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddScoped<CuratorSessionState>();
builder.Services.AddHttpClient<ICuratorApiClient, CuratorApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CuratorOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.ApiBaseUrl)
        ? "http://localhost:5080"
        : options.ApiBaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    await next();

    if (context.Request.Path == "/login"
        || context.Request.Path == "/_framework/blazor.web.js")
    {
        // #region agent log
        AgentDebugLog(new
        {
            sessionId = "1a7969",
            runId = "pre-fix",
            hypothesisId = "H1,H3,H4",
            location = "Program.cs:request-probe",
            message = "Curator request routing result",
            data = new
            {
                path = context.Request.Path.Value,
                method = context.Request.Method,
                status = context.Response.StatusCode,
                endpoint = context.GetEndpoint()?.DisplayName,
                contentType = context.Response.ContentType,
                commit = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA")
            },
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        // #endregion
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Soraeru.Curator" }));
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// #region agent log
var frameworkRoutes = ((IEndpointRouteBuilder)app).DataSources
    .SelectMany(source => source.Endpoints)
    .OfType<RouteEndpoint>()
    .Select(endpoint => endpoint.RoutePattern.RawText)
    .Where(route => route?.Contains("blazor.web", StringComparison.OrdinalIgnoreCase) == true)
    .Take(10)
    .ToArray();
var staticAssetManifests = Directory
    .EnumerateFiles(AppContext.BaseDirectory, "*staticwebassets*", SearchOption.TopDirectoryOnly)
    .Select(Path.GetFileName)
    .OrderBy(name => name)
    .ToArray();
AgentDebugLog(new
{
    sessionId = "1a7969",
    runId = "pre-fix",
    hypothesisId = "H1,H2,H4",
    location = "Program.cs:startup-probe",
    message = "Curator static asset runtime state",
    data = new
    {
        environment = app.Environment.EnvironmentName,
        contentRoot = app.Environment.ContentRootPath,
        webRoot = app.Environment.WebRootPath,
        baseDirectory = AppContext.BaseDirectory,
        commit = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA"),
        staticAssetManifests,
        frameworkRoutes,
        physicalBlazorScript = File.Exists(
            Path.Combine(app.Environment.WebRootPath ?? "", "_framework", "blazor.web.js"))
    },
    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
});
// #endregion

// #region agent log
var publishManifestPath = Path.Combine(
    AppContext.BaseDirectory,
    "Soraeru.Curator.staticwebassets.endpoints.json");
var publishManifestText = File.Exists(publishManifestPath)
    ? File.ReadAllText(publishManifestPath)
    : "";
AgentDebugLog(new
{
    sessionId = "1a7969",
    runId = "pre-fix-2",
    hypothesisId = "H5,H6",
    location = "Program.cs:publish-manifest-probe",
    message = "Curator publish manifest content state",
    data = new
    {
        commit = Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA"),
        manifestLength = publishManifestText.Length,
        manifestContainsBlazorWebRoute = publishManifestText.Contains(
            "\"Route\":\"_framework/blazor.web.js\"",
            StringComparison.Ordinal),
        physicalFrameworkDirectory = Directory.Exists(
            Path.Combine(app.Environment.WebRootPath ?? "", "_framework"))
    },
    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
});
// #endregion

app.Run();

static void AgentDebugLog(object payload)
{
    var json = JsonSerializer.Serialize(payload);
    Console.WriteLine($"AGENT_DEBUG {json}");

    try
    {
        // Production container runs as non-root; /app is not writable.
        File.AppendAllText(
            Path.Combine(Path.GetTempPath(), "debug-1a7969.log"),
            json + Environment.NewLine);
    }
    catch (Exception exception)
    {
        Console.WriteLine(
            $"AGENT_DEBUG_FILE_ERROR {exception.GetType().Name}: {exception.Message}");
    }
}

public partial class Program;
