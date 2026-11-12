using Backend.ShareData.Entities.Base;

namespace Backend.Features.Identity.Core.Entities;

/// <summary>
/// Represents an authenticated principal in the system, holding identity, credentials, profile data, and role memberships.
/// </summary>
public class User : AuditableEntity<Guid>, IHasNormalizedProperties, ISystemCreated
{
    public bool SystemCreated { get; set; }
    public string Username { get; set; } = null!;
    public string UsernameNormalized { get; private set; } = null!;
    public string Email { get; set; } = null!;
    public string EmailNormalized { get; private set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsEmailVerified { get; set; }
    public DateTime? LastSigninAt { get; set; }
    public string? Image { get; set; }

    /// <summary>
    /// Rotated whenever the account's credentials change (password change or reset) and when it is
    /// deactivated. Every refresh-token row records the stamp its chain was signed in under, and a renewal
    /// whose row names an older stamp is refused. Existing rows default to <see cref="Guid.Empty"/>.
    /// </summary>
    public Guid SecurityStamp { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<AuthToken> AuthTokens { get; set; } = [];
    public ICollection<Token> Tokens { get; set; } = [];

    public void NormalizeProperties()
    {
        UsernameNormalized = Username.Trim().ToLowerInvariant();
        EmailNormalized = Email.Trim().ToLowerInvariant();
    }
}