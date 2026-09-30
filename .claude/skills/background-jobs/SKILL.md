---
name: background-jobs
description: Run work off the request thread with Hangfire — fire-and-forget enqueues, recurring jobs registered in Program.cs, establishing the tenant scope a job acts in, the email-sending pattern, the platform-only dashboard, and what runs under Testing. Use when a request should not wait for slow work (email, cleanup, external calls) or when adding a scheduled task.
---

# Background jobs

Hangfire is configured in `Program.cs` with PostgreSQL storage (`Hangfire:Storage:ConnectionString`,
falling back to `ConnectionStrings:DefaultConnection`). The worker is registered with
`AddHangfireServer()` in every environment **except `Testing`** — storage, the dashboard and the
recurring-job registrations stay, but nothing is executed during a test run. The dashboard is served
at `/hangfire`, guarded by `HangfireAuthorizationFilter`, which admits only a platform-tier account
(the `is_platform` claim) — never a role name or a permission.

## Fire-and-forget

Do not call `BackgroundJob.Enqueue` from an endpoint. Wrap it in a small service so the endpoint
depends on an interface and the job expression stays in one place — `EmailBackgroundJobs`
(`External/Email`) is the model:

```csharp
namespace Backend.External.Email;

using Backend.Features.Tenancy.Core;
using Hangfire;

/// <summary>
/// Enqueues outgoing email messages onto the Hangfire job queue so that
/// delivery happens asynchronously off the request thread.
/// </summary>
public interface IEmailBackgroundJobs
{
    string Enqueue(string to, string subject, string body, bool isHtml = false);
    string EnqueueForPlatform(string to, string subject, string body, bool isHtml = false);
}

/// <summary>Default <see cref="IEmailBackgroundJobs"/> implementation…</summary>
[NoDirectUse]
public class EmailBackgroundJobs(ITenantContext tenantContext) : IEmailBackgroundJobs
{
    public string Enqueue(string to, string subject, string body, bool isHtml = false)
        => EnqueueFor(tenantContext.IsResolved ? tenantContext.CurrentTenantId : null, to, subject, body, isHtml);

    public string EnqueueForPlatform(string to, string subject, string body, bool isHtml = false)
        => EnqueueFor(null, to, subject, body, isHtml);

    private static string EnqueueFor(Guid? tenantId, string to, string subject, string body, bool isHtml)
        => BackgroundJob.Enqueue<IEmailService>(service => service.SendEmailAsync(tenantId, to, subject, body, isHtml));
}
```

The endpoint then just calls it and returns immediately:

```csharp
emailBackgroundJobs.EnqueueForPlatform(user.Email, "Reset Password", body);
```

Rules for the job expression:

- Reference an **interface** (`Enqueue<IEmailService>(service => service.SendEmailAsync(...))`);
  Hangfire resolves a fresh scope per execution, so never capture a `DbContext`, an `HttpContext`, or
  the current user.
- Pass **serializable arguments only** — ids and primitives, never entities. Re-load what you need
  inside the job.
- Jobs are retried, so make them idempotent: re-running must not double-charge, double-send or
  duplicate rows.

## Tenant scope inside a job

A job runs outside any request, so no `TenantContextProcessor` has run and its `ITenantContext`
starts **unresolved**. Any read or write of a tenant-scoped entity (`IMayHaveTenant`/`IHaveTenant`),
and `IFeatureChecker`, then throws `TenantScopeNotEstablishedException` rather than silently reading
every tenant's rows or none. So:

- **Work for one tenant:** capture `tenantContext.CurrentTenantId` when enqueuing, pass it as a job
  argument, and open the scope first thing in the job:

  ```csharp
  public async Task DoAsync(Guid tenantId)
  {
      using var tenantScope = tenantContext.BeginTenant(tenantId);
      // tenant-scoped queries, saves and IFeatureChecker now act for tenantId
  }
  ```

- **Platform-owned rows** (no tenant): `using var _ = tenantContext.BeginPlatformScope();`.
- **A sweep across every tenant** (cleanup): query with `.AcrossAllTenants()`, as
  `AuthTokenCleanService` does — it relaxes only the tenant filter, soft delete stays in force.
  Per-tenant work inside a sweep opens `BeginTenant` per tenant, disposing each handle before the
  next; never share one context between concurrent branches.

Entities with no tenant marker (e.g. `Token`) need none of this. See the `multi-tenancy` skill for
the scope model itself.

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
covers, in batches of hand-written SQL across every tenant (see the `notifications` skill). A job method
taking a `CancellationToken` is registered with `CancellationToken.None` - an expression tree cannot omit an
optional argument - and Hangfire substitutes its own shutdown token when it runs.

To add one: put the work in a feature service (`I<Name>CleanService` / `I<Name>Job` with a single
`Task` method), register it in that feature's `AddServices`, then add an `AddOrUpdate` line with a
stable kebab-case job id. The id is the update key — changing it leaves the old job scheduled. A
recurring job has no tenant, so it follows the sweep rule above.

## Email

`IEmailService` sends over the SMTP server described by the `EmailSettings` **setting** (`Email`), read
through `ISettingProvider` like any other (see the `settings` skill): `SmtpServer`, `SmtpPort`,
`SmtpUsername`, `SmtpPassword` (a `[SecretSetting]`, stored encrypted and never inherited apart from
the server, port and user name), `SenderEmail`, `SenderName`. The `EmailSettings` configuration section
is only the **default**, bound once at startup; the platform overrides it for everyone and each tenant
for itself on `/admin/settings`, property by property.

**Endpoints should enqueue rather than send**, so a slow or unreachable SMTP server cannot stall or fail
a request. The tenant whose settings apply is **captured when the email is enqueued** and travels as a
job argument; the job calls `SendEmailAsync(tenantId, …)`, which resolves `GetAsync<EmailSettings>(tenantId)`
when it runs — so a retry after a settings change uses the settings standing then. Which enqueue to
call is a security decision, because a tenant's administrator controls that tenant's server:

- `EnqueueForPlatform` for mail about an **account** — sign-up and resend verification, password
  reset, anything carrying a credential or an access link. It always uses the platform's settings,
  whichever tenant the caller acts in (an anonymous endpoint still carries a signed-in caller's scope).
- `Enqueue` for a tenant's own content, sent on its behalf with the acting tenant's settings — the
  platform's when there is no tenant scope. A tenant administrator chooses that server and port, so
  the worker connects wherever they point it: before the first caller of `Enqueue`, hold the SMTP host
  to the server-side request forgery rule in the `settings` skill.

`EmailService`, `EmailBackgroundJobs` and `SmtpEmailTransport` (`IEmailTransport`, the part that talks
to the server) are `[NoDirectUse]` and registered in `Program.cs`: depend on the interfaces.

## Testing

No worker runs under `Testing`, so an enqueued job is stored and never executed, and the test host
replaces `IEmailTransport` with `RecordingEmailTransport`, which delivers nothing but records each
message with the settings it was sent with. Test the endpoint's own effects, and test the job's
service method by resolving it and calling it directly with the tenant id the job would carry (or
opening the tenant scope it would open).

## Local development

The Hangfire server runs in-process with the API, so jobs execute as soon as `dotnet run` is up.
Watch `/hangfire` (signed in as a platform account) for failures and retries; a job that keeps
failing there is usually a serialization problem (a captured non-serializable argument), a missing
DI registration, or a tenant scope that was never established.

Configure SMTP before expecting mail to arrive — as the `EmailSettings` section in
`appsettings.Development.json` (the default every scope inherits), or as the platform's override on
`/admin/settings`. The template ships placeholder credentials.

## Checklist

- [ ] Work wrapped in an interface-backed service, not called inline in the endpoint
- [ ] Job expression takes serializable arguments only, no captured scoped state
- [ ] Tenant id passed as an argument and `BeginTenant` opened in the job, or `.AcrossAllTenants()` for a sweep
- [ ] Job is idempotent under retry
- [ ] Recurring jobs registered in `Program.cs` with a stable id and a `Cron.*` schedule
- [ ] Service registered in the owning feature's `AddServices`
