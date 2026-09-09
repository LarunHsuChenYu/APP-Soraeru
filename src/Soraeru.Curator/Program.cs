using Microsoft.Extensions.Options;
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.Run();

public partial class Program;
