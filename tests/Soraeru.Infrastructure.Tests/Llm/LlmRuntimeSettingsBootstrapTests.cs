using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Llm;

namespace Soraeru.Infrastructure.Tests.Llm;

public sealed class LlmRuntimeSettingsBootstrapTests
{
    [Fact]
    public async Task Seeds_from_config_when_db_incomplete()
    {
        var runtime = new CapturingRuntime(null);
        var options = new LlmOptions
        {
            ApiKey = "sk-seed-key-1234567890abcd",
            Model = "seed-model",
            BaseUrl = "https://seed.example/v1/"
        };

        await LlmRuntimeSettingsBootstrap.EnsureSeededFromConfigAsync(
            runtime, options, NullLogger.Instance);

        runtime.Saved.ShouldNotBeNull();
        runtime.Saved!.ApiKey.ShouldBe("sk-seed-key-1234567890abcd");
        runtime.Saved.Model.ShouldBe("seed-model");
        runtime.Saved.BaseUrl.ShouldBe("https://seed.example/v1");
    }

    [Fact]
    public async Task Does_not_overwrite_complete_db_row()
    {
        var existing = new LlmRuntimeSettingsRecord(
            "sk-db-already-set-xxxxxxxxxxxx",
            "db-model",
            "https://db.example/v1",
            DateTimeOffset.UtcNow,
            "a@b.c");
        var runtime = new CapturingRuntime(existing);
        var options = new LlmOptions
        {
            ApiKey = "sk-env-should-not-overwrite",
            Model = "env-model",
            BaseUrl = "https://env.example/v1"
        };

        await LlmRuntimeSettingsBootstrap.EnsureSeededFromConfigAsync(
            runtime, options, NullLogger.Instance);

        runtime.Saved.ShouldBeNull();
    }

    private sealed class CapturingRuntime(LlmRuntimeSettingsRecord? existing) : ILlmRuntimeSettingsRepository
    {
        public LlmRuntimeSettingsRecord? Saved { get; private set; }

        public Task<LlmRuntimeSettingsRecord?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(existing);

        public Task<LlmRuntimeSettingsRecord> UpsertAsync(
            LlmRuntimeSettingsRecord record,
            CancellationToken cancellationToken = default)
        {
            Saved = record;
            return Task.FromResult(record);
        }
    }
}
