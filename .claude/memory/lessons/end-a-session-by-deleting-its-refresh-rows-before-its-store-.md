---
scope: session
learned: 2026-09-29
task: 02-revoke-sessions-on-identity-access-changes
---

# End a session by deleting its refresh rows before its store record

Every path that ends a session (SessionRevocationService, signout, refresh, tenant switch/exit) deletes AuthToken rows first and the ISessionStore record second; AuthTokenService.RestampSessionAsync rewrites a kept record and relies on that order to avoid resurrecting a revoked one. New revocation code (e.g. Tenancy tasks) must go through ISessionRevocationService after commit, and any new issuance point must call TokenService.RecordSessionId with the SecurityStamp it was authorized under or PersistTokenAsync throws.
