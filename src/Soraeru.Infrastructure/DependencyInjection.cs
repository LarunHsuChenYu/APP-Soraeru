using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Auth;
using Soraeru.Infrastructure.Email;
using Soraeru.Infrastructure.Llm;
using Soraeru.Infrastructure.Persistence;

namespace Soraeru.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleAuthOptions>(options =>
        {
            var clientIds = configuration.GetSection($"{GoogleAuthOptions.SectionName}:ClientIds").Get<string[]>()
                ?? [];
            options.ClientIds = clientIds
                .Where(id => !string.IsNullOrWhiteSpace(id)
                    && !id.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
                .Select(id => id.Trim())
                .ToList();
        });
        services.Configure<DeveloperAccountsOptions>(options =>
        {
            var emails = configuration.GetSection(DeveloperAccountsOptions.SectionName).Get<string[]>()
                ?? configuration.GetSection($"{DeveloperAccountsOptions.SectionName}:Emails").Get<string[]>()
                ?? [];
            options.Emails = emails.ToList();
        });
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));

        var provider = configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddSingleton<IUsageRepository, InMemoryUsageRepository>();
            services.AddSingleton<IWordCardRepository, InMemoryWordCardRepository>();
            services.AddSingleton<IVerifiedMnemonicRepository, InMemoryVerifiedMnemonicRepository>();
            services.AddSingleton<IWordRegenerationRepository, InMemoryWordRegenerationRepository>();
            services.AddSingleton<ILlmRuntimeSettingsRepository, InMemoryLlmRuntimeSettingsRepository>();
            services.AddSingleton<ILlmUsageRepository, InMemoryLlmUsageRepository>();
        }
        else
        {
            var connectionString = configuration.GetConnectionString("Default")
                ?? "Data Source=soraeru.db";

            services.AddDbContext<SoraeruDbContext>(options => options.UseSqlite(connectionString));
            services.AddScoped<IUserRepository, EfUserRepository>();
            services.AddScoped<IUsageRepository, EfUsageRepository>();
            services.AddScoped<IWordCardRepository, EfWordCardRepository>();
            services.AddScoped<IVerifiedMnemonicRepository, EfVerifiedMnemonicRepository>();
            services.AddScoped<IWordRegenerationRepository, EfWordRegenerationRepository>();
            services.AddScoped<ILlmRuntimeSettingsRepository, EfLlmRuntimeSettingsRepository>();
            services.AddScoped<ILlmUsageRepository, EfLlmUsageRepository>();
        }

        services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();
        services.AddSingleton<IPasswordResetTokenStore, InMemoryPasswordResetTokenStore>();
        services.AddSingleton<IDeveloperAccountPolicy, ConfigDeveloperAccountPolicy>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleJsonWebSignatureIdTokenValidator>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<IAnalysisResultCache, MemoryAnalysisResultCache>();
        services.AddScoped<ILlmSettingsResolver, LlmSettingsResolver>();

        services.AddHttpClient<IWordAnalysisAgent, OpenAiCompatibleWordAnalysisAgent>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            OpenAiCompatibleWordAnalysisAgentExtensions.ConfigureHttpClient(client, options.TimeoutSeconds);
        });

        return services;
    }
}
