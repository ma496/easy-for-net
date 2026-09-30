namespace Backend.Tests.Features.Identity.Sessions;

using Backend.Exceptions;
using Backend.Features.Identity.Core.Sessions;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for how ended sessions are announced, without a host: the store decorator announces exactly what it
/// deleted and only once the deletion succeeded, a publisher or handler that fails is swallowed, and the
/// cross-instance message round-trips while a process ignores its own.
/// </summary>
public class SessionEndedPublishingTests
{
    /// <summary>Every deletion announces the sessions it deleted, and nothing is announced for none.</summary>
    [Fact]
    public async Task Deletions_Announce_What_They_Deleted()
    {
        var inner = new InMemorySessionStore();
        var publisher = new RecordingPublisher();
        var store = new PublishingSessionStore(inner, publisher, NullLogger<PublishingSessionStore>.Instance);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var inTenant = await CreateAsync(store, userId, tenantId);
        var inPlatform = await CreateAsync(store, userId, null);
        var single = await CreateAsync(store, Guid.NewGuid(), null);

        await store.DeleteAsync(single, TestContext.Current.CancellationToken);
        (await store.RevokeByTenantAsync(tenantId, TestContext.Current.CancellationToken)).Should().Equal([inTenant]);
        (await store.RevokeByUserAsync(userId, TestContext.Current.CancellationToken)).Should().Equal([inPlatform]);
        await store.RevokeByUserInTenantAsync(userId, tenantId, TestContext.Current.CancellationToken);

        publisher.Published.Should().BeEquivalentTo<string[]>(
            [[single], [inTenant], [inPlatform]],
            options => options.WithStrictOrdering(),
            "each deletion announces what it deleted, and a revocation that deleted nothing announces nothing");
    }

    /// <summary>A deletion that fails announces nothing and fails exactly as it did.</summary>
    [Fact]
    public async Task A_Failed_Deletion_Announces_Nothing()
    {
        var publisher = new RecordingPublisher();
        var store = new PublishingSessionStore(new UnreachableStore(), publisher, NullLogger<PublishingSessionStore>.Instance);

        var delete = () => store.DeleteAsync("sid", TestContext.Current.CancellationToken);
        var revoke = () => store.RevokeByUserAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<SessionStoreUnavailableException>();
        await revoke.Should().ThrowAsync<SessionStoreUnavailableException>();
        publisher.Published.Should().BeEmpty();
    }

    /// <summary>A publisher that throws never fails the deletion.</summary>
    [Fact]
    public async Task A_Failing_Publisher_Never_Fails_The_Deletion()
    {
        var inner = new InMemorySessionStore();
        var store = new PublishingSessionStore(inner, new ThrowingPublisher(), NullLogger<PublishingSessionStore>.Instance);
        var userId = Guid.NewGuid();
        var sessionId = await CreateAsync(store, userId, null);

        (await store.RevokeByUserAsync(userId, TestContext.Current.CancellationToken)).Should().Equal([sessionId]);
        var delete = () => store.DeleteAsync(sessionId, TestContext.Current.CancellationToken);
        await delete.Should().NotThrowAsync();
    }

    /// <summary>A handler that throws does not stop the handlers after it, and the dispatch never throws.</summary>
    [Fact]
    public async Task A_Failing_Handler_Does_Not_Stop_The_Others()
    {
        var recording = new RecordingHandler();
        var dispatcher = new LocalSessionEndedDispatcher(
            [new ThrowingHandler(), recording],
            NullLogger<LocalSessionEndedDispatcher>.Instance);

        var dispatch = () => dispatcher.DispatchAsync(["a", "b"]);

        await dispatch.Should().NotThrowAsync();
        recording.Received.Should().Equal("a", "b");
    }

    /// <summary>
    /// A message another process sent is read back as the sessions it names; this process's own, and one that
    /// cannot be read, are ignored.
    /// </summary>
    [Fact]
    public void Channel_Messages_Round_Trip_And_Own_Messages_Are_Ignored()
    {
        var own = RedisSessionEndedChannel.Serialize(["a", "b"]);
        var fromOther = own.Replace(RedisSessionEndedChannel.Origin, Guid.NewGuid().ToString("N"));

        RedisSessionEndedChannel.ReadFromOthers(fromOther).Should().Equal("a", "b");
        RedisSessionEndedChannel.ReadFromOthers(own).Should().BeNull("this process told its own handlers already");
        RedisSessionEndedChannel.ReadFromOthers("not json").Should().BeNull();
        RedisSessionEndedChannel.ReadFromOthers(null).Should().BeNull();
        RedisSessionEndedChannel.For("app:").ToString().Should().Be("app:sessions:ended");
    }

    private static async Task<string> CreateAsync(ISessionStore store, Guid userId, Guid? tenantId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        await store.CreateAsync(new SessionRecord
        {
            SessionId = sessionId,
            UserId = userId,
            Username = "u",
            Email = "u@example.com",
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        }, TestContext.Current.CancellationToken);
        return sessionId;
    }

    private sealed class RecordingPublisher : ISessionEndedPublisher
    {
        public List<string[]> Published { get; } = [];

        public Task PublishAsync(IReadOnlyCollection<string> sessionIds)
        {
            Published.Add([.. sessionIds]);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : ISessionEndedPublisher
    {
        public Task PublishAsync(IReadOnlyCollection<string> sessionIds) => throw new InvalidOperationException("publish failed");
    }

    private sealed class RecordingHandler : ISessionEndedHandler
    {
        public List<string> Received { get; } = [];

        public Task OnSessionsEndedAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
        {
            Received.AddRange(sessionIds);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : ISessionEndedHandler
    {
        public Task OnSessionsEndedAsync(IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("handler failed");
    }

    private sealed class UnreachableStore : ISessionStore
    {
        public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default) => throw Outage();

        public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default) => throw Outage();

        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default) => throw Outage();

        public Task<IReadOnlyList<string>> RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw Outage();

        public Task<IReadOnlyList<string>> RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default) => throw Outage();

        public Task<IReadOnlyList<string>> RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw Outage();

        private static SessionStoreUnavailableException Outage() => new("down");
    }
}
