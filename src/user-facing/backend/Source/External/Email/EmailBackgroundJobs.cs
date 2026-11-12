using Hangfire;

namespace Backend.External.Email;

/// <summary>
/// Enqueues outgoing email messages onto the Hangfire job queue so that
/// delivery happens asynchronously off the request thread.
/// </summary>
public interface IEmailBackgroundJobs
{
    /// <summary>
    /// Schedules an email for asynchronous delivery via Hangfire, sent with the
    /// <see cref="EmailSettings"/> standing when the job runs.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email body content.</param>
    /// <param name="isHtml">Indicates whether the body is HTML; defaults to plain text.</param>
    /// <returns>The Hangfire job id.</returns>
    string Enqueue(string to, string subject, string body, bool isHtml = false);
}

/// <summary>
/// Default <see cref="IEmailBackgroundJobs"/> implementation that delegates to Hangfire.
/// </summary>
[NoDirectUse]
public class EmailBackgroundJobs : IEmailBackgroundJobs
{
    /// <inheritdoc/>
    public string Enqueue(string to, string subject, string body, bool isHtml = false)
        => BackgroundJob.Enqueue<IEmailService>(service => service.SendEmailAsync(to, subject, body, isHtml));
}
