# docs/builds — the ledger

One record per change that landed, written by the run that landed it. `npm run record --
--index` rebuilds the index below it.

This is the durable answer to "why does this exist?" long after the queue lane has been
cleared and the spec has gone stale. A build record names the brief, the commit, and what
the change actually did — including anything it deliberately did not do.

A row marked **nothing landed** is a task the queue filed as done without a commit to show
for it. Each one is either work still worth doing or a brief worth deleting; neither
resolves itself by being ignored.
