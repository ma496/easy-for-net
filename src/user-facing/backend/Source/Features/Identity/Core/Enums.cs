namespace Backend.Features.Identity.Core;

/// <summary>
/// What a <see cref="Entities.Token"/> was issued for.
/// </summary>
public enum TokenPurpose
{
    EmailVerification,
    PasswordReset
}
