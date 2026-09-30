namespace Backend.External.Email;

using Backend.Features.Settings.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Sends transactional email on behalf of the application, through the SMTP server the
/// <see cref="EmailSettings"/> resolved for the tenant it is sent for describe.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a single email message with the <see cref="EmailSettings"/> of the acting scope - the tenant
    /// the request or job acts in, or the platform in platform scope or when no scope is established.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = false);

    /// <summary>
    /// Sends a single email message with the <see cref="EmailSettings"/> of an explicit target - what a
    /// background job calls, since a job runs in no scope and carries its tenant as an argument.
    /// </summary>
    /// <param name="tenantId">The tenant whose settings apply, or <see langword="null"/> for the platform's.</param>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    Task SendEmailAsync(Guid? tenantId, string to, string subject, string body, bool isHtml = false);
}

/// <summary>
/// Default <see cref="IEmailService"/> implementation: resolves <see cref="EmailSettings"/> at send
/// time - so a job retried after a settings change uses the settings standing when it runs - and hands
/// the message to <see cref="IEmailTransport"/>.
/// </summary>
/// <remarks>
/// The scope-less overload falls back to the platform's settings when no scope has been established,
/// rather than refusing: a job enqueued through it before the tenant became a job argument runs in a
/// Hangfire worker with no scope, and must still send.
/// </remarks>
[NoDirectUse]
public class EmailService(ISettingProvider settingProvider, IEmailTransport transport, ITenantContext tenantContext) : IEmailService
{
    /// <inheritdoc/>
    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = false)
        => SendEmailAsync(tenantContext.IsResolved ? tenantContext.CurrentTenantId : null, to, subject, body, isHtml);

    /// <inheritdoc/>
    public async Task SendEmailAsync(Guid? tenantId, string to, string subject, string body, bool isHtml = false)
    {
        var settings = await settingProvider.GetAsync<EmailSettings>(tenantId);
        await transport.SendAsync(settings, new EmailMessage(to, subject, body, isHtml));
    }
}
