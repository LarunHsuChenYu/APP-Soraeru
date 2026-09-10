using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Llm;

/// <summary>
/// Resolves ApiKey / Model / BaseUrl from SQLite only (ADR-0013 amended).
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

        // #region agent log
        try
        {
            var line = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = "7ea34e",
                hypothesisId = "H-DB-ONLY",
                location = "LlmSettingsResolver.cs:ResolveAsync",
                message = "Resolved LLM settings from SQLite only",
                data = new
                {
                    apiKeyFromDb,
                    modelFromDb,
                    baseUrlFromDb,
                    model,
                    baseUrl,
                    apiKeyMasked = apiKey.Length <= 8
                        ? "(short-or-empty)"
                        : $"{apiKey[..4]}…{apiKey[^4..]}",
                    readsEnvForKeyModelBaseUrl = false
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId = "post-fix"
            }) + Environment.NewLine;
            File.AppendAllText(@"d:\VS\Soraeru\debug-7ea34e.log", line);
        }
        catch
        {
            // ignore
        }
        // #endregion

        return new LlmEffectiveSettings(
            ApiKey: apiKey,
            Model: model,
            BaseUrl: baseUrl,
            TimeoutSeconds: Math.Clamp(cfg.TimeoutSeconds, 10, 180),
            ApiKeyFromDatabase: apiKeyFromDb,
            ModelFromDatabase: modelFromDb,
            BaseUrlFromDatabase: baseUrlFromDb,
            // Legacy fields: no longer mirror Railway env — surface DB values for Curator.
            ConfigModel: model,
            ConfigBaseUrl: baseUrl);
    }
}
