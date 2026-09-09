using System.Collections.Concurrent;
using Soraeru.Application.Abstractions.Persistence;

namespace Soraeru.Infrastructure.Persistence;

public sealed class InMemoryLlmRuntimeSettingsRepository : ILlmRuntimeSettingsRepository
{
    private LlmRuntimeSettingsRecord? _row;

    public Task<LlmRuntimeSettingsRecord?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_row);

    public Task<LlmRuntimeSettingsRecord> UpsertAsync(
        LlmRuntimeSettingsRecord record,
        CancellationToken cancellationToken = default)
    {
        _row = record;
        return Task.FromResult(record);
    }
}

public sealed class InMemoryLlmUsageRepository : ILlmUsageRepository
{
    private readonly ConcurrentBag<LlmUsageRecord> _rows = [];

    public Task AddAsync(LlmUsageRecord record, CancellationToken cancellationToken = default)
    {
        _rows.Add(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LlmUsageRecord>> ListRecentAsync(
        int take,
        string? featureType,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<LlmUsageRecord> q = _rows;
        if (!string.IsNullOrWhiteSpace(featureType))
        {
            q = q.Where(x => x.FeatureType == featureType);
        }

        IReadOnlyList<LlmUsageRecord> list = q.OrderByDescending(x => x.CreatedAtUtc).Take(take).ToList();
        return Task.FromResult(list);
    }

    public Task<(int Count, long PromptTokens, long CompletionTokens, decimal EstimatedCostNtd)> SummarizeSinceAsync(
        DateTimeOffset sinceUtc,
        string? featureType,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<LlmUsageRecord> q = _rows.Where(x => x.CreatedAtUtc >= sinceUtc);
        if (!string.IsNullOrWhiteSpace(featureType))
        {
            q = q.Where(x => x.FeatureType == featureType);
        }

        var list = q.ToList();
        return Task.FromResult((
            list.Count,
            list.Sum(x => (long)(x.PromptTokens ?? 0)),
            list.Sum(x => (long)(x.CompletionTokens ?? 0)),
            list.Sum(x => x.EstimatedCostNtd)));
    }
}
