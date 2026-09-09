using Shouldly;
using Soraeru.Curator.Access;

namespace Soraeru.Curator.Tests.Access;

public sealed class CuratorAccessGateTests
{
    [Fact]
    public void CanEnterMaintenance_null_session_is_false()
    {
        CuratorAccessGate.CanEnterMaintenance(null).ShouldBeFalse();
    }

    [Fact]
    public void CanEnterMaintenance_missing_token_is_false()
    {
        var session = new CuratorSession(
            UserId: Guid.NewGuid(),
            Email: "curator@example.com",
            AccessToken: "",
            IsDeveloper: true);

        CuratorAccessGate.CanEnterMaintenance(session).ShouldBeFalse();
    }

    [Fact]
    public void CanEnterMaintenance_non_allowlist_is_false_even_with_token()
    {
        var session = new CuratorSession(
            UserId: Guid.NewGuid(),
            Email: "learner@example.com",
            AccessToken: "jwt",
            IsDeveloper: false);

        CuratorAccessGate.CanEnterMaintenance(session).ShouldBeFalse();
    }

    [Fact]
    public void CanEnterMaintenance_allowlist_with_token_is_true()
    {
        var session = new CuratorSession(
            UserId: Guid.NewGuid(),
            Email: "curator@example.com",
            AccessToken: "jwt",
            IsDeveloper: true);

        CuratorAccessGate.CanEnterMaintenance(session).ShouldBeTrue();
    }
}
