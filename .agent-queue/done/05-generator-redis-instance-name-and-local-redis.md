Give generated projects their own Redis key prefix and a local Redis setup step
Depends-on: 01-redis-session-store-and-per-request-validation

After task 01, the API needs Redis outside `Testing` and prefixes every key with `Redis:InstanceName`. Each generated project needs its own prefix so that several applications can share one Redis server. The scaffolding guide also has to tell a new developer to start a Redis.

## Scope
- In `tool/EasyForNetTool/Generator/CreateProjectGenerator.cs`, next to the existing `ConnectionStrings.DefaultConnection` rewrites:
  - set `Redis.InstanceName` to `<Name>:` in `appsettings.json`, and in `appsettings.Development.json` if the generator rewrites that file;
  - set it to `<Name>Test:` in `appsettings.Testing.json`.
- Add a tool test in `tool/EasyForNetTool.Tests` that asserts the rewritten values.
- In `.claude/skills/new-project`, add a post-create step that starts a local Redis (for example `docker run -d -p 6379:6379 redis:7`) before the API is run. Say that the backend tests do not need Redis.

## Out of scope — do not touch
- Everything under `src/`. The session store, the configuration keys and `scripts/redis-ready.mjs` belong to task 01.
- `CLAUDE.md`, `tool/EasyForNetTool/new-project-claude.md`, and the `multi-tenancy`, `permissions` and `feature-management` skills. Task 06 owns them.
- A docker-compose file or any new root-level file. The generator's list of copied root files stays the same.

## Done when
- `npm run verify` passes, and so does `dotnet test tool/EasyForNetTool.Tests/EasyForNetTool.Tests.csproj`.
- A project generated locally with `efn cp -n Demo`:
  - has `Redis:InstanceName` set to `Demo:` in `appsettings.json`;
  - has it set to `DemoTest:` in `appsettings.Testing.json`;
  - builds.
- Following the `new-project` skill's steps in order brings up an API that a user can sign in to, using a local Redis.
