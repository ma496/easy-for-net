namespace Backend.Features.Identity.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// Represents an authenticated principal in the system, holding identity, credentials, profile data, and role memberships.
/// </summary>
public class User : AuditableEntity<Guid>, IHasNormalizedProperties
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

    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<AuthToken> AuthTokens { get; set; } = [];
    public ICollection<Token> Tokens { get; set; } = [];

    public void NormalizeProperties()
    {
        UsernameNormalized = Username.Trim().ToLowerInvariant();
        EmailNormalized = Email.Trim().ToLowerInvariant();
    }
}