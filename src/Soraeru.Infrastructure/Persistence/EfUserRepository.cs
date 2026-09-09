using Microsoft.EntityFrameworkCore;
using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Infrastructure.Persistence.Entities;

namespace Soraeru.Infrastructure.Persistence;

public sealed class EfUserRepository : IUserRepository
{
    private readonly SoraeruDbContext _db;

    public EfUserRepository(SoraeruDbContext db)
    {
        _db = db;
    }

    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var entity = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserRecord?> FindByGoogleSubjectAsync(
        string googleSubject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleSubject))
        {
            return null;
        }

        var subject = googleSubject.Trim();
        var entity = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.GoogleSubject == subject, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<UserRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _db.Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRecord).ToList();
    }

    public async Task<UserRecord> AddAsync(UserRecord user, CancellationToken cancellationToken = default)
    {
        var entity = ToEntity(user);
        _db.Users.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(UserRecord user, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken)
            ?? throw new InvalidOperationException($"User {user.Id} was not found.");

        entity.Email = user.Email.Trim().ToLowerInvariant();
        entity.PasswordHash = user.PasswordHash;
        entity.GoogleSubject = user.GoogleSubject;
        entity.DisplayName = user.DisplayName;
        entity.PlanTier = user.PlanTier;
        entity.DailyQuota = user.DailyQuota;
        entity.NotationPref = user.NotationPref;
        entity.IsDeveloper = user.IsDeveloper;
        entity.OnboardingCompleted = user.OnboardingCompleted;
        entity.CreatedAt = user.CreatedAtUtc;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _db.Users.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static UserRecord ToRecord(UserEntity entity) =>
        new(
            entity.Id,
            entity.Email,
            entity.PasswordHash,
            entity.GoogleSubject,
            entity.DisplayName,
            entity.PlanTier,
            entity.DailyQuota,
            entity.NotationPref,
            entity.IsDeveloper,
            entity.OnboardingCompleted,
            entity.CreatedAt);

    private static UserEntity ToEntity(UserRecord user) =>
        new()
        {
            Id = user.Id,
            Email = user.Email.Trim().ToLowerInvariant(),
            PasswordHash = user.PasswordHash,
            GoogleSubject = user.GoogleSubject,
            DisplayName = user.DisplayName,
            PlanTier = user.PlanTier,
            DailyQuota = user.DailyQuota,
            NotationPref = user.NotationPref,
            IsDeveloper = user.IsDeveloper,
            OnboardingCompleted = user.OnboardingCompleted,
            CreatedAt = user.CreatedAtUtc
        };
}
