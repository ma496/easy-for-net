# Document that sessions are validated on every request and revoked when access changes

| | |
|---|---|
| **Commit** | `76c0ba6c` |
| **Landed** | 2026-09-29 |
| **Task brief** | `06-document-session-validation-and-revocation.md` |
| **Verification** | `npm run verify` — the static gate, plus whichever live checks the diff demanded |

## What changed

| File | + | − |
|------|---|---|
| `.agent-queue/{todo => doing}/06-document-session-validation-and-revocation.md` | 0 | 0 |
| `.agent-queue/{doing => done}/05-generator-redis-instance-name-and-local-redis.md` | 0 | 0 |
| `.claude/agents/security-reviewer.md` | 8 | 3 |
| `.claude/memory/lessons/check-that-a-salvage-brief-s-tree-holds-the-task-s-own-work.md` | 9 | 0 |
| `.claude/memory/lessons/put-tests-that-read-the-default-tenant-s-languages-in-the-lo.md` | 9 | 0 |
| `.claude/skills/backend-tests/SKILL.md` | 8 | 3 |
| `.claude/skills/feature-management/SKILL.md` | 30 | 13 |
| `.claude/skills/multi-tenancy/SKILL.md` | 15 | 5 |
| `.claude/skills/permissions/SKILL.md` | 22 | 10 |
| `CLAUDE.md` | 39 | 16 |
| `docs/builds/05-generator-redis-instance-name-and-local-redis.md` | 52 | 0 |
| `docs/builds/README.md` | 2 | 1 |
| `src/backend/Source/Program.cs` | 9 | 8 |
| `tool/EasyForNetTool/new-project-claude.md` | 39 | 16 |

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

Depends-on: 01-redis-session-store-and-per-request-validation, 02-revoke-sessions-on-identity-access-changes, 03-revoke-sessions-on-tenant-membership-and-lifecycle, 04-revoke-sessions-on-plan-changes

The agent-facing docs still say that authority is "decided once, when a token is minted" and changes only "at the next token renewal". After tasks 01–04 that is no longer true:
- a token carries only the user and `sid`;
- the session is read from the store on every request;
- any access change revokes the affected sessions at once.

This task rewrites every statement of the old rule so that later agents are not misled.

## Scope
- In `CLAUDE.md`, keeping the text operational:
  - **Auth**: the token holds `NameIdentifier` and `sid`, and the session lives in Redis (in memory under `Testing`). Describe the validation step and the claims projection, 401 versus 503 `sessionStoreUnavailable`, sign-out deleting the session, and the revocation table in brief.
  - **Permissions**: replace the mint-time rule.
  - **Features (entitlements)**: plan gating is "computed when the session is created, and the session is revoked when the plan changes".
- Mention `ConnectionStrings:Redis`, `Redis:InstanceName`, the startup guard and `scripts/redis-ready.mjs` wherever configuration and the verify loop are described.
- Make the same changes in `tool/EasyForNetTool/new-project-claude.md`.
- In the `multi-tenancy`, `permissions` and `feature-management` skills under `.claude/skills/`, rewrite every place that repeats the mint-time rule. Keep namespace references in the qualified `Backend.` form.
- In `Program.cs`, rewrite the comment on `SlidingExpiration = false` for the new rule. Change only the comment.

## Out of scope — do not touch
- Any code, apart from the one `Program.cs` comment.
- The `new-project` skill and `CreateProjectGenerator`. Task 05 owns them.
- The `template-maintenance` skill, unless it repeats the mint-time rule.

## Done when
- `npm run verify` passes.
- `grep -rn "decided once\|next token renewal\|mint-time" CLAUDE.md tool/EasyForNetTool/new-project-claude.md .claude/skills` finds no statement of the old rule.
- `CLAUDE.md` and `new-project-claude.md` describe the same session model.
