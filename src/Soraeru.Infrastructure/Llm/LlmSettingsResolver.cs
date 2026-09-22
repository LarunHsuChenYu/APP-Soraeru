using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Llm;

/// <summary>
/// Resolves ApiKey / Model / BaseUrl from SQLite only (ADR-0013).
/// System prompts: SQLite when set; otherwise embedded <see cref="WordAnalysisPrompts"/> defaults.
/// Timeout still comes from LlmOptions (appsettings / env).
/// </summary>
public sealed class LlmSettingsResolver : ILlmSettingsResolver
{
    private readonly IOptions<LlmOptions> _options;
    private readonly ILlmRuntimeSettingsRepository _runtime;

    public LlmSettingsResolver(
        IOptions<LlmOptions> options,
        ILlmRuntimeSettingsRepository runtime)
    {
        _options = options;
        _runtime = runtime;
    }

    public async Task<LlmEffectiveSettings> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var cfg = _options.Value;
        var db = await _runtime.GetAsync(cancellationToken);

        var apiKey = db?.ApiKey?.Trim() ?? "";
        var model = db?.Model?.Trim() ?? "";
        var baseUrl = (db?.BaseUrl ?? "").Trim().TrimEnd('/');

        var apiKeyFromDb = !string.IsNullOrWhiteSpace(apiKey);
        var modelFromDb = !string.IsNullOrWhiteSpace(model);
        var baseUrlFromDb = !string.IsNullOrWhiteSpace(baseUrl);

        var embeddedSystem = WordAnalysisPrompts.System;
        var embeddedMeaning = WordAnalysisPrompts.MeaningReadingOnlySystem;
        var systemFromDb = !string.IsNullOrWhiteSpace(db?.SystemPrompt);
        var meaningFromDb = !string.IsNullOrWhiteSpace(db?.MeaningReadingOnlySystemPrompt);
        var systemPrompt = systemFromDb ? db!.SystemPrompt!.Trim() : embeddedSystem;
        var meaningPrompt = meaningFromDb ? db!.MeaningReadingOnlySystemPrompt!.Trim() : embeddedMeaning;

        return new LlmEffectiveSettings(
            ApiKey: apiKey,
            Model: model,
            BaseUrl: baseUrl,
            TimeoutSeconds: Math.Clamp(cfg.TimeoutSeconds, 10, 180),
            ApiKeyFromDatabase: apiKeyFromDb,
            ModelFromDatabase: modelFromDb,
            BaseUrlFromDatabase: baseUrlFromDb,
            ConfigModel: model,
            ConfigBaseUrl: baseUrl,
            SystemPrompt: systemPrompt,
            MeaningReadingOnlySystemPrompt: meaningPrompt,
            SystemPromptFromDatabase: systemFromDb,
            MeaningReadingOnlySystemPromptFromDatabase: meaningFromDb,
            ConfigSystemPrompt: embeddedSystem,
            ConfigMeaningReadingOnlySystemPrompt: embeddedMeaning);
    }
}
