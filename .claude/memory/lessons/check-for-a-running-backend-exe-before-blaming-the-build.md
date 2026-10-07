---
scope: src/backend
learned: 2026-09-29
---

# Check for a running Backend.exe before blaming the build

If the gate fails at dotnet build with MSB3027/MSB3021 'file is locked by Backend (PID)', a dotnet run of src/backend/Source (often a developer's VS Code terminal) holds bin/Debug and also occupies :5000 for the smoke check. The gate now runs `npm run stop:api` before its build, and the backend agents run it before every dotnet build/test/ef; if the lock persists, run `npm run stop:api -- --list` to see what holds bin/, stop it, and rerun verify with --autostart; building to a different OutputPath only hides the problem from the gate.
