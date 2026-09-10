using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Soraeru.Curator.Access;

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
            return;
        }

        _hydrated = true;
        try
        {
            var result = await _storage.GetAsync<CuratorSession>(StorageKey);
            if (result.Success)
            {
                Session = result.Value;
            }
        }
        catch
        {
            // First render / JS unavailable — ignore.
        }

        NotifyChanged();
    }

    public async Task SetAsync(CuratorSession session)
    {
        Session = session;
        _hydrated = true;
        await _storage.SetAsync(StorageKey, session);
        NotifyChanged();
    }

    public async Task ClearAsync()
    {
        Session = null;
        _hydrated = true;
        await _storage.DeleteAsync(StorageKey);
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
