using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Soraeru.Curator.Access;
using Soraeru.Curator.Diagnostics;

namespace Soraeru.Curator.Services;

/// <summary>Circuit-scoped session; mirrors into ProtectedSessionStorage for refresh.</summary>
public sealed class CuratorSessionState
{
    private const string StorageKey = "soraeru.curator.session";
    private readonly ProtectedSessionStorage _storage;
    private bool _hydrated;

    public CuratorSessionState(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public CuratorSession? Session { get; private set; }

    public bool CanEnterMaintenance => CuratorAccessGate.CanEnterMaintenance(Session);

    /// <summary>Raised after session set, clear, or hydrate so layout/nav can refresh.</summary>
    public event Action? Changed;

    public async Task EnsureHydratedAsync()
    {
        if (_hydrated)
        {
            // #region agent log
            AgentDebugLog.Write("B", "CuratorSessionState.EnsureHydratedAsync", "skip-already-hydrated", new
            {
                sessionNull = Session is null,
                isDeveloper = Session?.IsDeveloper,
                hasToken = !string.IsNullOrWhiteSpace(Session?.AccessToken),
                canEnter = CanEnterMaintenance
            });
            // #endregion
            return;
        }

        _hydrated = true;
        var storageSuccess = false;
        string? catchKind = null;
        try
        {
            var result = await _storage.GetAsync<CuratorSession>(StorageKey);
            storageSuccess = result.Success;
            if (result.Success)
            {
                Session = result.Value;
            }
        }
        catch (Exception ex)
        {
            // First render / JS unavailable — ignore.
            catchKind = ex.GetType().Name;
        }

        // #region agent log
        AgentDebugLog.Write("A", "CuratorSessionState.EnsureHydratedAsync", "hydrate-complete", new
        {
            storageSuccess,
            catchKind,
            sessionNull = Session is null,
            isDeveloper = Session?.IsDeveloper,
            hasToken = !string.IsNullOrWhiteSpace(Session?.AccessToken),
            canEnter = CanEnterMaintenance
        });
        // #endregion

        NotifyChanged();
    }

    public async Task SetAsync(CuratorSession session)
    {
        Session = session;
        _hydrated = true;
        await _storage.SetAsync(StorageKey, session);
        // #region agent log
        AgentDebugLog.Write("A", "CuratorSessionState.SetAsync", "session-set", new
        {
            isDeveloper = session.IsDeveloper,
            hasToken = !string.IsNullOrWhiteSpace(session.AccessToken),
            canEnter = CanEnterMaintenance
        });
        // #endregion
        NotifyChanged();
    }

    public async Task ClearAsync()
    {
        Session = null;
        _hydrated = true;
        await _storage.DeleteAsync(StorageKey);
        // #region agent log
        AgentDebugLog.Write("E", "CuratorSessionState.ClearAsync", "session-cleared", new
        {
            canEnter = CanEnterMaintenance
        });
        // #endregion
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
