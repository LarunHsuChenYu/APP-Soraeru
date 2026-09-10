using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Llm;

/// <summary>
/// One-time migration: copy Llm env/appsettings into SQLite when DB row is incomplete.
/// After this, request-time resolution must not read ApiKey/Model/BaseUrl from env.
/// </summary>
public static class LlmRuntimeSettingsBootstrap
{
    public static async Task EnsureSeededFromConfigAsync(
        ILlmRuntimeSettingsRepository runtime,
        LlmOptions options,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await runtime.GetAsync(cancellationToken);
        var apiKey = FirstNonEmpty(existing?.ApiKey, options.ApiKey);
        var model = FirstNonEmpty(existing?.Model, options.Model);
        var baseUrl = FirstNonEmpty(existing?.BaseUrl, options.BaseUrl);

        var dbComplete = !string.IsNullOrWhiteSpace(existing?.ApiKey)
            && !string.IsNullOrWhiteSpace(existing?.Model)
            && !string.IsNullOrWhiteSpace(existing?.BaseUrl);
        if (dbComplete)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(model)
            || string.IsNullOrWhiteSpace(baseUrl))
        {
            logger?.LogWarning(
                "LLM SQLite settings incomplete and config cannot seed ApiKey/Model/BaseUrl. Configure via Curator LLM 設定.");
            return;
        }

        var alreadySame = existing is not null
            && string.Equals(existing.ApiKey?.Trim(), apiKey.Trim(), StringComparison.Ordinal)
            && string.Equals(existing.Model?.Trim(), model.Trim(), StringComparison.Ordinal)
            && string.Equals(
                existing.BaseUrl?.Trim().TrimEnd('/'),
                baseUrl.Trim().TrimEnd('/'),
                StringComparison.Ordinal);
        if (alreadySame)
        {
            return;
        }

        await runtime.UpsertAsync(
            new LlmRuntimeSettingsRecord(
                apiKey.Trim(),
                model.Trim(),
                baseUrl.Trim().TrimEnd('/'),
                DateTimeOffset.UtcNow,
                UpdatedByEmail: "bootstrap:config→sqlite"),
            cancellationToken);

        logger?.LogInformation(
            "Seeded LlmRuntimeSettings from config into SQLite (one-time). Prefer Curator LLM 設定 afterwards; Railway Llm__* can be removed.");
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred
        : !string.IsNullOrWhiteSpace(fallback) ? fallback
        : null;
}
