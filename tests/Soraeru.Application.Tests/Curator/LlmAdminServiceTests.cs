using NSubstitute;
using Shouldly;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Llm;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;
using Soraeru.Application.Curator;

namespace Soraeru.Application.Tests.Curator;

public sealed class LlmAdminServiceTests
{
    private static readonly Guid CuratorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LearnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDeveloperAccountPolicy _policy = Substitute.For<IDeveloperAccountPolicy>();
    private readonly ILlmSettingsResolver _resolver = Substitute.For<ILlmSettingsResolver>();
    private readonly ILlmRuntimeSettingsRepository _runtime = Substitute.For<ILlmRuntimeSettingsRepository>();
    private readonly ILlmUsageRepository _usage = Substitute.For<ILlmUsageRepository>();
    private readonly LlmAdminService _sut;

    public LlmAdminServiceTests()
    {
        _sut = new LlmAdminService(_users, _policy, _resolver, _runtime, _usage);
        _users.FindByIdAsync(CuratorId, Arg.Any<CancellationToken>())
            .Returns(new UserRecord(
                CuratorId, "curator@example.com", null, null, "C", "Free", 20, "zhuyin",
                true, false, DateTimeOffset.UtcNow));
        _users.FindByIdAsync(LearnerId, Arg.Any<CancellationToken>())
            .Returns(new UserRecord(
                LearnerId, "learner@example.com", null, null, "L", "Free", 20, "zhuyin",
                false, false, DateTimeOffset.UtcNow));
        _policy.IsDeveloperEmail("curator@example.com").Returns(true);
        _policy.IsDeveloperEmail("learner@example.com").Returns(false);

        _resolver.ResolveAsync(Arg.Any<CancellationToken>()).Returns(
            new LlmEffectiveSettings(
                "secret-key-abcdef",
                "gemini-3.6-flash",
                "https://example.com/v1",
                60,
                false,
                false,
                false,
                "gemini-3.6-flash",
                "https://example.com/v1"));
    }

    [Fact]
    public async Task GetSettings_non_curator_forbidden()
    {
        var result = await _sut.GetSettingsAsync(LearnerId);
        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task GetSettings_masks_api_key()
    {
        var result = await _sut.GetSettingsAsync(CuratorId);
        result.IsSuccess.ShouldBeTrue();
        result.Value!.ApiKeyMasked.ShouldBe(ApiKeyMask.Mask("secret-key-abcdef"));
        result.Value.ApiKeyMasked.ShouldNotContain("secret-key-abcdef");
    }

    [Fact]
    public async Task UpdateSettings_writes_runtime_override()
    {
        LlmRuntimeSettingsRecord? saved = null;
        _runtime.UpsertAsync(Arg.Any<LlmRuntimeSettingsRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<LlmRuntimeSettingsRecord>(0);
                return saved;
            });
        _runtime.GetAsync(Arg.Any<CancellationToken>()).Returns((LlmRuntimeSettingsRecord?)null);

        var result = await _sut.UpdateSettingsAsync(
            new UpdateLlmSettingsCommand(
                CuratorId,
                ApiKey: "new-api-key-value-12345",
                ClearApiKey: false,
                Model: "gemini-2.5-flash",
                BaseUrl: "https://openrouter.ai/api/v1"));

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.ApiKey.ShouldBe("new-api-key-value-12345");
        saved.Model.ShouldBe("gemini-2.5-flash");
        saved.BaseUrl.ShouldBe("https://openrouter.ai/api/v1");
    }
}
