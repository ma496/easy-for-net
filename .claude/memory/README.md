# .claude/memory — what the loop learned

Cross-run memory. Everything else the agents read is *static*: `CLAUDE.md` states the
conventions, a skill states a procedure, a brief states one task. None of it changes because
a run went badly. This directory is the part that does.

```
.claude/memory/
├── README.md        this file
└── lessons/
    └── <slug>.md    one lesson, one topic
```

## The contract

**Written** by a task that learned something, at the end of its run. The session that hit
the trap is the only party that knows what the trap was, so it records it:

```bash
node scripts/record-lesson.mjs --title "Anchor grep acceptance checks with word boundaries" --scope grep --body "…what to do instead, and why…"
```

One line, so the same command runs in bash and in PowerShell.

**Read** by `agent-run.mjs`, which prepends every matching lesson to the next task's brief.
A lesson scoped `always` reaches every task; any other scope reaches a task whose brief
mentions that keyword. Matching is deliberately generous — a lesson wrongly included costs
a few hundred characters, a lesson wrongly excluded costs the mistake being repeated.

`npm run lessons` shows what the loop currently remembers.

## Why this exists

`agent-run.mjs` already re-reads the journal for prior failed attempts at *the same task*,
so a retry knows why the last attempt failed. But the journal is keyed by task, which means
a mistake made in task 5 is invisible to task 20, and anything the guides do not cover has
to be rediscovered by every run that trips on it. Static documentation cannot fix that,
because nobody edits a guide in the middle of an unattended run.

## Rules

- **The bar is a future *unrelated* task.** Record something only if a task with a different
  subject would trip on it too. "The build failed because I had a typo" is not a lesson.
- **Never duplicate a `CLAUDE.md`.** Conventions belong in the guides, which every run is
  already told to read. Memory is for what the guides do not say and somebody had to find
  out the hard way.
- **A memory full of the obvious is worse than an empty one.** Injection is capped at 12
  lessons and 6000 characters, oldest dropped first, so every platitude evicts something
  real. The cap discloses what it dropped rather than truncating silently.
- **Recording is idempotent by title.** Writing the same title again refines that lesson
  rather than accumulating near-duplicates that each consume the budget.
- **Memory must never break a run.** An unreadable lesson is skipped, a failed write is
  reported and swallowed. It improves a run; it is never the reason one cannot start.
- **Delete lessons that stop being true.** A lesson describing a trap that has since been
  fixed is now actively misleading, and it still costs budget. `git rm` it.
