---
scope: userupdateendpoint
learned: 2026-09-29
task: 
---

# Only platform scope can edit an account shared across tenants

GuardSharedAccountAsync refuses any update, role change included, to an account that reaches beyond the acting tenant with 400 UserSharedAcrossTenants, unless the caller is a platform account acting in no tenant; entering the tenant as a platform member does not help. A test that changes a dual-tenant user's roles from inside one tenant cannot be written; arrange it from platform scope or use a single-tenant account.
