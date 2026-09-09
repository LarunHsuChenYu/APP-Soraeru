namespace Soraeru.Curator.Access;

public sealed record CuratorSession(
    Guid UserId,
    string Email,
    string AccessToken,
    bool IsDeveloper);

/// <summary>
/// UI gate: only allowlist (IsDeveloper) sessions with a JWT may enter maintenance.
/// API still enforces the same policy on every write.
/// </summary>
public static class CuratorAccessGate
{
    public static bool CanEnterMaintenance(CuratorSession? session) =>
        session is not null
        && !string.IsNullOrWhiteSpace(session.AccessToken)
        && session.IsDeveloper;
}
