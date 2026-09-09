namespace Soraeru.Application.Abstractions.Llm;

public sealed record LlmEffectiveSettings(
    string ApiKey,
    string Model,
    string BaseUrl,
    int TimeoutSeconds,
    bool ApiKeyFromDatabase,
    bool ModelFromDatabase,
    bool BaseUrlFromDatabase,
    string ConfigModel,
    string ConfigBaseUrl);

public interface ILlmSettingsResolver
{
    Task<LlmEffectiveSettings> ResolveAsync(CancellationToken cancellationToken = default);
}
