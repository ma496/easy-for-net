namespace Backend.Tests.Features.Identity.Sessions;

using Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Tests for the semantics every <see cref="ISessionStore"/> promises - expiry, the per-user and
/// per-tenant indexes, and the three revocations - against the in-memory store, with a clock the test
/// moves. These need no host and no database.
/// </summary>
public class InMemorySessionStoreTests
{
    private readonly ManualClock _clock = new();
    private readonly InMemorySessionStore _store;

    public InMemorySessionStoreTests() => _store = new InMemorySessionStore(_clock);

    /// <summary>Verifies a stored session is read back, and a delete removes it.</summary>
    [Fact]
    public async Task Create_Get_Delete()
    {
        var session = NewSession(Guid.NewGuid(), Guid.NewGuid());

        await _store.CreateAsync(session, TestContext.Current.CancellationToken);
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeSameAs(session);

        await _store.DeleteAsync(session.SessionId, TestContext.Current.CancellationToken);
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _store.GetAsync("unknown", TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Verifies a session is gone once its expiry passes, and that reading it drops its index entries.</summary>
    [Fact]
    public async Task Expired_Session_Reads_As_Missing_And_Is_Unindexed()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var session = NewSession(userId, tenantId, lifetime: TimeSpan.FromMinutes(10));
        await _store.CreateAsync(session, TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(9));
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        _store.IndexedForUser(userId).Should().Be(0);
        _store.IndexedForTenant(tenantId).Should().Be(0);
    }

    /// <summary>Verifies a session that is already expired is never stored.</summary>
    [Fact]
    public async Task Already_Expired_Session_Is_Not_Stored()
    {
        var userId = Guid.NewGuid();
        var session = NewSession(userId, null, lifetime: TimeSpan.FromMinutes(-1));

        await _store.CreateAsync(session, TestContext.Current.CancellationToken);

        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        _store.IndexedForUser(userId).Should().Be(0);
    }

    /// <summary>Verifies reading an index prunes the entries of sessions that have expired, and revokes only live ones.</summary>
    [Fact]
    public async Task Revocation_Prunes_Expired_Index_Entries()
    {
        var userId = Guid.NewGuid();
        var expiring = NewSession(userId, null, lifetime: TimeSpan.FromMinutes(1));
        var lasting = NewSession(userId, null, lifetime: TimeSpan.FromHours(1));
        await _store.CreateAsync(expiring, TestContext.Current.CancellationToken);
        await _store.CreateAsync(lasting, TestContext.Current.CancellationToken);
        _store.IndexedForUser(userId).Should().Be(2);

        _clock.Advance(TimeSpan.FromMinutes(5));
        var revoked = await _store.RevokeByUserAsync(userId, TestContext.Current.CancellationToken);

        revoked.Should().Equal([lasting.SessionId], "only a live session is deleted, so only it is reported");

        _store.IndexedForUser(userId).Should().Be(0, "the expired entry was pruned and the live one revoked");
        (await _store.GetAsync(lasting.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Verifies revoking by user takes every session of that user in every tenant and no one else's.</summary>
    [Fact]
    public async Task Revoke_By_User()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var mine = new[] { NewSession(user, tenant), NewSession(user, Guid.NewGuid()), NewSession(user, null) };
        var theirs = NewSession(other, tenant);
        foreach (var session in mine.Append(theirs))
        {
            await _store.CreateAsync(session, TestContext.Current.CancellationToken);
        }

        var revoked = await _store.RevokeByUserAsync(user, TestContext.Current.CancellationToken);

        revoked.Should().BeEquivalentTo(mine.Select(session => session.SessionId));
        foreach (var session in mine)
        {
            (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        }

        (await _store.GetAsync(theirs.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        _store.IndexedForTenant(tenant).Should().Be(1, "the revoked sessions left the tenant index too");
    }

    /// <summary>Verifies revoking a user in a tenant leaves that user's sessions elsewhere alone.</summary>
    [Fact]
    public async Task Revoke_By_User_In_Tenant()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var inTenant = NewSession(user, tenant);
        var elsewhere = NewSession(user, Guid.NewGuid());
        var platform = NewSession(user, null);
        var otherUser = NewSession(Guid.NewGuid(), tenant);
        foreach (var session in new[] { inTenant, elsewhere, platform, otherUser })
        {
            await _store.CreateAsync(session, TestContext.Current.CancellationToken);
        }

        var revoked = await _store.RevokeByUserInTenantAsync(user, tenant, TestContext.Current.CancellationToken);

        revoked.Should().Equal([inTenant.SessionId]);
        (await _store.GetAsync(inTenant.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _store.GetAsync(elsewhere.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await _store.GetAsync(platform.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await _store.GetAsync(otherUser.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
    }

    /// <summary>Verifies revoking a tenant takes every session acting in it, whoever holds it, and no others.</summary>
    [Fact]
    public async Task Revoke_By_Tenant()
    {
        var tenant = Guid.NewGuid();
        var first = NewSession(Guid.NewGuid(), tenant);
        var second = NewSession(Guid.NewGuid(), tenant);
        var otherTenant = NewSession(Guid.NewGuid(), Guid.NewGuid());
        var platform = NewSession(Guid.NewGuid(), null);
        foreach (var session in new[] { first, second, otherTenant, platform })
        {
            await _store.CreateAsync(session, TestContext.Current.CancellationToken);
        }

        var revoked = await _store.RevokeByTenantAsync(tenant, TestContext.Current.CancellationToken);

        revoked.Should().BeEquivalentTo([first.SessionId, second.SessionId]);
        (await _store.GetAsync(first.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _store.GetAsync(second.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        (await _store.GetAsync(otherTenant.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        (await _store.GetAsync(platform.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();
        _store.IndexedForTenant(tenant).Should().Be(0);
    }

    /// <summary>Verifies revoking a user or tenant that has no sessions is a no-op.</summary>
    [Fact]
    public async Task Revoking_Nothing_Is_Fine()
    {
        (await _store.RevokeByUserAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await _store.RevokeByUserInTenantAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await _store.RevokeByTenantAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private SessionRecord NewSession(Guid userId, Guid? tenantId, TimeSpan? lifetime = null)
    {
        var now = _clock.GetUtcNow();
        return new SessionRecord
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Username = "user",
            Email = "user@example.com",
            TenantId = tenantId,
            CreatedAt = now,
            ExpiresAt = now + (lifetime ?? TimeSpan.FromHours(1)),
        };
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
