using NSubstitute;
using Shouldly;
using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Auth;
using Soraeru.Application.Common;

namespace Soraeru.Application.Tests.Auth;

public sealed class AuthServiceDeveloperSyncTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IPasswordResetTokenStore _reset = Substitute.For<IPasswordResetTokenStore>();
    private readonly IDeveloperAccountPolicy _developers = Substitute.For<IDeveloperAccountPolicy>();
    private readonly IGoogleIdTokenValidator _google = Substitute.For<IGoogleIdTokenValidator>();
    private readonly AuthService _sut;

    public AuthServiceDeveloperSyncTests()
    {
        _sut = new AuthService(_users, _hasher, _tokens, _email, _reset, _developers, _google);
        _hasher.VerifyHashedPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _tokens.CreateAccessToken(Arg.Any<Guid>(), Arg.Any<string>()).Returns("token");
    }

    [Fact]
    public async Task Login_keeps_db_IsDeveloper_when_not_on_allowlist()
    {
        var user = new UserRecord(
            UserId, "promoted@example.com", "hash", null, "P", "Free",
            AppConstants.UnlimitedDailyQuota, "zhuyin", true, true, DateTimeOffset.UtcNow);
        _users.FindByEmailAsync("promoted@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _developers.IsDeveloperEmail("promoted@example.com").Returns(false);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(new LoginEmailCommand("promoted@example.com", "password1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsDeveloper.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.IsDeveloper.ShouldBeTrue();
        saved.LoginCount.ShouldBe(1);
        saved.LastLoginAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Login_force_developer_when_on_allowlist_even_if_db_false()
    {
        var user = new UserRecord(
            UserId, "listed@example.com", "hash", null, "L", "Free",
            AppConstants.FreeDailyQuota, "zhuyin", false, true, DateTimeOffset.UtcNow);
        _users.FindByEmailAsync("listed@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _developers.IsDeveloperEmail("listed@example.com").Returns(true);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(new LoginEmailCommand("listed@example.com", "password1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsDeveloper.ShouldBeTrue();
        saved.ShouldNotBeNull();
        saved!.IsDeveloper.ShouldBeTrue();
        saved.DailyQuota.ShouldBe(AppConstants.UnlimitedDailyQuota);
        saved.LoginCount.ShouldBe(1);
        saved.LastLoginAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Login_does_not_clear_non_allowlist_non_developer()
    {
        var user = new UserRecord(
            UserId, "normal@example.com", "hash", null, "N", "Free",
            AppConstants.FreeDailyQuota, "zhuyin", false, true, DateTimeOffset.UtcNow,
            LoginCount: 2);
        _users.FindByEmailAsync("normal@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _developers.IsDeveloperEmail("normal@example.com").Returns(false);
        UserRecord? saved = null;
        _users.UpdateAsync(Arg.Any<UserRecord>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                saved = ci.ArgAt<UserRecord>(0);
                return Task.CompletedTask;
            });

        var result = await _sut.LoginWithEmailAsync(new LoginEmailCommand("normal@example.com", "password1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value!.IsDeveloper.ShouldBeFalse();
        saved.ShouldNotBeNull();
        saved!.IsDeveloper.ShouldBeFalse();
        saved.LoginCount.ShouldBe(3);
        saved.LastLoginAtUtc.ShouldNotBeNull();
    }
}
