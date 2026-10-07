---
description: Verify, review, and commit the working tree — then stop for approval to push
allowed-tools: Bash, PowerShell, Read, Edit, Write, Grep, Glob, Agent
---

Ship the work in the current working tree.

Under `project.workflow: "solo"` (agentic.config.json) work commits on the branch that is
checked out, as a matter of course. Under `"team"` nothing commits on the base branch: if
that is what is checked out, `git switch -c <type>/<slug>` first and commit there. Never
merge a pull request.

1. **Verify.** Run `npm run verify -- --autostart`. If it fails — including a live check it
   could not run — stop and fix before going further.
2. **Review.** Run `npm run owes`: it lists the reviewers this diff owes, by the same rule the
   task runner enforces. Delegate to every one it names in a single message, address anything
   real they find, and send each fix back to the reviewer that asked for it.
3. **Stage deliberately.** `git status` first. Stage only files that belong to this change.
   Do not `git add -A` — unrelated edits must not ride along. Never stage the environment
   file.
4. **Commit.** One-line imperative summary, then a body: what changed, why, and how it was
   verified.
5. **Stop.** Report the commit, the gates that ran and the ones that were skipped, and how
   many commits are now unpushed. Wait.
6. **Push.** The guard refuses an agent's push to `main`, `master`, `develop`, the base branch or
   `project.branch`, whatever is said in the turn — so for those, give the owner the command
   to run themselves (`! git push` in this prompt). A feature branch may be pushed once they
   say so in this turn; then hand them the pull request URL (`npm run pr`). Merging is theirs
   alone.
