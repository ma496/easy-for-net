---
scope: tests
learned: 2026-09-29
task: 
---

# Never change a seeded tenant's plan, members or lifecycle in a test

Plan changes (tenant edition, feature values, edition delete) and membership/lifecycle changes now revoke every session in the tenant, and every parallel test class signs in to the bootstrap tenant as tenantadmin, so doing one to a seeded tenant fails unrelated tests at random. Prove the rule on a tenant the test creates; if it must look system-created, mark it and clear the mark in a finally, inside the BootstrapTenant collection that TenantSeedingTests shares.
