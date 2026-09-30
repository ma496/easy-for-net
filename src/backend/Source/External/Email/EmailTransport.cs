namespace Backend.External.Email;

using System.Net;
using System.Net.Mail;

/// <summary>One outgoing email message, independent of how it is delivered.</summary>
/// <param name="To">Recipient email address.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Body">Body content.</param>
/// <param name="IsHtml">Whether <paramref name="Body"/> is HTML.</param>
public sealed record EmailMessage(string To, string Subject, string Body, bool IsHtml);

/// <summary>
/// Delivers one message with the settings it is handed - the seam between choosing which
/// <see cref="EmailSettings"/> apply (<see cref="IEmailService"/>) and talking to a mail server.
/// </summary>
public interface IEmailTransport
{
    /// <summary>Delivers <paramref name="message"/> through the server <paramref name="settings"/> describe.</summary>
    Task SendAsync(EmailSettings settings, EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IEmailTransport"/>, delivering over SMTP with TLS and the configured credentials.</summary>
[NoDirectUse]
public class SmtpEmailTransport : IEmailTransport
{
    /// <inheritdoc/>
    public async Task SendAsync(EmailSettings settings, EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var smtpClient = new SmtpClient(settings.SmtpServer)
        {
            Port = settings.SmtpPort,
            EnableSsl = true,
        };

        // No user name and no password means an unauthenticated relay: offer no credentials rather than empty ones.
        if (settings.SmtpUsername.Length > 0 || settings.SmtpPassword.Length > 0)
        {
            smtpClient.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(settings.SenderEmail, settings.SenderName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml
        };
        mailMessage.To.Add(message.To);

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);
    }
}
