namespace Soraeru.Application.Abstractions.Persistence;

/// <summary>
/// Persistence boundary for user accounts. Implemented in Infrastructure.
/// </summary>
public interface IUserRepository
{
    Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task<UserRecord> AddAsync(UserRecord user, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserRecord user, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record UserRecord(
    Guid Id,
    string Email,
    string? PasswordHash,
    string? GoogleSubject,
    string DisplayName,
    string PlanTier,
    int DailyQuota,
    string NotationPref,
    bool IsDeveloper,
    bool OnboardingCompleted,
    DateTimeOffset CreatedAtUtc,
    int LoginCount = 0,
    DateTimeOffset? LastLoginAtUtc = null);
