---
description: Verify, review, and commit the working tree — then stop for approval to push
allowed-tools: Bash, Read, Grep, Glob
---

Ship the work in the current working tree.

Work happens on the project's working branch and commits there as a matter of course.
**Pushing is what needs the user's explicit go-ahead in this turn** — stop at step 5 without
it. The bash guard refuses the protected branch as a push destination by any refspec
regardless, and never merge a pull request.

1. **Verify.** Run `npm run verify`. If it fails — including a live check it could not
   run — stop and fix before going further.
2. **Review.** Delegate the diff to the `code-reviewer` agent, or run `/review-diff`.
   Address anything real it finds, then review again.
3. **Stage deliberately.** `git status` first. Stage only files that belong to this change.
   Do not `git add -A` — unrelated edits must not ride along. Never stage the environment
   file.
4. **Commit.** One-line imperative summary, then a body: what changed, why, and how it was
   verified.
5. **Stop.** Report the commit, the gates that ran and the ones that were skipped, and say
   how many commits are now unpushed. Wait.
6. **Push — only once they say so in this turn.** Then hand them the pull request URL if
   they want one. Merging it is theirs alone.
