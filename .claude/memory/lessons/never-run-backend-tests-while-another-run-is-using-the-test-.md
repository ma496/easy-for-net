---
scope: always
learned: 2026-09-29
---

# Never run backend tests while another run is using the test database

Every dotnet test run of Backend.Tests shares one PostgreSQL test database, and App.TearDownAsync drops it at the end. A reviewer running dotnet test while npm run verify is running makes about 100 unrelated tests fail at seeding or teardown. Run verify only once no reviewer is running tests, and tell parallel reviewers to read, not run, the suite.
