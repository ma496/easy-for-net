using Backend.Features.Identity.Core.Sessions;

namespace Backend.Tests.Features.Identity.Sessions;

/// <summary>
/// Tests for the semantics every <see cref="ISessionStore"/> promises - expiry, the per-user and
/// the per-user index, and revocation by user - against the in-memory store, with a clock the test
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
        var session = NewSession(Guid.NewGuid());

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
        var session = NewSession(userId, lifetime: TimeSpan.FromMinutes(10));
        await _store.CreateAsync(session, TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromMinutes(9));
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        _store.IndexedForUser(userId).Should().Be(0);
    }

    /// <summary>Verifies a session that is already expired is never stored.</summary>
    [Fact]
    public async Task Already_Expired_Session_Is_Not_Stored()
    {
        var userId = Guid.NewGuid();
        var session = NewSession(userId, lifetime: TimeSpan.FromMinutes(-1));

        await _store.CreateAsync(session, TestContext.Current.CancellationToken);

        (await _store.GetAsync(session.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
        _store.IndexedForUser(userId).Should().Be(0);
    }

    /// <summary>Verifies reading an index prunes the entries of sessions that have expired, and revokes only live ones.</summary>
    [Fact]
    public async Task Revocation_Prunes_Expired_Index_Entries()
    {
        var userId = Guid.NewGuid();
        var expiring = NewSession(userId, lifetime: TimeSpan.FromMinutes(1));
        var lasting = NewSession(userId, lifetime: TimeSpan.FromHours(1));
        await _store.CreateAsync(expiring, TestContext.Current.CancellationToken);
        await _store.CreateAsync(lasting, TestContext.Current.CancellationToken);
        _store.IndexedForUser(userId).Should().Be(2);

        _clock.Advance(TimeSpan.FromMinutes(5));
        var revoked = await _store.RevokeByUserAsync(userId, TestContext.Current.CancellationToken);

        revoked.Should().Equal([lasting.SessionId], "only a live session is deleted, so only it is reported");

        _store.IndexedForUser(userId).Should().Be(0, "the expired entry was pruned and the live one revoked");
        (await _store.GetAsync(lasting.SessionId, TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>Verifies revoking by user takes every session of that user and no one else's.</summary>
    [Fact]
    public async Task Revoke_By_User()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var mine = new[] { NewSession(user), NewSession(user), NewSession(user) };
        var theirs = NewSession(other);
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
    }

    /// <summary>Verifies revoking a user that has no sessions is a no-op.</summary>
    [Fact]
    public async Task Revoking_Nothing_Is_Fine()
    {
        (await _store.RevokeByUserAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private SessionRecord NewSession(Guid userId, TimeSpan? lifetime = null)
    {
        var now = _clock.GetUtcNow();
        return new SessionRecord
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Username = "user",
            Email = "user@example.com",
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
