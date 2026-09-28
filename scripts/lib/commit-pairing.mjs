/**
 * Tying a task brief to the commit that carries it.
 *
 * `done/` is read as proof a task's code is on `main`, and dependents are released on that
 * basis — so the pairing has to be right, and it has to survive a human. It did not: the
 * audit matched a brief to a commit by comparing the brief's subject line against commit
 * subjects, which broke the first time somebody committed a run's leftover work under a
 * message of their own. One finished task then sat in `todo/` holding up eight dependents,
 * and was only found by hand.
 *
 * So a run's commit now carries a `Task: <brief-stem>` trailer, which is exact and does not
 * care what the subject says. Subject matching stays as the fallback, because every commit
 * written before the trailer existed still has to pair as it did.
 *
 * Pure functions over strings: no git, no filesystem, no network. The caller supplies the
 * log it already read.
 */

/** The brief stem a commit message names in its `Task:` trailer, or null for none. */
export function taskTrailerOf(message) {
  const m = /^[ \t]*Task:[ \t]*([A-Za-z0-9._-]+?)[ \t]*$/m.exec(message ?? "");
  return m ? m[1].replace(/\.md$/, "") : null;
}

/** A brief's file name reduced to the stem the trailer carries. */
export function stemOf(taskFile) {
  return String(taskFile ?? "").replace(/\.md$/, "");
}

/** The brief's subject: its first non-empty line, as agent-run derives the commit subject. */
export function subjectOf(brief) {
  for (const line of String(brief ?? "").split("\n")) {
    const t = line.trim();
    if (t) return t.replace(/\.$/, "");
  }
  return "";
}

/**
 * The commit that carries `taskFile`, or null.
 *
 * `commits` is `[{ sha, subject, message }]`, newest first. The trailer wins outright; the
 * subject fallback matches in full or on a generous prefix, because some tooling truncates
 * a subject — but never on a prefix short enough to pair two different builds.
 */
export function findCommitFor({ taskFile, brief, commits }) {
  const stem = stemOf(taskFile);
  const list = Array.isArray(commits) ? commits : [];

  const byTrailer = list.find((c) => taskTrailerOf(c.message ?? "") === stem);
  if (byTrailer) return { ...byTrailer, matchedBy: "trailer" };

  const subject = subjectOf(brief);
  if (!subject) return null;

  const exact = list.find((c) => c.subject === subject);
  if (exact) return { ...exact, matchedBy: "subject-exact" };

  // A prefix match is a reasonable guess for a report and not good enough to move a lane
  // file on, so it is labelled rather than hidden. `--fix` only acts on the exact kinds.
  const key = subject.slice(0, 48);
  if (key.length < 24) return null;
  const prefix = list.find((c) => String(c.subject ?? "").startsWith(key));
  return prefix ? { ...prefix, matchedBy: "subject-prefix" } : null;
}

/**
 * Whether a match is solid enough to move a lane file on. A trailer is exact by
 * construction and a whole-subject match is exact by comparison; a prefix match is a guess,
 * and guessing wrong here files an unbuilt task as done and releases its dependents.
 */
export function isDefinite(match) {
  return match?.matchedBy === "trailer" || match?.matchedBy === "subject-exact";
}

/** The sha a build record already names, from its text, or "" when it names none. */
export function recordedShaIn(recordText) {
  const m = /\*\*Commit\*\*\s*\|\s*`([0-9a-f]{7,40})`/.exec(String(recordText ?? ""));
  return m ? m[1] : "";
}
