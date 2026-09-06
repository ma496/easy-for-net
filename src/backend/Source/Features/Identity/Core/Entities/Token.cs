namespace Backend.Features.Identity.Core.Entities;

using Backend.Data.Entities.Base;

/// <summary>
/// A short-lived, single-use token (e.g., email-verification or password-reset) issued to a <see cref="User"/>.
/// </summary>
public class Token : AuditableEntity<Guid>
{
    public string Value { get; set; } = null!;
    public DateTime Expiry { get; set; }
    public bool IsUsed { get; set; }
    public TokenPurpose Purpose { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public enum TokenPurpose
{
    EmailVerification = 1,
    PasswordReset = 2
}
