using Microsoft.EntityFrameworkCore;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Persistence.Entities;

namespace Soraeru.Infrastructure.Persistence;

public sealed class EfLlmRuntimeSettingsRepository : ILlmRuntimeSettingsRepository
{
    private readonly SoraeruDbContext _db;

    public EfLlmRuntimeSettingsRepository(SoraeruDbContext db) => _db = db;

    public async Task<LlmRuntimeSettingsRecord?> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.LlmRuntimeSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    public async Task<LlmRuntimeSettingsRecord> UpsertAsync(
        LlmRuntimeSettingsRecord record,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.LlmRuntimeSettings.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (row is null)
        {
            row = new LlmRuntimeSettingsEntity { Id = 1 };
            _db.LlmRuntimeSettings.Add(row);
        }

        row.ApiKey = record.ApiKey;
        row.Model = record.Model;
        row.BaseUrl = record.BaseUrl;
        row.UpdatedAt = record.UpdatedAtUtc;
        row.UpdatedByEmail = record.UpdatedByEmail;
        await _db.SaveChangesAsync(cancellationToken);
        return ToRecord(row);
    }

    private static LlmRuntimeSettingsRecord ToRecord(LlmRuntimeSettingsEntity row) =>
        new(row.ApiKey, row.Model, row.BaseUrl, row.UpdatedAt, row.UpdatedByEmail);
}

public sealed class EfLlmUsageRepository : ILlmUsageRepository
{
    private readonly SoraeruDbContext _db;

    public EfLlmUsageRepository(SoraeruDbContext db) => _db = db;

    public async Task AddAsync(LlmUsageRecord record, CancellationToken cancellationToken = default)
    {
        _db.LlmUsages.Add(new LlmUsageEntity
        {
            Id = record.Id,
            UserId = record.UserId,
            FeatureType = record.FeatureType,
            Model = record.Model,
            Provider = record.Provider,
            PromptTokens = record.PromptTokens,
            CompletionTokens = record.CompletionTokens,
            LatencyMs = record.LatencyMs,
            Success = record.Success,
            ErrorCode = record.ErrorCode,
            EstimatedCostNtd = record.EstimatedCostNtd,
            CreatedAt = record.CreatedAtUtc
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LlmUsageRecord>> ListRecentAsync(
        int take,
        string? featureType,
        CancellationToken cancellationToken = default)
    {
        var q = _db.LlmUsages.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(featureType))
        {
            q = q.Where(x => x.FeatureType == featureType);
        }

        var rows = await q.OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<(int Count, long PromptTokens, long CompletionTokens, decimal EstimatedCostNtd)> SummarizeSinceAsync(
        DateTimeOffset sinceUtc,
        string? featureType,
        CancellationToken cancellationToken = default)
    {
        var q = _db.LlmUsages.AsNoTracking().Where(x => x.CreatedAt >= sinceUtc);
        if (!string.IsNullOrWhiteSpace(featureType))
        {
            q = q.Where(x => x.FeatureType == featureType);
        }

        var count = await q.CountAsync(cancellationToken);
        var prompt = await q.SumAsync(x => (long)(x.PromptTokens ?? 0), cancellationToken);
        var completion = await q.SumAsync(x => (long)(x.CompletionTokens ?? 0), cancellationToken);
        var cost = await q.SumAsync(x => x.EstimatedCostNtd, cancellationToken);
        return (count, prompt, completion, cost);
    }

    private static LlmUsageRecord ToRecord(LlmUsageEntity x) =>
        new(
            x.Id,
            x.UserId,
            x.FeatureType,
            x.Model,
            x.Provider,
            x.PromptTokens,
            x.CompletionTokens,
            x.LatencyMs,
            x.Success,
            x.ErrorCode,
            x.EstimatedCostNtd,
            x.CreatedAt);
}
