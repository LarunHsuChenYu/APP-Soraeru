using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Persistence;

namespace Soraeru.Infrastructure.Tests.Persistence;

public sealed class EfLlmUsageRepositoryTests
{
    [Fact]
    public async Task SummarizeSinceAsync_empty_table_returns_zeros_on_sqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soraeru-llm-usage-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = CreateDb(path);
            await db.Database.EnsureCreatedAsync();
            var repo = new EfLlmUsageRepository(db);

            var since = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var summary = await repo.SummarizeSinceAsync(since, featureType: null);

            summary.Count.ShouldBe(0);
            summary.PromptTokens.ShouldBe(0);
            summary.CompletionTokens.ShouldBe(0);
            summary.EstimatedCostNtd.ShouldBe(0m);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SummarizeSinceAsync_counts_only_rows_on_or_after_since()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soraeru-llm-usage-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = CreateDb(path);
            await db.Database.EnsureCreatedAsync();
            var repo = new EfLlmUsageRepository(db);

            var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            await repo.AddAsync(new LlmUsageRecord(
                Guid.NewGuid(), null, "text_analysis", "m", "p",
                10, 5, 100, true, null, 1.5m, today.AddHours(1)));
            await repo.AddAsync(new LlmUsageRecord(
                Guid.NewGuid(), null, "text_analysis", "m", "p",
                100, 50, 100, true, null, 9m, today.AddDays(-1)));

            var summary = await repo.SummarizeSinceAsync(today, "text_analysis");

            summary.Count.ShouldBe(1);
            summary.PromptTokens.ShouldBe(10);
            summary.CompletionTokens.ShouldBe(5);
            summary.EstimatedCostNtd.ShouldBe(1.5m);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static SoraeruDbContext CreateDb(string path)
    {
        var options = new DbContextOptionsBuilder<SoraeruDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new SoraeruDbContext(options);
    }
}
