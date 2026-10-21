---
scope: tenants endpoint
learned: 2026-10-16
---

# Register every new Tenancy endpoint in TenantPermissionTests

TenantPermissionTests.Every_Tenant_Endpoint_Declares_Its_Permission reflects over every endpoint type in the Tenancy slice and fails unless each one is in its _declaredPermissions map, anonymous ones included (as an empty array). Add the new endpoint there with its permission, or [] and a reason, when you add it; the narrow tests for the endpoint itself will not catch the omission.
