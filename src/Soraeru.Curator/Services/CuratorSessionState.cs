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
    }

    public async Task SetAsync(CuratorSession session)
    {
        Session = session;
        _hydrated = true;
        await _storage.SetAsync(StorageKey, session);
    }

    public async Task ClearAsync()
    {
        Session = null;
        _hydrated = true;
        await _storage.DeleteAsync(StorageKey);
    }
}
