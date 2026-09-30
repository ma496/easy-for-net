namespace Backend.Features.Identity.Core;

/// <summary>
/// Sign-in behaviour, registered as the <c>Signin</c> setting by <see cref="IdentitySettingsProvider"/>
/// and resolved per tenant - the tenant's own override, then the platform's, then the defaults below.
/// </summary>
/// <remarks>
/// Sign-in and refresh read it for the tenant the session enters; the account self-service flows that
/// run before any tenant exists (sign-up, resending the verification email) read the platform's value.
/// </remarks>
public class SigninSettings
{
    /// <summary>Whether an account must have verified its email address before it may sign in.</summary>
    public bool IsEmailVerificationRequired { get; set; } = false;
}

/// <summary>
/// FluentValidation rules for <see cref="SigninSettings"/>. A single flag admits every value, so it
/// declares no rule yet; it is registered so a rule added with a new property is enforced from the start.
/// </summary>
sealed class SigninSettingsValidator : AbstractValidator<SigninSettings>
{
}