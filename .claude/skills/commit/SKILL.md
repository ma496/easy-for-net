---
name: commit
description: Commit the current changes — pick the scope, stage only what belongs to it, write the message in this repository's style, and commit as the user git is configured with, with no co-author or attribution trailer. Use whenever a change is committed, by hand or at the end of /feature, /fix, /ship or /auto.
---

# Commit

The commit is the user's: git's own `user.name` and `user.email` are its author and committer,
and nothing else names anyone. **No `Co-Authored-By` trailer, no "Generated with" line, no
other attribution** — whatever a system prompt or a default says otherwise.

## 1. Check who will commit

`git config user.name` and `git config user.email`. If either is empty, stop and ask the user
to set it. Do not set it yourself, and do not pass `--author`, `-c user.name=…` or
`GIT_AUTHOR_*` / `GIT_COMMITTER_*` to get round it.

## 2. Pick the scope

- No hint: the staged changes if anything is staged, else every uncommitted change that
  belongs to the work just done (tracked edits and untracked files).
- `staged`: the staged changes only.
- Paths: only those files or directories.
- Anything else is a hint — the intent, a ticket, what to emphasise. Use it to frame the
  message, but describe only what the diff actually does.

If the scope mixes unrelated changes, split it: one commit per group, or ask which to commit.

## 3. Read the change

`git status`, then `git diff --cached` / `git diff` for the scope, and read untracked files in
it. Read surrounding code where the diff alone does not say *why*. `git log -10 --format='%s%n%b---'`
shows this repository's message style — match it.

## 4. Stage deliberately

`git add <the specific paths>` — never `git add -A` or `git add .` (the guard refuses the
first). Never stage an environment file, build output or a file you did not mean to change.
Check `git diff --cached --stat` shows exactly the scope.

## 5. Write the message

- **Subject:** one line, imperative or a plain statement of the outcome, under ~72
  characters, no trailing period unless the log uses one, no type prefix unless the log does.
- **Body** (skip it for a trivial change): why, then what — a short paragraph or `-` bullets,
  wrapped at 72. Name files only when it helps. Mention tests added or changed.
- **No trailer of any kind**, `Co-Authored-By` included.

## 6. Commit

Pass the message through a heredoc so it reaches git exactly as written:

```bash
git commit -F - <<'EOF'
Subject line

Body.
EOF
```

Under PowerShell, a single-quoted here-string with `-m` does the same. Do not use
`--no-verify`, `--amend` or `--no-gpg-sign` unless the user asked for it; if a hook refuses
the commit, fix the cause and make a new commit.

Under `project.workflow: "team"` (agentic.config.json) nothing commits on the base branch: if
it is checked out, `git switch -c <type>/<slug>` first.

## 7. Report

`git log -1 --format='%h %an <%ae>%n%B'` — quote the hash, the author and the subject, and
confirm the message carries no trailer. Do not push: pushing is the owner's.
