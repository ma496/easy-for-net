using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// What a sign-in ticket stands for: one account entering one tenant, authorized under the security
/// stamp the account held when the ticket was issued.
/// </summary>
/// <param name="UserId">The account the ticket signs in.</param>
/// <param name="TenantId">The only tenant the ticket may be redeemed into.</param>
/// <param name="SecurityStamp">The account's stamp at issuance; a credential change since voids the ticket.</param>
public sealed record SignInTicket(Guid UserId, Guid TenantId, Guid SecurityStamp);

/// <summary>
/// Short-lived, single-use tickets that carry a signed-in account from one host to a tenant's own host,
/// where it has no session yet. Each host keeps its own cookies, so the ticket is the one thing the new
/// host is handed; it is redeemed there for a session of its own.
/// </summary>
/// <remarks>
/// Only a hash of the ticket is stored, so a read of the store yields nothing redeemable. Taking a
/// ticket removes it in the same operation, so two redemptions racing each other cannot both succeed.
/// An implementation that cannot reach its backing store throws <see cref="SessionStoreUnavailableException"/>.
/// </remarks>
public interface ISignInTicketStore
{
    /// <summary>Stores a ticket for <see cref="SignInTicketStoreDefaults.Lifetime"/> and returns the value to hand over.</summary>
    /// <param name="ticket">What the ticket stands for.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The opaque ticket value; it is never stored as is.</returns>
    Task<string> IssueAsync(SignInTicket ticket, CancellationToken cancellationToken = default);

    /// <summary>Removes a ticket and returns what it stood for.</summary>
    /// <param name="value">The ticket value <see cref="IssueAsync"/> returned.</param>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>The ticket, or <see langword="null"/> when it is unknown, already taken or expired.</returns>
    Task<SignInTicket?> TakeAsync(string value, CancellationToken cancellationToken = default);
}

/// <summary>Values shared by every <see cref="ISignInTicketStore"/>.</summary>
public static class SignInTicketStoreDefaults
{
    /// <summary>How long a ticket stays redeemable: long enough to open a tab, too short to be worth stealing.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    /// <summary>A new random ticket value, URL-safe.</summary>
    internal static string NewValue()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The key a ticket value is stored under: its SHA-256 hash.</summary>
    internal static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

/// <summary>Redis-backed tickets, sharing the session store's connection and key prefix.</summary>
[NoDirectUse]
public sealed class RedisSignInTicketStore(IConnectionMultiplexer multiplexer, IOptions<RedisSetting> setting) : ISignInTicketStore
{
    private readonly string _prefix = setting.Value.InstanceName ?? string.Empty;

    /// <inheritdoc />
    public async Task<string> IssueAsync(SignInTicket ticket, CancellationToken cancellationToken = default)
    {
        var value = SignInTicketStoreDefaults.NewValue();
        await RunAsync(database => database.StringSetAsync(Key(value), JsonSerializer.Serialize(ticket), SignInTicketStoreDefaults.Lifetime));
        return value;
    }

    /// <inheritdoc />
    public async Task<SignInTicket?> TakeAsync(string value, CancellationToken cancellationToken = default)
    {
        // GETDEL reads and removes in one command, which is what makes the ticket single-use.
        var stored = await RunAsync(database => database.StringGetDeleteAsync(Key(value)));
        if (stored.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SignInTicket>(stored.ToString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string Key(string value) => $"{_prefix}signin-ticket:{SignInTicketStoreDefaults.Hash(value)}";

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

/// <summary>In-process tickets for the <c>Testing</c> host, beside its in-memory session store.</summary>
[NoDirectUse]
public sealed class InMemorySignInTicketStore(TimeProvider? timeProvider = null) : ISignInTicketStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (SignInTicket Ticket, DateTimeOffset ExpiresAt)> _tickets = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<string> IssueAsync(SignInTicket ticket, CancellationToken cancellationToken = default)
    {
        var value = SignInTicketStoreDefaults.NewValue();
        _tickets[SignInTicketStoreDefaults.Hash(value)] = (ticket, _timeProvider.GetUtcNow() + SignInTicketStoreDefaults.Lifetime);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task<SignInTicket?> TakeAsync(string value, CancellationToken cancellationToken = default)
    {
        if (!_tickets.TryRemove(SignInTicketStoreDefaults.Hash(value), out var entry) || entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Task.FromResult<SignInTicket?>(null);
        }

        return Task.FromResult<SignInTicket?>(entry.Ticket);
    }
}
