namespace Backend.Features.Identity.Core.Sessions;

using System.Text.Json;
using StackExchange.Redis;

/// <summary>
/// The <see cref="ISessionStore"/> deployments use: sessions as JSON strings under keys that expire on
/// their own, plus one set of session identifiers per user and one per tenant so a group can be revoked
/// without scanning the keyspace.
/// </summary>
/// <remarks>
/// <para>
/// Every key is prefixed with <c>Redis:InstanceName</c>. A session key expires exactly when the session
/// does; an index set has its expiry raised to the session lifetime whenever a session is added to
/// it, and only ever extends it, so it never expires before the last of its members. A member whose record has expired is left in the
/// set until the set is next read, when it is removed.
/// </para>
/// <para>
/// Every failure to reach Redis - a refused or lost connection, a timeout, a disposed multiplexer - is
/// rethrown as <see cref="SessionStoreUnavailableException"/>, so a caller has one thing to fail closed
/// on and never sees the client library's own exception types.
/// </para>
/// </remarks>
[NoDirectUse]
public sealed class RedisSessionStore(IConnectionMultiplexer multiplexer, IOptions<RedisSetting> setting) : ISessionStore
{
    private readonly string _prefix = setting.Value.InstanceName ?? string.Empty;

    /// <inheritdoc />
    public Task CreateAsync(SessionRecord session, CancellationToken cancellationToken = default)
        => RunAsync(async database =>
        {
            var lifetime = session.ExpiresAt - DateTimeOffset.UtcNow;
            if (lifetime <= TimeSpan.Zero)
            {
                return;
            }

            // One MULTI/EXEC, so a connection that drops part-way leaves either the whole session and its
            // index entries or nothing - never a session that no index names, which no revocation could reach.
            var transaction = database.CreateTransaction();
            var writes = new List<Task>
            {
                transaction.StringSetAsync(SessionKey(session.SessionId), JsonSerializer.Serialize(session), lifetime),
            };
            writes.AddRange(AddToIndex(transaction, UserIndexKey(session.UserId), session.SessionId, lifetime));
            if (session.TenantId is { } tenantId)
            {
                writes.AddRange(AddToIndex(transaction, TenantIndexKey(tenantId), session.SessionId, lifetime));
            }

            if (!await transaction.ExecuteAsync())
            {
                throw new SessionStoreUnavailableException("The session store did not commit the session.");
            }

            await Task.WhenAll(writes);
        });

    /// <inheritdoc />
    public Task<SessionRecord?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => RunAsync(async database => Parse(await database.StringGetAsync(SessionKey(sessionId))));

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        => RunAsync(async database =>
        {
            var key = SessionKey(sessionId);
            var session = Parse(await database.StringGetAsync(key));

            var batch = database.CreateBatch();
            var writes = new List<Task> { batch.KeyDeleteAsync(key) };
            if (session is not null)
            {
                writes.AddRange(Unindex(batch, session));
            }

            batch.Execute();
            await Task.WhenAll(writes);
        });

    /// <inheritdoc />
    public Task RevokeByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => RunAsync(database => RevokeAsync(database, UserIndexKey(userId), _ => true));

    /// <inheritdoc />
    public Task RevokeByUserInTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        => RunAsync(database => RevokeAsync(database, UserIndexKey(userId), session => session.TenantId == tenantId));

    /// <inheritdoc />
    public Task RevokeByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => RunAsync(database => RevokeAsync(database, TenantIndexKey(tenantId), _ => true));

    /// <summary>
    /// Reads one index, deletes the live sessions in it that match, and removes from it every member
    /// whose record is gone - the expired sessions an index tolerates until it is read.
    /// </summary>
    private async Task RevokeAsync(IDatabase database, RedisKey indexKey, Func<SessionRecord, bool> matches)
    {
        var members = await database.SetMembersAsync(indexKey);
        if (members.Length == 0)
        {
            return;
        }

        var sessionKeys = members.Select(member => (RedisKey)SessionKey(member.ToString())).ToArray();
        var values = await database.StringGetAsync(sessionKeys);

        var batch = database.CreateBatch();
        var writes = new List<Task>();
        for (var i = 0; i < members.Length; i++)
        {
            var session = Parse(values[i]);
            if (session is null)
            {
                writes.Add(batch.SetRemoveAsync(indexKey, members[i]));
            }
            else if (matches(session))
            {
                writes.Add(batch.KeyDeleteAsync(sessionKeys[i]));
                writes.AddRange(Unindex(batch, session));
            }
        }

        batch.Execute();
        await Task.WhenAll(writes);
    }

    /// <summary>
    /// Adds a member to an index set and gives the set at least <paramref name="lifetime"/> to live. A set
    /// that has no expiry yet (a fresh one) gets it with <c>EXPIRE NX</c>; one that has an expiry only ever
    /// has it extended with <c>EXPIRE GT</c>, never shortened, so a session that lives less long than
    /// another indexed beside it cannot make the set forget the other. Needs Redis 7.0.
    /// </summary>
    private static IEnumerable<Task> AddToIndex(ITransaction transaction, RedisKey indexKey, string sessionId, TimeSpan lifetime)
    {
        yield return transaction.SetAddAsync(indexKey, sessionId);
        yield return transaction.KeyExpireAsync(indexKey, lifetime, ExpireWhen.HasNoExpiry);
        yield return transaction.KeyExpireAsync(indexKey, lifetime, ExpireWhen.GreaterThanCurrentExpiry);
    }

    private IEnumerable<Task> Unindex(IBatch batch, SessionRecord session)
    {
        yield return batch.SetRemoveAsync(UserIndexKey(session.UserId), session.SessionId);
        if (session.TenantId is { } tenantId)
        {
            yield return batch.SetRemoveAsync(TenantIndexKey(tenantId), session.SessionId);
        }
    }

    /// <summary>
    /// The record a stored value holds, or <see langword="null"/> for a missing, unreadable or
    /// already-expired one - none of which is a session anyone may act on.
    /// </summary>
    private static SessionRecord? Parse(RedisValue value)
    {
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            var session = JsonSerializer.Deserialize<SessionRecord>(value.ToString());
            return session is not null && session.ExpiresAt > DateTimeOffset.UtcNow ? session : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string SessionKey(string sessionId) => $"{_prefix}session:{sessionId}";

    private string UserIndexKey(Guid userId) => $"{_prefix}sessions:user:{userId:N}";

    private string TenantIndexKey(Guid tenantId) => $"{_prefix}sessions:tenant:{tenantId:N}";

    private Task RunAsync(Func<IDatabase, Task> action)
        => RunAsync<bool>(async database =>
        {
            await action(database);
            return true;
        });

    private async Task<T> RunAsync<T>(Func<IDatabase, Task<T>> action)
    {
        try
        {
            return await action(multiplexer.GetDatabase());
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or ObjectDisposedException)
        {
            throw new SessionStoreUnavailableException("The session store could not be reached.", exception);
        }
    }
}
