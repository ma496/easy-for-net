---
scope: always
learned: 2026-09-29
task: 
---

# Check for a running Backend.exe before blaming the build

If the gate fails at dotnet build with MSB3027/MSB3021 'file is locked by Backend (PID)', a dotnet run of src/backend/Source (often a developer's VS Code terminal) holds bin/Debug and also occupies :5000 for the smoke check. Identify it (Get-CimInstance Win32_Process for its command line), stop it, and rerun verify with --autostart; building to a different OutputPath only hides the problem from the gate.
