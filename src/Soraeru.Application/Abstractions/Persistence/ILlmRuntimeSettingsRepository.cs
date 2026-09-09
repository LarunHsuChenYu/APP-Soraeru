namespace Soraeru.Application.Abstractions.Persistence;

public sealed record LlmRuntimeSettingsRecord(
    string? ApiKey,
    string? Model,
    string? BaseUrl,
    DateTimeOffset UpdatedAtUtc,
    string? UpdatedByEmail);

public interface ILlmRuntimeSettingsRepository
{
    Task<LlmRuntimeSettingsRecord?> GetAsync(CancellationToken cancellationToken = default);

    Task<LlmRuntimeSettingsRecord> UpsertAsync(
        LlmRuntimeSettingsRecord record,
        CancellationToken cancellationToken = default);
}

public sealed record LlmUsageRecord(
    Guid Id,
    Guid? UserId,
    string FeatureType,
    string Model,
    string Provider,
    int? PromptTokens,
    int? CompletionTokens,
    int LatencyMs,
    bool Success,
    string? ErrorCode,
    decimal EstimatedCostNtd,
    DateTimeOffset CreatedAtUtc);

public interface ILlmUsageRepository
{
    Task AddAsync(LlmUsageRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmUsageRecord>> ListRecentAsync(
        int take,
        string? featureType,
        CancellationToken cancellationToken = default);

    Task<(int Count, long PromptTokens, long CompletionTokens, decimal EstimatedCostNtd)> SummarizeSinceAsync(
        DateTimeOffset sinceUtc,
        string? featureType,
        CancellationToken cancellationToken = default);
}
