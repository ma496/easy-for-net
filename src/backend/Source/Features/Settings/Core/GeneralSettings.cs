namespace Backend.Features.Settings.Core;

/// <summary>
/// The behaviour settings that belong to no single feature, registered as the <c>General</c> setting by
/// <see cref="GeneralSettingsProvider"/> and resolved per tenant - the tenant's own override, then the
/// platform's, then the defaults below.
/// </summary>
/// <remarks>
/// Add a property here for a small cross-cutting option rather than registering a setting of its own;
/// a setting that one feature owns outright belongs in that feature's own settings provider instead.
/// Sign-in and refresh read the sign-in options for the tenant the session enters; the account self-service
/// flows that run before any tenant exists (sign-up, resending the verification email) read the platform's value.
/// A property marked <see cref="PlatformSettingAttribute"/> - <see cref="IsSignupEnabled"/> - is the
/// platform's alone: no tenant may override it, and it resolves to the platform's value in every scope.
/// </remarks>
[AllowOutside]
public class GeneralSettings
{
    /// <summary>Whether an account must have verified its email address before it may sign in.</summary>
    public bool IsEmailVerificationRequired { get; set; } = false;

    /// <summary>
    /// Whether anyone may sign up and create a tenant of their own through <c>POST /account/signup</c>.
    /// Off by default, so a fresh installation creates tenants only through the platform administrator.
    /// Sign-up happens before any tenant exists, so this is read from the platform only and no tenant
    /// may override it.
    /// </summary>
    [PlatformSetting]
    public bool IsSignupEnabled { get; set; } = false;
}

/// <summary>
/// FluentValidation rules for <see cref="GeneralSettings"/>. Its flags admit every value, so it
/// declares no rule yet; it is registered so a rule added with a new property is enforced from the start.
/// </summary>
sealed class GeneralSettingsValidator : AbstractValidator<GeneralSettings>
{
}
