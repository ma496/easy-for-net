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
}
```

To add one: put the work in a feature service (`I<Name>CleanService` / `I<Name>Job` with a single
`Task` method), register it in that feature's `AddServices`, then add an `AddOrUpdate` line with a
stable kebab-case job id. The id is the update key — changing it leaves the old job scheduled. A
recurring job has no tenant, so it follows the sweep rule above.

## Email

`IEmailService.SendEmailAsync(to, subject, body, isHtml)` sends over SMTP configured by the
`EmailSettings` section (`SmtpServer`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `SenderEmail`,
`SenderName`). **Endpoints should enqueue rather than send**, so a slow or unreachable SMTP server
cannot stall or fail a request — sign-up, resend-verification and forgot-password all use
`IEmailBackgroundJobs.Enqueue`.

`EmailService` and `EmailBackgroundJobs` are `[NoDirectUse]` and registered in `Program.cs`: depend on
the interfaces.

## Testing

No worker runs under `Testing`, so an enqueued job is stored and never executed, and the test host
replaces `IEmailService` with a no-op. Test the endpoint's own effects, and test the job's service
method by resolving it and calling it directly (opening the tenant scope it would open).

## Local development

The Hangfire server runs in-process with the API, so jobs execute as soon as `dotnet run` is up.
Watch `/hangfire` (signed in as a platform account) for failures and retries; a job that keeps
failing there is usually a serialization problem (a captured non-serializable argument), a missing
DI registration, or a tenant scope that was never established.

Configure SMTP in `appsettings.Development.json` before expecting mail to arrive — the template
ships placeholder credentials.

## Checklist

- [ ] Work wrapped in an interface-backed service, not called inline in the endpoint
- [ ] Job expression takes serializable arguments only, no captured scoped state
- [ ] Tenant id passed as an argument and `BeginTenant` opened in the job, or `.AcrossAllTenants()` for a sweep
- [ ] Job is idempotent under retry
- [ ] Recurring jobs registered in `Program.cs` with a stable id and a `Cron.*` schedule
- [ ] Service registered in the owning feature's `AddServices`
