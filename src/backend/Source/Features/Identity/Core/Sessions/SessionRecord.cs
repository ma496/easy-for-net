namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Everything the API knows about one signed-in session beyond who the caller is: the account, the
/// tenant it acts in, the roles and permissions that tenant grants it. The access token and the auth
/// cookie carry only the account and <see cref="SessionId"/>; this record, read from the session store
/// on every authenticated request, is the single source of the rest.
/// </summary>
/// <remarks>
/// It describes the account's standing at the moment the session was minted, exactly as the claims a
/// token used to carry did, and it lives until <see cref="ExpiresAt"/> or until something deletes it -
/// which is what makes a session revocable before its token expires.
/// </remarks>
public sealed class SessionRecord
{
    /// <summary>Gets the opaque random identifier the token names this session by.</summary>
    public string SessionId { get; init; } = null!;

    /// <summary>Gets the account the session belongs to.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the account's username when the session was minted.</summary>
    public string Username { get; init; } = null!;

    /// <summary>Gets the account's email address when the session was minted.</summary>
    public string Email { get; init; } = null!;

    /// <summary>Gets a value indicating whether the account belongs to the platform tier.</summary>
    public bool IsPlatform { get; init; }

    /// <summary>Gets the tenant the session acts in, or <see langword="null"/> when it acts in none.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Gets the names of the roles the account holds in the scope being acted in.</summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>Gets the names of the permissions those roles grant, narrowed to the scope and plan.</summary>
    public List<string> Permissions { get; init; } = [];

    /// <summary>Gets the moment the session was minted.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the moment after which the session is no longer honoured.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
