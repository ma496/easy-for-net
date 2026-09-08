---
name: spec-driven
description: Run a feature through the spec-driven loop — /specify writes EARS acceptance criteria into specs/<NNN-slug>/spec.md, /plan turns them into plan.md plus contracts and tasks.md, /implement executes the tasks, /verify traces every criterion back to code and tests. Use when starting a feature big enough to be worth specifying, and whenever you need the document templates, the gate rules or the tasks.md format.
---

# Spec-driven development

A specification is the unit of work handed to an agent; the code is the artifact measured against it.
Each stage is one dynamic workflow that fans out across many subagents, verifies its own output
adversarially, and hands you a document to read before the next stage runs.

**When not to use this.** One full loop is roughly 200 subagent calls. A one-line fix, a rename, a
typo, a dependency bump — do those directly. Reach for the loop when the work spans both stacks, adds
a permission or an entity, or is something you would want to be able to prove was built correctly.

## The loop

| Command | Workflow | Reads | Writes | Gate before moving on |
| --- | --- | --- | --- | --- |
| `/specify <request>` | `spec-specify` | the request, the codebase | `spec.md`, the spec directory | Answer the blocking questions it returns |
| `/plan [NNN-slug]` | `spec-plan` | `spec.md` | `plan.md`, four contracts, `tasks.md` | Read `plan.md`; check the coverage table |
| `/implement [NNN-slug]` | `spec-implement` | `tasks.md` | application code, `implementation.md` | Read the diff |
| `/verify [NNN-slug]` | `spec-verify` | `spec.md` + the code | `verification.md`, a new task round | Converged, or loop back to `/implement` |

The stages are separate runs on purpose: **a workflow cannot ask you a question while it runs** — it
can only return one. Every point where a human decision belongs is a stage boundary.

`/verify` is a convergence loop, not a report. When it finds unmet criteria it appends them to
`tasks.md` as a new numbered round; run `/implement` again on just those ids, then `/verify` again.
Stop when it returns converged.

## Artifacts

Everything for one feature lives in `specs/<NNN-slug>/` at the repository root, committed to git:

```
specs/007-user-csv-export/
  spec.md               what and why - no implementation detail
  plan.md               the chosen approach and why it beat the alternatives
  data-model.md         entities, keys, interfaces, the migration to run
  api-contract.md       endpoints, validators, permissions, error codes
  frontend-contract.md  RTK Query endpoints, DTOs, routes, components, translation keys
  test-plan.md          what is tested where, and what proves each criterion
  tasks.md              the ordered work list - the only file three stages share
  implementation.md     what landed, what failed, follow-ups
  verification.md       the traceability matrix
```

`AC-nnn` and `T-nnn` ids are minted once and **never renumbered**. They are the traceability key:
`tasks.md` cites `AC-nnn`, `verification.md` reports a verdict per `AC-nnn`, and a task citing no
criterion is scope creep by definition. Amend text in place; append new ids at the end.

## Acceptance criteria use EARS

Every criterion is one testable behaviour in one of the five EARS forms, on its own line with a
stable id:

```markdown
- **AC-001** The system shall record the exporting user and the export time on every export.
- **AC-002** When an administrator requests an export, the system shall return the rows the current
  filter selects, in the current sort order.
- **AC-003** While an export is in progress, the system shall reject a second export from the same user.
- **AC-004** Where the project ships more than one locale, the system shall write column headers in
  the caller's locale.
- **AC-005** If the caller lacks the export permission, then the system shall reject the request and
  return a defined error code.
```

Ubiquitous (*The system shall*), event-driven (*When … the system shall*), state-driven (*While …*),
optional-feature (*Where …*), unwanted (*If … then the system shall*). Use "shall", never "should"
or "may". **No class names, file paths or library names inside a criterion** — that is the plan's
job. Every failure path gets its own *If … then …* criterion; a spec whose criteria are all happy
paths is not finished.

## tasks.md format

One task per line, parsed by `spec-implement`. Keep the format exactly:

```markdown
- [ ] **T-004** [P] Add the export permission constant and definition - `files:` src/backend/Source/Permissions/Allow.cs, src/backend/Source/Features/Identity/Core/IdentityPermissionsProvider.cs - `skill:` permissions - `acs:` AC-005 - `after:` T-001
```

- `[P]` means the task may run beside its siblings; omit it when it must run alone.
- `files:` is a hard boundary — the implementing agent may edit nothing else, because other agents
  are editing other files at the same moment.
- `skill:` names the guide under `.claude/skills/` that governs the task.
- `after:` lists task ids that must finish first.

`spec-implement` schedules tasks into waves whose file sets do not intersect. It also treats a set of
**hotspot files** as shared even when the declared lists differ, because two features appending to
them merge cleanly and then fail to compile: `AppDbContext.cs`, `Allow.cs`, `Meta.cs`, `Program.cs`,
`ErrorCodes.cs`, `allow.ts`, `auth-urls.ts`, `nav-items.ts`, `searchable-items.ts`, barrel
`index.ts` files, and every `public/locales/*.json`. Give each of those a single owning task that the
others declare in `after:`.

## The completeness checklist

Specs and plans in this codebase are incomplete until each of these is either covered or explicitly
ruled out. The critics in `spec-specify` and `spec-verify` work from this list:

- **Permission** — a constant in `Backend.Permissions.Allow`, a definition in the owning feature's
  `IPermissionDefinitionProvider`, `Permissions(Allow.X)` on the endpoint, and the mirrored entry in
  `src/frontend/web/allow.ts` plus `auth-urls.ts` and `nav-items.ts` where a screen is gated. See the
  `permissions` skill.
- **Entity** — base class and the audit / `ISoftDelete` / normalized-property interfaces, an
  `IEntityTypeConfiguration`, the `DbSet` on `AppDbContext`, and a migration. See `backend-entity`.
- **Error path** — a code in `Backend.ErrorHandling.ErrorCodes`, `ThrowError(message, ErrorCodes.X)`
  at the raise site, and an `error.server.*` translation key. See `api-error-handling`.
- **Localization** — no hard-coded user-visible string; every key added to
  `public/locales/en.json` and to every other locale the project ships. See `localization`.
- **Feature isolation** — nothing under `Backend.Features.X` may reference `Backend.Features.Y`
  unless the target is `[AllowOutside]`, or `FeatureDependencyTests` fails. See `backend-feature`.
- **Lists** — sortable fields whitelisted in the request validator, paging via `ListRequestDto<TId>`
  and `IQueryableExtension.Process`. See `backend-endpoint`.
- **Tests** — an integration test per endpoint behaviour including its failure branch, and no
  dependence on global database state the test did not create. See `backend-tests`.

## Running the workflows directly

The slash commands are thin wrappers; the workflows take these arguments:

```
spec-specify   {request, slug?, specDir?}
spec-plan      {specDir, focus?, approaches?}
spec-implement {specDir, tasks?, maxParallel?, gateEveryWave?}
spec-verify    {specDir, refuters?, autoFix?}
```

Pass `specDir` (`specs/007-user-csv-export`) rather than letting a workflow guess — no workflow ever
looks for "the latest spec". Run `/specify` one at a time: directory numbering is allocated by
reading the directory, so two concurrent runs would claim the same number.

Every script scales its depth from the token budget you set for the turn, and logs anything it drops
so a truncated run never reads like a complete one.

## Editing the workflow scripts

`.claude/workflows/*.js` are dynamic-workflow scripts, so: plain JavaScript with no TypeScript
syntax, a pure-literal `export const meta` at the top, and no `Date.now()`, `Math.random()`, argless
`new Date()`, `require` or filesystem access in the orchestrator — those break resume. The script
never touches disk; its subagents do.

**Comment as you go.** Each script opens with a header block stating what the stage does, its
`args` contract, what it returns, which files its agents write and its rough agent count, and every
non-obvious decision carries an inline note — why a `parallel()` barrier is used where a `pipeline()`
would not do, how the wave scheduler decides two tasks may run together, how majority voting resolves
adversarial verification. Keep that up: the next person to open these files will not have the context
you have now.

**The scripts must stay free of namespaces.** They ship into generated projects byte-for-byte —
only markdown under `.claude` is rewritten — so a namespace-qualified type name, a solution file name
or a project file name in a `.js` file would arrive wrong and stay wrong. Keep repo-specific detail in
this file, and let the scripts point agents at it and at `CLAUDE.md`.

## Checklist

- [ ] `spec.md` criteria are all in EARS form, each one testable, each failure path its own criterion
- [ ] Every `AC-nnn` appears in at least one task, and every task cites at least one `AC-nnn`
- [ ] Tasks that touch a hotspot file have a single owner, with the others sequenced behind it
- [ ] The completeness checklist above is covered or explicitly ruled out in `spec.md`
- [ ] `/verify` returns converged before the feature is called done
- [ ] `dotnet build EasyForNet.slnx`, `dotnet test src/backend/Tests/Backend.Tests.csproj` and
      `npm run lint` pass
