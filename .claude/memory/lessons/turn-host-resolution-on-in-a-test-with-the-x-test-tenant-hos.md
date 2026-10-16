---
scope: host
learned: 2026-10-16
---

# Turn host resolution on in a test with the X-Test-Tenant-Hosting header

The Testing host forces Web:TenantOriginTemplate empty, so every request resolves to None and a second AppFixture would seed and drop the shared database. Tests/Fakes/TenantHostingOverride resolves against app.tenant-hosting.test / {tenant}.tenant-hosting.test only for requests carrying X-Test-Tenant-Hosting: on; send it with a Host header (HubProbe takes a configureRequest for the hub) inside [Collection("TenantHosting")].
