namespace Backend.Features.Settings.Core;

/// <summary>
/// The behaviour settings that belong to no single feature, registered as the <c>General</c> setting by
/// <see cref="GeneralSettingsProvider"/> and resolved from the stored override, then the defaults below.
/// </summary>
/// <remarks>
/// Add a property here for a small cross-cutting option rather than registering a setting of its own;
/// a setting that one feature owns outright belongs in that feature's own settings provider instead.
/// Sign-in and refresh read the sign-in options as they stand when the session is minted or renewed.
/// </remarks>
[AllowOutside]
public class GeneralSettings
{
    /// <summary>Whether an account must have verified its email address before it may sign in.</summary>
    public bool IsEmailVerificationRequired { get; set; } = false;

    /// <summary>
    /// Whether anyone may create an account of their own through <c>POST /account/signup</c>. On by
    /// default, since a user-facing site normally lets its visitors register.
    /// </summary>
    public bool IsSignupEnabled { get; set; } = true;

    /// <summary>
    /// The name of the role an account created by sign-up is given, compared case-insensitively. Empty,
    /// or a name no role carries, creates the account with no role rather than failing the sign-up.
    /// </summary>
    public string SignupRoleName { get; set; } = "User";
}

/// <summary>
/// FluentValidation rules for <see cref="GeneralSettings"/>.
/// </summary>
sealed class GeneralSettingsValidator : AbstractValidator<GeneralSettings>
{
    public GeneralSettingsValidator()
    {
        RuleFor(x => x.SignupRoleName).NotNull().MaximumLength(100);
    }
}
