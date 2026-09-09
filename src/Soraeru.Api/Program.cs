using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Soraeru.Api.Endpoints;
using Soraeru.Api.Hosting;
using Soraeru.Application;
using Soraeru.Infrastructure;
using Soraeru.Infrastructure.Auth;
using Soraeru.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var corsOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .ToArray();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod());
    });
}

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:SigningKey must be set (min 32 chars). Use appsettings.Development.json or User Secrets.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var provider = app.Configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
if (!string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    SqliteDataDirectory.EnsureExists(app.Configuration.GetConnectionString("Default"));
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SoraeruDbContext>();
    db.Database.Migrate();
}

if (corsOrigins.Length > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

// Root has no product page; avoid Chrome "HTTP ERROR 404" when opening the listen URL.
app.MapGet("/", () => Results.Redirect("/health"));
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Soraeru.Api" }));

app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapAnalyzeEndpoints();
app.MapNotebookEndpoints();
app.MapCuratorMnemonicEndpoints();
app.MapCuratorLlmAdminEndpoints();
app.MapCuratorUserAdminEndpoints();

app.Run();
