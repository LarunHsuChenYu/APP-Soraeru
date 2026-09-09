using NSubstitute;
using Shouldly;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;
using Soraeru.Application.Curator;

namespace Soraeru.Application.Tests.Curator;

public sealed class CuratorUserAdminServiceTests
{
    private static readonly Guid CuratorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LearnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TargetId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IDeveloperAccountPolicy _policy = Substitute.For<IDeveloperAccountPolicy>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly CuratorUserAdminService _sut;

    public CuratorUserAdminServiceTests()
    {
        _sut = new CuratorUserAdminService(_users, _policy, _hasher);
        _users.FindByIdAsync(CuratorId, Arg.Any<CancellationToken>())
            .Returns(new UserRecord(
                CuratorId, "curator@example.com", "hash", null, "C", "Free",
                AppConstants.UnlimitedDailyQuota, "zhuyin", true, true, DateTimeOffset.UtcNow));
        _users.FindByIdAsync(LearnerId, Arg.Any<CancellationToken>())
            .Returns(new UserRecord(
                LearnerId, "learner@example.com", "hash", null, "L", "Free",
                AppConstants.FreeDailyQuota, "zhuyin", false, false, DateTimeOffset.UtcNow));
        _policy.IsDeveloperEmail("curator@example.com").Returns(true);
        _policy.IsDeveloperEmail("learner@example.com").Returns(false);
        _policy.IsDeveloperEmail("target@example.com").Returns(false);
    }

    [Fact]
    public async Task ListUsers_non_curator_forbidden()
    {
        var result = await _sut.ListUsersAsync(LearnerId);
        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe("FORBIDDEN");
    }

    [Fact]
    public async Task ListUsers_returns_accounts_without_password_hash()
    {
        var created = DateTimeOffset.Parse("2026-01-15T10:00:00Z");
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new UserRecord(
                TargetId, "target@example.com", "SECRET_HASH", null, "T", "Free",
                AppConstants.FreeDailyQuota, "zhuyin", false, true, created)
        ]);

        var result = await _sut.ListUsersAsync(CuratorId);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Count.ShouldBe(1);
        var row = result.Value[0];
        row.Id.ShouldBe(TargetId);
        row.Email.ShouldBe("target@example.com");
        row.IsDeveloper.ShouldBeFalse();
        row.DailyQuota.ShouldBe(AppConstants.FreeDailyQuota);
        row.OnboardingCompleted.ShouldBeTrue();
        row.CreatedAtUtc.ShouldBe(created);
        row.HasPassword.ShouldBeTrue();
    }

    [Fact]
    public async Task ListUsers_db_developer_without_allowlist_may_act_as_curator()
    {
        var dbDevId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        _users.FindByIdAsync(dbDevId, Arg.Any<CancellationToken>())
            .Returns(new UserRecord(
                dbDevId, "dbdev@example.com", "hash", null, "D", "Free",
                AppConstants.UnlimitedDailyQuota, "zhuyin", true, true, DateTimeOffset.UtcNow));
        _policy.IsDeveloperEmail("dbdev@example.com").Returns(false);
        _users.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.ListUsersAsync(dbDevId);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SetDeveloper_true_sets_unlimited_quota()
    {
        var target = new UserRecord(
            TargetId, "target@example.com", "hash", null, "T", "Free",
            AppConstants.FreeDailyQuota, "zhuyin", false, false, DateTimeOffset.UtcNow);
        _users.FindByIdAsync(TargetId, Arg.Any<CancellationToken>()).Returns(target);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.SetDeveloperAsync(CuratorId, TargetId, isDeveloper: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsDeveloper.ShouldBeTrue();
        result.Value.DailyQuota.ShouldBe(AppConstants.UnlimitedDailyQuota);
        saved.ShouldNotBeNull();
        saved!.IsDeveloper.ShouldBeTrue();
        saved.DailyQuota.ShouldBe(AppConstants.UnlimitedDailyQuota);
    }

    [Fact]
    public async Task SetDeveloper_false_sets_free_quota()
    {
        var target = new UserRecord(
            TargetId, "target@example.com", "hash", null, "T", "Free",
            AppConstants.UnlimitedDailyQuota, "zhuyin", true, false, DateTimeOffset.UtcNow);
        _users.FindByIdAsync(TargetId, Arg.Any<CancellationToken>()).Returns(target);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.SetDeveloperAsync(CuratorId, TargetId, isDeveloper: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsDeveloper.ShouldBeFalse();
        result.Value.DailyQuota.ShouldBe(AppConstants.FreeDailyQuota);
        saved!.IsDeveloper.ShouldBeFalse();
        saved.DailyQuota.ShouldBe(AppConstants.FreeDailyQuota);
    }

    [Fact]
    public async Task ResetPassword_hashes_and_updates()
    {
        var target = new UserRecord(
            TargetId, "target@example.com", "old-hash", null, "T", "Free",
            AppConstants.FreeDailyQuota, "zhuyin", false, false, DateTimeOffset.UtcNow);
        _users.FindByIdAsync(TargetId, Arg.Any<CancellationToken>()).Returns(target);
        _hasher.HashPassword("newpassword1").Returns("new-hash");
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.ResetPasswordAsync(CuratorId, TargetId, "newpassword1");

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.PasswordHash.ShouldBe("new-hash");
        _hasher.Received(1).HashPassword("newpassword1");
    }

    [Fact]
    public async Task ResetPassword_rejects_short_password()
    {
        var result = await _sut.ResetPasswordAsync(CuratorId, TargetId, "short");
        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe("VALIDATION");
    }
}
