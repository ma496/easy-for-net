---
name: background-jobs
description: Run work off the request thread with Hangfire — fire-and-forget enqueues, recurring jobs registered in Program.cs, what a job may and may not capture, the email-sending pattern, the permission-guarded dashboard, and what runs under Testing. Use when a request should not wait for slow work (email, cleanup, external calls) or when adding a scheduled task.
---

# Background jobs

Hangfire is configured in `Program.cs` with PostgreSQL storage (`Hangfire:Storage:ConnectionString`,
falling back to `ConnectionStrings:DefaultConnection`). The worker is registered with
`AddHangfireServer()` in every environment **except `Testing`** — storage, the dashboard and the
recurring-job registrations stay, but nothing is executed during a test run. The dashboard is served
at `/hangfire`, guarded by `HangfireAuthorizationFilter`, which admits only an authenticated caller
whose session holds the `Hangfire.Dashboard` permission (`Allow.Hangfire_Dashboard`) — a permission,
never a role name, so a revoked grant reaches the dashboard as it reaches every endpoint.

## Fire-and-forget

Do not call `BackgroundJob.Enqueue` from an endpoint. Wrap it in a small service so the endpoint
depends on an interface and the job expression stays in one place — `EmailBackgroundJobs`
(`External/Email`) is the model:

```csharp
using Hangfire;

namespace Backend.External.Email;

/// <summary>
/// Enqueues outgoing email messages onto the Hangfire job queue so that
/// delivery happens asynchronously off the request thread.
/// </summary>
public interface IEmailBackgroundJobs
{
    string Enqueue(string to, string subject, string body, bool isHtml = false);
}

/// <summary>Default <see cref="IEmailBackgroundJobs"/> implementation that delegates to Hangfire.</summary>
[NoDirectUse]
public class EmailBackgroundJobs : IEmailBackgroundJobs
{
    public string Enqueue(string to, string subject, string body, bool isHtml = false)
        => BackgroundJob.Enqueue<IEmailService>(service => service.SendEmailAsync(to, subject, body, isHtml));
}
```

The endpoint then just calls it and returns immediately:

```csharp
emailBackgroundJobs.Enqueue(user.Email, "Reset Password", body);
```

Rules for the job expression:

- Reference an **interface** (`Enqueue<IEmailService>(service => service.SendEmailAsync(...))`);
  Hangfire resolves a fresh scope per execution, so never capture a `DbContext`, an `HttpContext`, or
  the current user.
- Pass **serializable arguments only** — ids and primitives, never entities. Re-load what you need
  inside the job.
- Never pass a secret, a resolved setting or a token as an argument — Hangfire stores arguments
  unencrypted and shows them on its dashboard. Resolve settings inside the job.
- Jobs are retried, so make them idempotent: re-running must not double-charge, double-send or
  duplicate rows.

## Who a job acts for

A job runs outside any request, so there is no signed-in caller: `ICurrentUserService` reports no
user, and audit fields the job saves carry none. When the work is about one account, capture its id
when enqueuing, pass it as an argument, and load the account in the job. A job is not authorized by
anyone's permissions — decide in the endpoint, before enqueuing, whether the caller may cause it.

## Recurring jobs

Registered at the end of `Program.cs`, after the database is ready:

```csharp
using (app.Services.CreateScope())
{
    RecurringJob.AddOrUpdate<IAuthTokenCleanService>("delete-expired-auth-tokens", service => service.DeleteExpiredTokensAsync(), Cron.Daily);
    RecurringJob.AddOrUpdate<ITokenCleanService>("delete-expired-tokens", service => service.DeleteExpiredTokensAsync(), Cron.Daily);
    RecurringJob.AddOrUpdate<INotificationRetentionService>("delete-expired-notifications", service => service.DeleteExpiredAsync(CancellationToken.None), Cron.Daily);
}
```

`delete-expired-notifications` hard-deletes notifications older than `Notifications:RetentionDays`
(`NotificationOptions`, default 90, validated on start) and prunes read visit rows a read cursor already
covers, in batches of hand-written SQL (see the `notifications` skill). A job method taking a
`CancellationToken` is registered with `CancellationToken.None` - an expression tree cannot omit an
optional argument - and Hangfire substitutes its own shutdown token when it runs.

To add one: put the work in a feature service (`I<Name>CleanService` / `I<Name>Job` with a single
`Task` method), register it in that feature's `AddServices`, then add an `AddOrUpdate` line with a
stable kebab-case job id. The id is the update key — changing it leaves the old job scheduled. A
cleanup deletes in bulk with `ExecuteDeleteAsync` over a predicate (`AuthTokenCleanService`), never
by loading rows.

## Email

`IEmailService` sends over the SMTP server described by the `EmailSettings` **setting** (`Email`), read
through `ISettingProvider` like any other (see the `settings` skill): `SmtpServer`, `SmtpPort`,
`SmtpUsername`, `SmtpPassword` (a `[SecretSetting]`, stored encrypted and never inherited apart from
the server, port and user name), `SenderEmail`, `SenderName`. The `EmailSettings` configuration section
is only the **default**, bound once at startup; an administrator overrides it on `/admin/settings`,
property by property.

**Endpoints should enqueue rather than send**, so a slow or unreachable SMTP server cannot stall or fail
a request. The job resolves `GetAsync<EmailSettings>()` when it runs — so a retry after a settings
change uses the settings standing then. Links in a message are built from the site origin
(`WebSetting.SiteOrigin()`, `Web:DefaultDomain`), never from the request's host.

`EmailService`, `EmailBackgroundJobs` and `SmtpEmailTransport` (`IEmailTransport`, the part that talks
to the server) are `[NoDirectUse]` and registered in `Program.cs`: depend on the interfaces.

## Testing

No worker runs under `Testing`, so an enqueued job is stored and never executed, and the test host
replaces `IEmailTransport` with `RecordingEmailTransport`, which delivers nothing but records each
message with the settings it was sent with. Test the endpoint's own effects, and test the job's
service method by resolving it and calling it directly with the arguments the job would carry.
`HangfireDashboardTests` and `HangfireAuthorizationFilterTests` pin the dashboard's permission gate.

## Local development

The Hangfire server runs in-process with the API, so jobs execute as soon as `dotnet run` is up.
Watch `/hangfire` (signed in as an account holding `Hangfire.Dashboard` — the seeded `admin`) for
failures and retries; a job that keeps failing there is usually a serialization problem (a captured
non-serializable argument) or a missing DI registration.

Configure SMTP before expecting mail to arrive — as the `EmailSettings` section in
`appsettings.Development.json` (the default), or as an override on `/admin/settings`. The
`EmailSettings` section in `appsettings.json` holds only placeholder credentials.

## Checklist

- [ ] Work wrapped in an interface-backed service, not called inline in the endpoint
- [ ] Job expression takes serializable arguments only, no captured scoped state and no secrets
- [ ] Authorization decided in the endpoint before enqueuing
- [ ] Job is idempotent under retry
- [ ] Recurring jobs registered in `Program.cs` with a stable id and a `Cron.*` schedule
- [ ] Service registered in the owning feature's `AddServices`
