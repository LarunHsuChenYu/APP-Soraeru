using Soraeru.Application.Abstractions.Persistence;
using Soraeru.Application.Common;

namespace Soraeru.Infrastructure.Persistence;

/// <summary>
/// In-memory stand-in for tests or Persistence:Provider=InMemory.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<UserRecord> _users = [];
    private readonly object _gate = new();

    public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(
                _users.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<UserRecord?> FindByGoogleSubjectAsync(
        string googleSubject,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(googleSubject))
            {
                return Task.FromResult<UserRecord?>(null);
            }

            return Task.FromResult(
                _users.FirstOrDefault(u =>
                    string.Equals(u.GoogleSubject, googleSubject.Trim(), StringComparison.Ordinal)));
        }
    }

    public Task<UserRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        }
    }

    public Task<IReadOnlyList<UserRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<UserRecord> list = _users
                .OrderBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task<UserRecord> AddAsync(UserRecord user, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var stored = user with
            {
                Email = user.Email.Trim().ToLowerInvariant(),
                DailyQuota = user.DailyQuota == 0 ? AppConstants.FreeDailyQuota : user.DailyQuota
            };
            _users.Add(stored);
            return Task.FromResult(stored);
        }
    }

    public Task UpdateAsync(UserRecord user, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var index = _users.FindIndex(u => u.Id == user.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"User {user.Id} was not found.");
            }

            _users[index] = user with { Email = user.Email.Trim().ToLowerInvariant() };
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _users.RemoveAll(u => u.Id == id);
            return Task.CompletedTask;
        }
    }
}
