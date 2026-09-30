---
scope: tenant
learned: 2026-09-30
task: 02-email-setting-with-configuration-default-and-secrets
---

# Never let an anonymous endpoint act on the caller's tenant

TenantContextProcessor resolves the session's tenant even on AllowAnonymous endpoints, so a signed-in caller of forget-password or resend-verify carries their own tenant scope. Anything account-level (account mail through IEmailBackgroundJobs.EnqueueForPlatform, per-tenant settings) must name the platform explicitly; otherwise a tenant admin can route another account's reset link through their own tenant's SMTP settings.
