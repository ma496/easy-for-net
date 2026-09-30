namespace Backend.External.Email;

using Backend.Features.Settings.Core;

/// <summary>
/// The SMTP server and sender outgoing email is delivered through - a setting (<c>Email</c>) the
/// platform and each tenant may override, whose default is the <c>EmailSettings</c> configuration
/// section when the deployment supplies it, else these initializers.
/// </summary>
public sealed class EmailSettings
{
    public string SmtpServer { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>
    /// Never inherited apart from the server, port and user name it authenticates against: a layer that
    /// overrides any of them without supplying its own password resolves to none.
    /// </summary>
    [SecretSetting(nameof(SmtpServer), nameof(SmtpPort), nameof(SmtpUsername))]
    public string SmtpPassword { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = "no-reply@localhost";
    public string SenderName { get; set; } = string.Empty;
}

/// <summary>Rules every resolved <see cref="EmailSettings"/> must satisfy for mail to be deliverable at all.</summary>
public sealed class EmailSettingsValidator : AbstractValidator<EmailSettings>
{
    public EmailSettingsValidator()
    {
        RuleFor(x => x.SmtpServer).NotEmpty().WithErrorCode(ErrorCodes.EmailSmtpServerRequired.Value);
        RuleFor(x => x.SmtpPort).InclusiveBetween(1, 65535).WithErrorCode(ErrorCodes.EmailSmtpPortInvalid.Value);
        RuleFor(x => x.SenderEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode(ErrorCodes.EmailSenderEmailInvalid.Value)
            .EmailAddress().WithErrorCode(ErrorCodes.EmailSenderEmailInvalid.Value);
    }
}

/// <summary>
/// Declares the <see cref="EmailSettings"/> setting, defaulted from the <c>EmailSettings</c>
/// configuration section.
/// </summary>
public sealed class EmailSettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name <see cref="EmailSettings"/> is registered, stored and addressed under.</summary>
    public const string EmailSettingName = "Email";

    /// <summary>The configuration section whose values stand in for the code default.</summary>
    public const string ConfigurationSection = "EmailSettings";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<EmailSettings>(EmailSettingName, new EmailSettingsValidator()).FromConfiguration(ConfigurationSection);
    }
}
