using Soraeru.Application.Abstractions.Auth;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Application.Curator;

public interface ICuratorUserAdminService
{
    Task<ServiceResult<IReadOnlyList<CuratorUserListItem>>> ListUsersAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CuratorUserListItem>> SetDeveloperAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isDeveloper,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ResetPasswordAsync(
        Guid actorUserId,
        Guid targetUserId,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed record CuratorUserListItem(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsDeveloper,
    int DailyQuota,
    bool OnboardingCompleted,
    bool HasPassword,
    DateTimeOffset CreatedAtUtc,
    int LoginCount,
    DateTimeOffset? LastLoginAtUtc);

public sealed class CuratorUserAdminService : ICuratorUserAdminService
{
    private readonly IUserRepository _users;
    private readonly IDeveloperAccountPolicy _curatorPolicy;
    private readonly IPasswordHasher _passwordHasher;

    public CuratorUserAdminService(
        IUserRepository users,
        IDeveloperAccountPolicy curatorPolicy,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _curatorPolicy = curatorPolicy;
        _passwordHasher = passwordHasher;
    }

    public async Task<ServiceResult<IReadOnlyList<CuratorUserListItem>>> ListUsersAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<IReadOnlyList<CuratorUserListItem>>.Failure(auth.Value.Code, auth.Value.Message);
        }

        var users = await _users.ListAsync(cancellationToken);
        var items = users
            .OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .Select(ToListItem)
            .ToList();
        return ServiceResult<IReadOnlyList<CuratorUserListItem>>.Success(items);
    }

    public async Task<ServiceResult<CuratorUserListItem>> SetDeveloperAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isDeveloper,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<CuratorUserListItem>.Failure(auth.Value.Code, auth.Value.Message);
        }

        if (targetUserId == Guid.Empty)
        {
            return ServiceResult<CuratorUserListItem>.Failure("VALIDATION", "Target user id is required.");
        }

        var target = await _users.FindByIdAsync(targetUserId, cancellationToken);
        if (target is null)
        {
            return ServiceResult<CuratorUserListItem>.Failure("NOT_FOUND", "使用者不存在。");
        }

        var updated = target with
        {
            IsDeveloper = isDeveloper,
            DailyQuota = isDeveloper ? AppConstants.UnlimitedDailyQuota : AppConstants.FreeDailyQuota
        };
        await _users.UpdateAsync(updated, cancellationToken);
        return ServiceResult<CuratorUserListItem>.Success(ToListItem(updated));
    }

    public async Task<ServiceResult<bool>> ResetPasswordAsync(
        Guid actorUserId,
        Guid targetUserId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var auth = await EnsureCuratorAsync(actorUserId, cancellationToken);
        if (auth is not null)
        {
            return ServiceResult<bool>.Failure(auth.Value.Code, auth.Value.Message);
        }

        if (targetUserId == Guid.Empty)
        {
            return ServiceResult<bool>.Failure("VALIDATION", "Target user id is required.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return ServiceResult<bool>.Failure("VALIDATION", "Password must be at least 8 characters.");
        }

        var target = await _users.FindByIdAsync(targetUserId, cancellationToken);
        if (target is null)
        {
            return ServiceResult<bool>.Failure("NOT_FOUND", "使用者不存在。");
        }

        var updated = target with { PasswordHash = _passwordHasher.HashPassword(newPassword) };
        await _users.UpdateAsync(updated, cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private static CuratorUserListItem ToListItem(UserRecord user) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsDeveloper,
            user.DailyQuota,
            user.OnboardingCompleted,
            !string.IsNullOrEmpty(user.PasswordHash),
            user.CreatedAtUtc,
            user.LoginCount,
            user.LastLoginAtUtc);

    private async Task<(string Code, string Message)?> EnsureCuratorAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty)
        {
            return ("VALIDATION", "User id is required.");
        }

        var user = await _users.FindByIdAsync(actorUserId, cancellationToken);
        if (user is null)
        {
            return ("NOT_FOUND", "使用者不存在。");
        }

        if (!IsCurator(user))
        {
            return ("FORBIDDEN", "僅策展授權帳號可管理使用者。");
        }

        return null;
    }

    private bool IsCurator(UserRecord user) =>
        user.IsDeveloper || _curatorPolicy.IsDeveloperEmail(user.Email);
}
