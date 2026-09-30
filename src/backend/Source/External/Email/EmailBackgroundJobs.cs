namespace Backend.External.Email;

using Backend.Features.Tenancy.Core;
using Hangfire;

/// <summary>
/// Enqueues outgoing email messages onto the Hangfire job queue so that
/// delivery happens asynchronously off the request thread.
/// </summary>
/// <remarks>
/// Which <see cref="EmailSettings"/> a message is sent with is decided here, when it is enqueued, and
/// the choice is a security decision: a tenant's administrator controls that tenant's SMTP server.
/// <list type="bullet">
/// <item><see cref="EnqueueForPlatform"/> for mail about an <b>account</b> - sign-up verification,
/// resend verification, password reset, anything carrying a credential or a link that grants access.
/// It always uses the platform's settings, so no tenant administrator can route another account's
/// reset link through a server they control, whichever tenant the caller happens to act in.</item>
/// <item><see cref="Enqueue"/> for a tenant's own content, sent on that tenant's behalf. It uses the
/// settings of the tenant the caller acts in.</item>
/// </list>
/// </remarks>
public interface IEmailBackgroundJobs
{
    /// <summary>
    /// Schedules tenant mail for asynchronous delivery via Hangfire, sent with the
    /// <see cref="EmailSettings"/> of the scope it is enqueued in: the acting tenant, or the platform's in
    /// platform scope or when no scope has been established. Never use it for account mail - see
    /// <see cref="EnqueueForPlatform"/>.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    /// <returns>The Hangfire job id.</returns>
    string Enqueue(string to, string subject, string body, bool isHtml = false);

    /// <summary>
    /// Schedules account mail for asynchronous delivery via Hangfire, always sent with the platform's
    /// <see cref="EmailSettings"/> whatever scope the caller acts in.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    /// <returns>The Hangfire job id.</returns>
    string EnqueueForPlatform(string to, string subject, string body, bool isHtml = false);
}

/// <summary>
/// Default <see cref="IEmailBackgroundJobs"/> implementation that delegates to Hangfire. The tenant is
/// captured when the message is enqueued and travels as a job argument, because the job runs outside
/// any request, with no tenant scope of its own.
/// </summary>
[NoDirectUse]
public class EmailBackgroundJobs(ITenantContext tenantContext) : IEmailBackgroundJobs
{
    /// <inheritdoc/>
    public string Enqueue(string to, string subject, string body, bool isHtml = false)
        => EnqueueFor(tenantContext.IsResolved ? tenantContext.CurrentTenantId : null, to, subject, body, isHtml);

    /// <inheritdoc/>
    public string EnqueueForPlatform(string to, string subject, string body, bool isHtml = false)
        => EnqueueFor(null, to, subject, body, isHtml);

    private static string EnqueueFor(Guid? tenantId, string to, string subject, string body, bool isHtml)
        => BackgroundJob.Enqueue<IEmailService>(service => service.SendEmailAsync(tenantId, to, subject, body, isHtml));
}
