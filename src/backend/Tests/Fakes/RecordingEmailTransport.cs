namespace Backend.Tests.Fakes;

using System.Collections.Concurrent;
using Backend.External.Email;

/// <summary>
/// The <see cref="IEmailTransport"/> the test host uses: it delivers nothing and records every message
/// with a snapshot of the <see cref="EmailSettings"/> it was handed, so a test can see which settings a
/// send resolved.
/// </summary>
/// <remarks>
/// The Testing environment's mail settings are placeholders, so a real send would reach an SMTP server
/// that refuses it. The real <see cref="IEmailService"/> still runs - resolution of the settings is what
/// is under test - and only delivery is replaced. Messages are keyed by recipient, compared
/// case-insensitively; tests running beside each other use recipients no other test uses.
/// </remarks>
public sealed class RecordingEmailTransport : IEmailTransport
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RecordedEmail>> _sent = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task SendAsync(EmailSettings settings, EmailMessage message, CancellationToken cancellationToken = default)
    {
        var snapshot = new EmailSettings
        {
            SmtpServer = settings.SmtpServer,
            SmtpPort = settings.SmtpPort,
            SmtpUsername = settings.SmtpUsername,
            SmtpPassword = settings.SmtpPassword,
            SenderEmail = settings.SenderEmail,
            SenderName = settings.SenderName
        };

        _sent.GetOrAdd(message.To, _ => new ConcurrentQueue<RecordedEmail>()).Enqueue(new RecordedEmail(snapshot, message));
        return Task.CompletedTask;
    }

    /// <summary>Every message recorded for <paramref name="recipient"/>, in the order they were sent.</summary>
    public IReadOnlyList<RecordedEmail> SentTo(string recipient)
        => _sent.TryGetValue(recipient, out var messages) ? [.. messages] : [];
}

/// <summary>One message the <see cref="RecordingEmailTransport"/> was asked to deliver, and the settings it was handed.</summary>
/// <param name="Settings">A copy of the settings the send resolved.</param>
/// <param name="Message">The message.</param>
public sealed record RecordedEmail(EmailSettings Settings, EmailMessage Message);
