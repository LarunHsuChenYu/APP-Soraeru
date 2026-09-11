using NSubstitute;
using Shouldly;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Auth;
using Soraeru.Application.Common;

namespace Soraeru.Application.Tests.Auth;

public sealed class AuthServiceLoginStatsTests
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IPasswordResetTokenStore _reset = Substitute.For<IPasswordResetTokenStore>();
    private readonly IDeveloperAccountPolicy _developers = Substitute.For<IDeveloperAccountPolicy>();
    private readonly IGoogleIdTokenValidator _google = Substitute.For<IGoogleIdTokenValidator>();
    private readonly AuthService _sut;

    public AuthServiceLoginStatsTests()
    {
        _sut = new AuthService(_users, _hasher, _tokens, _email, _reset, _developers, _google);
        _hasher.VerifyHashedPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _tokens.CreateAccessToken(Arg.Any<Guid>(), Arg.Any<string>()).Returns("token");
        _developers.IsDeveloperEmail(Arg.Any<string>()).Returns(false);
    }

    [Fact]
    public async Task App_login_increments_login_count_and_last_login()
    {
        var user = BaseUser(LoginCount: 2);
        _users.FindByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(
            new LoginEmailCommand("user@example.com", "password1", AuthClient.App));

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.LoginCount.ShouldBe(3);
        saved.LastLoginAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Curator_login_does_not_change_app_login_stats()
    {
        var lastLogin = DateTimeOffset.Parse("2026-03-01T08:30:00Z");
        var user = BaseUser(LoginCount: 4, LastLoginAtUtc: lastLogin);
        _users.FindByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginWithEmailAsync(
            new LoginEmailCommand("user@example.com", "password1", AuthClient.Curator));

        result.IsSuccess.ShouldBeTrue();
        await _users.DidNotReceive()
            .UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Default_client_is_app_and_records_login()
    {
        var user = BaseUser(LoginCount: 0);
        _users.FindByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(user);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(
            new LoginEmailCommand("user@example.com", "password1"));

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.LoginCount.ShouldBe(1);
    }

    [Fact]
    public async Task Curator_login_still_syncs_developer_without_touching_login_stats()
    {
        var lastLogin = DateTimeOffset.Parse("2026-03-01T08:30:00Z");
        var user = new UserRecord(
            UserId,
            "listed@example.com",
            "hash",
            null,
            "L",
            "Free",
            AppConstants.FreeDailyQuota,
            "zhuyin",
            false,
            true,
            DateTimeOffset.UtcNow,
            LoginCount: 4,
            LastLoginAtUtc: lastLogin);
        _users.FindByEmailAsync("listed@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _developers.IsDeveloperEmail("listed@example.com").Returns(true);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(
            new LoginEmailCommand("listed@example.com", "password1", AuthClient.Curator));

        result.IsSuccess.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.IsDeveloper.ShouldBeTrue();
        saved.DailyQuota.ShouldBe(AppConstants.UnlimitedDailyQuota);
        saved.LoginCount.ShouldBe(4);
        saved.LastLoginAtUtc.ShouldBe(lastLogin);
    }

    private static UserRecord BaseUser(
        int LoginCount = 0,
        DateTimeOffset? LastLoginAtUtc = null) =>
        new(
            UserId,
            "user@example.com",
            "hash",
            null,
            "U",
            "Free",
            AppConstants.FreeDailyQuota,
            "zhuyin",
            false,
            true,
            DateTimeOffset.UtcNow,
            LoginCount,
            LastLoginAtUtc);
}
