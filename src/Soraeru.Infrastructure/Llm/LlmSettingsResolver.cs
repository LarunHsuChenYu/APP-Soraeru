using Microsoft.Extensions.Options;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Llm;

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

        var apiKeyFromDb = !string.IsNullOrWhiteSpace(db?.ApiKey);
        var modelFromDb = !string.IsNullOrWhiteSpace(db?.Model);
        var baseUrlFromDb = !string.IsNullOrWhiteSpace(db?.BaseUrl);

        return new LlmEffectiveSettings(
            ApiKey: apiKeyFromDb ? db!.ApiKey!.Trim() : (cfg.ApiKey ?? ""),
            Model: modelFromDb ? db!.Model!.Trim() : (cfg.Model ?? ""),
            BaseUrl: (baseUrlFromDb ? db!.BaseUrl! : (cfg.BaseUrl ?? "")).Trim().TrimEnd('/'),
            TimeoutSeconds: Math.Clamp(cfg.TimeoutSeconds, 10, 180),
            ApiKeyFromDatabase: apiKeyFromDb,
            ModelFromDatabase: modelFromDb,
            BaseUrlFromDatabase: baseUrlFromDb,
            ConfigModel: cfg.Model ?? "",
            ConfigBaseUrl: (cfg.BaseUrl ?? "").Trim().TrimEnd('/'));
    }
}
