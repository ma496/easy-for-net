namespace Backend.External.Email;

using System.Net;
using System.Net.Mail;
using Backend.Attributes;
using Microsoft.Extensions.Options;

/// <summary>
/// Sends transactional email on behalf of the application through the
/// configured SMTP server.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a single email message through the configured SMTP server.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);
}

/// <summary>
/// Default <see cref="IEmailService"/> implementation that uses an
/// <see cref="SmtpClient"/> configured from <see cref="EmailSetting"/>.
/// </summary>
[NoDirectUse]
public class EmailService(IOptions<EmailSetting> emailSetting) : IEmailService
{
    private readonly EmailSetting _emailSetting = emailSetting.Value;

    /// <inheritdoc/>
    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
    {
        using var smtpClient = new SmtpClient(_emailSetting.SmtpServer)
        {
            Port = _emailSetting.SmtpPort,
            Credentials = new NetworkCredential(_emailSetting.SmtpUsername, _emailSetting.SmtpPassword),
            EnableSsl = true,
        };

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_emailSetting.SenderEmail, _emailSetting.SenderName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml
        };
        mailMessage.To.Add(to);

        await smtpClient.SendMailAsync(mailMessage);
    }
}
