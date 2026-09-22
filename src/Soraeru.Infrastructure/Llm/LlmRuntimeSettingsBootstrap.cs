using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Llm;

/// <summary>
/// One-time migration: copy Llm env/appsettings into SQLite when DB row is incomplete,
/// and seed system prompts from <see cref="WordAnalysisPrompts"/> when missing.
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
        var systemPrompt = FirstNonEmpty(existing?.SystemPrompt, WordAnalysisPrompts.System);
        var meaningPrompt = FirstNonEmpty(
            existing?.MeaningReadingOnlySystemPrompt,
            WordAnalysisPrompts.MeaningReadingOnlySystem);

        var credentialsComplete = !string.IsNullOrWhiteSpace(existing?.ApiKey)
            && !string.IsNullOrWhiteSpace(existing?.Model)
            && !string.IsNullOrWhiteSpace(existing?.BaseUrl);
        var promptsComplete = !string.IsNullOrWhiteSpace(existing?.SystemPrompt)
            && !string.IsNullOrWhiteSpace(existing?.MeaningReadingOnlySystemPrompt);
        if (credentialsComplete && promptsComplete)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(model)
            || string.IsNullOrWhiteSpace(baseUrl))
        {
            // Still seed prompts alone when credentials already in DB but prompts missing.
            if (credentialsComplete && !promptsComplete
                && !string.IsNullOrWhiteSpace(systemPrompt)
                && !string.IsNullOrWhiteSpace(meaningPrompt))
            {
                await runtime.UpsertAsync(
                    new LlmRuntimeSettingsRecord(
                        existing!.ApiKey,
                        existing.Model,
                        existing.BaseUrl,
                        DateTimeOffset.UtcNow,
                        UpdatedByEmail: "bootstrap:prompts→sqlite",
                        systemPrompt,
                        meaningPrompt),
                    cancellationToken);
                logger?.LogInformation(
                    "Seeded LlmRuntimeSettings system prompts from embedded WordAnalysisPrompts into SQLite.");
            }
            else
            {
                logger?.LogWarning(
                    "LLM SQLite settings incomplete and config cannot seed ApiKey/Model/BaseUrl. Configure via Curator LLM 設定.");
            }

            return;
        }

        var alreadySame = existing is not null
            && string.Equals(existing.ApiKey?.Trim(), apiKey.Trim(), StringComparison.Ordinal)
            && string.Equals(existing.Model?.Trim(), model.Trim(), StringComparison.Ordinal)
            && string.Equals(
                existing.BaseUrl?.Trim().TrimEnd('/'),
                baseUrl.Trim().TrimEnd('/'),
                StringComparison.Ordinal)
            && string.Equals(existing.SystemPrompt?.Trim(), systemPrompt!.Trim(), StringComparison.Ordinal)
            && string.Equals(
                existing.MeaningReadingOnlySystemPrompt?.Trim(),
                meaningPrompt!.Trim(),
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
                UpdatedByEmail: "bootstrap:config→sqlite",
                systemPrompt!.Trim(),
                meaningPrompt!.Trim()),
            cancellationToken);

        logger?.LogInformation(
            "Seeded LlmRuntimeSettings from config/prompts into SQLite (one-time). Prefer Curator LLM 設定 afterwards; Railway Llm__* can be removed.");
    }

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred
        : !string.IsNullOrWhiteSpace(fallback) ? fallback
        : null;
}
