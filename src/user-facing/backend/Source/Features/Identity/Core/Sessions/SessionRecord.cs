namespace Backend.Features.Identity.Core.Sessions;

/// <summary>
/// Everything the API knows about one signed-in session beyond who the caller is: the account and the
/// roles and permissions it holds. The access token and the auth
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

    /// <summary>Gets the names of the roles the account holds.</summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>Gets the names of the permissions those roles grant.</summary>
    public List<string> Permissions { get; init; } = [];

    /// <summary>
    /// Gets the account's security stamp when the session was minted. Records stored before the property
    /// existed read as <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid SecurityStamp { get; init; }

    /// <summary>Gets the moment the session was minted.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the moment after which the session is no longer honoured.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}
