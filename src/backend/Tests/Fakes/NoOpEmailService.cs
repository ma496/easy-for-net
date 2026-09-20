namespace Backend.Tests.Fakes;

using Backend.External.Email;

/// <summary>
/// The <see cref="IEmailService"/> the test host uses, which accepts every message and sends nothing.
/// </summary>
/// <remarks>
/// The Testing environment's mail settings are placeholders, so a real send attempt reaches an SMTP
/// server that refuses it, and the endpoints that raise mail - signup, forgotten password, resend
/// verification - are exercised for what they write, never for what they post.
/// </remarks>
public class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = false) => Task.CompletedTask;
}
