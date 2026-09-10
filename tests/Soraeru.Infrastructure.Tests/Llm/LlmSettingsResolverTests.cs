using Microsoft.Extensions.Options;
using Shouldly;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Llm;

namespace Soraeru.Infrastructure.Tests.Llm;

public sealed class LlmSettingsResolverTests
{
    [Fact]
    public async Task Resolve_uses_sqlite_only_even_when_options_differ()
    {
        var runtime = new StubRuntime(
            new LlmRuntimeSettingsRecord(
                "sk-db-key-abcdefghijklmnop",
                "db-model",
                "https://db.example/v1",
                DateTimeOffset.UtcNow,
                "curator@example.com"));
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-env-should-not-be-used-zzzz",
            Model = "env-model",
            BaseUrl = "https://env.example/v1",
            TimeoutSeconds = 45
        });

        var sut = new LlmSettingsResolver(options, runtime);
        var effective = await sut.ResolveAsync();

        effective.ApiKey.ShouldBe("sk-db-key-abcdefghijklmnop");
        effective.Model.ShouldBe("db-model");
        effective.BaseUrl.ShouldBe("https://db.example/v1");
        effective.ApiKeyFromDatabase.ShouldBeTrue();
        effective.ModelFromDatabase.ShouldBeTrue();
        effective.BaseUrlFromDatabase.ShouldBeTrue();
        effective.ConfigModel.ShouldBe("db-model");
        effective.ConfigBaseUrl.ShouldBe("https://db.example/v1");
        effective.TimeoutSeconds.ShouldBe(45);
    }

    [Fact]
    public async Task Resolve_does_not_fall_back_to_env_when_db_empty()
    {
        var runtime = new StubRuntime(null);
        var options = Options.Create(new LlmOptions
        {
            ApiKey = "sk-env-only-key-xxxxxxxxxxxx",
            Model = "env-model",
            BaseUrl = "https://env.example/v1"
        });

        var sut = new LlmSettingsResolver(options, runtime);
        var effective = await sut.ResolveAsync();

        effective.ApiKey.ShouldBe("");
        effective.Model.ShouldBe("");
        effective.BaseUrl.ShouldBe("");
        effective.ApiKeyFromDatabase.ShouldBeFalse();
    }

    private sealed class StubRuntime(LlmRuntimeSettingsRecord? row) : ILlmRuntimeSettingsRepository
    {
        public Task<LlmRuntimeSettingsRecord?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(row);

        public Task<LlmRuntimeSettingsRecord> UpsertAsync(
            LlmRuntimeSettingsRecord record,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(record);
    }
}
