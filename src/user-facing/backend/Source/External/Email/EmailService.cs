using Backend.Features.Settings.Core;

namespace Backend.External.Email;

/// <summary>
/// Sends transactional email on behalf of the application, through the SMTP server the
/// <see cref="EmailSettings"/> describe.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a single email message.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
}

/// <summary>
/// Default <see cref="IEmailService"/> implementation: resolves <see cref="EmailSettings"/> at send
/// time - so a job retried after a settings change uses the settings standing when it runs - and hands
/// the message to <see cref="IEmailTransport"/>.
/// </summary>
[NoDirectUse]
public class EmailService(ISettingProvider settingProvider, IEmailTransport transport) : IEmailService
{
    /// <inheritdoc/>
    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
    {
        var settings = await settingProvider.GetAsync<EmailSettings>();
        await transport.SendAsync(settings, new EmailMessage(to, subject, body, isHtml));
    }
}
