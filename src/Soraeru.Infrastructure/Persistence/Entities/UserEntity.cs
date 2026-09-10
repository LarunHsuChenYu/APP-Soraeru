using Soraeru.Application.Common;

namespace Soraeru.Infrastructure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public string? GoogleSubject { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string PlanTier { get; set; } = AppConstants.PlanTierFree;

    public int DailyQuota { get; set; } = AppConstants.FreeDailyQuota;

    public string NotationPref { get; set; } = AppConstants.DefaultNotationPref;

    public bool IsDeveloper { get; set; }

    public bool OnboardingCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int LoginCount { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }
}
