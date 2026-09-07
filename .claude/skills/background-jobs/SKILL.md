---
name: background-jobs
description: Run work off the request thread with Hangfire — fire-and-forget enqueues, recurring jobs registered in Program.cs, the email-sending pattern, and the dashboard. Use when a request should not wait for slow work (email, cleanup, external calls) or when adding a scheduled task.
---

# Background jobs

Hangfire is configured in `Program.cs` with PostgreSQL storage (`Hangfire:Storage:ConnectionString`,
falling back to `ConnectionStrings:DefaultConnection`) and a server registered via
`AddHangfireServer()`. The dashboard is served at `/hangfire`, guarded by
`HangfireAuthorizationFilter`.

## Fire-and-forget

Do not call `BackgroundJob.Enqueue` from an endpoint. Wrap it in a small service so the endpoint
depends on an interface and the job expression stays in one place — `EmailBackgroundJobs` is the
model:

```csharp
namespace Backend.External.Email;

using Backend.Attributes;
using Hangfire;

/// <summary>
/// Enqueues outgoing email messages onto the Hangfire job queue so that
/// delivery happens asynchronously off the request thread.
/// </summary>
public interface IEmailBackgroundJobs
{
    void Enqueue(string to, string subject, string body, bool isHtml = false);
}

/// <summary>Default <see cref="IEmailBackgroundJobs"/> implementation…</summary>
[NoDirectUse]
public class EmailBackgroundJobs(IEmailService emailService) : IEmailBackgroundJobs
{
    public void Enqueue(string to, string subject, string body, bool isHtml = false)
        => BackgroundJob.Enqueue(() => emailService.SendEmailAsync(to, subject, body, isHtml));
}
```

The endpoint then just calls it and returns immediately:

```csharp
emailBackgroundJobs.Enqueue(user.Email, "Reset Password", body);
```

Rules for the job expression:

- Reference an **interface** (`() => emailService.SendEmailAsync(...)`); Hangfire resolves a fresh
  scope per execution, so never capture a `DbContext`, an `HttpContext`, or the current user.
- Pass **serializable arguments only** — ids and primitives, never entities. Re-load what you need
  inside the job.
- Jobs are retried, so make them idempotent: re-running must not double-charge, double-send or
  duplicate rows.

## Recurring jobs

Registered at the end of `Program.cs`, after the database is ready:

```csharp
using (app.Services.CreateScope())
{
    RecurringJob.AddOrUpdate<IAuthTokenCleanService>("delete-expired-auth-tokens",
        service => service.DeleteExpiredTokensAsync(), Cron.Daily);
    RecurringJob.AddOrUpdate<ITokenCleanService>("delete-expired-tokens",
        service => service.DeleteExpiredTokensAsync(), Cron.Daily);
}
```

To add one: put the work in a feature service (`I<Name>CleanService` / `I<Name>Job` with a single
`Task DoAsync()`), register it in that feature's `AddServices`, then add an `AddOrUpdate` line with a
stable kebab-case job id. The id is the update key — changing it leaves the old job scheduled.

## Email

`IEmailService.SendEmailAsync(to, subject, body, isHtml)` sends synchronously over SMTP configured
by the `EmailSettings` section (`SmtpServer`, `SmtpPort`, credentials, sender). **Endpoints should
enqueue rather than send**, so a slow or unreachable SMTP server cannot stall or fail a request —
sign-up, resend-verification and forgot-password all use `IEmailBackgroundJobs.Enqueue`.

Both `EmailService` and `EmailBackgroundJobs` are `[NoDirectUse]`: depend on the interfaces.

## Local development

The Hangfire server runs in-process with the API, so jobs execute as soon as `dotnet run` is up.
Watch `/hangfire` for failures and retries; a job that keeps failing there is usually a
serialization problem (a captured non-serializable argument) or a missing DI registration.

Configure SMTP in `appsettings.Development.json` before expecting mail to arrive — the template
ships placeholder credentials.

## Checklist

- [ ] Work wrapped in an interface-backed service, not called inline in the endpoint
- [ ] Job expression takes serializable arguments only, no captured scoped state
- [ ] Job is idempotent under retry
- [ ] Recurring jobs registered in `Program.cs` with a stable id and a `Cron.*` schedule
- [ ] Service registered in the owning feature's `AddServices`
