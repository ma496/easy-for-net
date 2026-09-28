#!/usr/bin/env node
/**
 * The build record: one committed markdown file per change the task queue landed.
 *
 *   npm run record -- --task <brief.md> --commit <sha>   # record one build
 *   npm run record -- --backfill                          # reconstruct from history
 *   npm run record -- --index                             # rewrite the index only
 *
 * Why this exists. A task brief is the most complete description of a change this repo
 * ever produces — it says what to build, what not to touch, and how to know it worked.
 * But `.agent-queue/done/` is gitignored, so the moment a task finished, the only durable
 * record was a commit subject. Six months on, "why is there a synonym table?" had no
 * answer in the repo at all.
 *
 * So every task that lands writes its brief into `docs/builds/`, which is committed. The
 * brief is preserved verbatim rather than summarised: a summary is a second thing that can
 * drift from the truth, and the whole point is a record that cannot.
 *
 * This never fails a drain. A build that lands but goes unrecorded is a documentation gap;
 * a drain that dies because it could not write a markdown file is a broken pipeline. Every
 * error here is caught and reported, and the exit code stays 0 unless you invoked it
 * directly with bad arguments.
 */
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { recordedShaIn, taskTrailerOf } from "./lib/commit-pairing.mjs";
import { config, WORK_BRANCH } from "./lib/project-config.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const BUILDS = join(ROOT, config.docs.builds);
const DONE = join(ROOT, ".agent-queue", "done");

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback = null) => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

function git(args, fallback = "") {
  try {
    return execFileSync("git", args, { cwd: ROOT, encoding: "utf8" }).trim();
  } catch {
    return fallback;
  }
}

/** The sha an existing build record already names, or "" when it names none. */
function recordedShaFor(taskFile) {
  const path = join(BUILDS, `${slugFor(taskFile)}.md`);
  if (!existsSync(path)) return "";
  return recordedShaIn(readFileSync(path, "utf8"));
}

/** A task file name becomes the record's file name, so the two line up on sight. */
function slugFor(taskFile) {
  return basename(taskFile, ".md")
    .replace(/^\d{4}-\d{2}-\d{2}T[\d-]+-/, "")
    .replace(/[^a-zA-Z0-9-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}

/**
 * The subject is the brief's first non-empty line, matching how agent-run derives the
 * commit subject — that correspondence is what lets `--backfill` pair the two up later.
 */
function subjectOf(brief) {
  for (const line of brief.split("\n")) {
    const t = line.trim();
    if (t) return t.replace(/\.$/, "");
  }
  return "(no subject)";
}

/** Everything after the subject line: the scope, the exclusions, the done-when. */
function bodyOf(brief) {
  const lines = brief.split("\n");
  const i = lines.findIndex((l) => l.trim());
  return lines.slice(i + 1).join("\n").trim();
}

function statFor(sha) {
  if (!sha) return [];
  const out = git(["show", "--numstat", "--format=", sha]);
  if (!out) return [];
  return out
    .split("\n")
    .filter(Boolean)
    .map((line) => {
      const [add, del, file] = line.split("\t");
      return { file, add, del };
    })
    .filter((r) => r.file);
}

function renderRecord({ subject, sha, date, taskFile, body, files }) {
  const table = files.length
    ? [
        "| File | + | − |",
        "|------|---|---|",
        ...files.map((f) => `| \`${f.file}\` | ${f.add} | ${f.del} |`),
      ].join("\n")
    : "_No file-level detail recorded for this build._";

  // A brief with no commit is not a formatting problem to paper over. It means the queue
  // filed a task as done that produced no change on the work branch, and the record says so loudly —
  // that discrepancy is the single most useful thing this directory can surface.
  const orphan = !sha;
  const warning = orphan
    ? `\n> **No commit on \`${WORK_BRANCH}\` corresponds to this brief.**` + " The queue recorded it as done,\n" +
      "> but no commit carries the work. Treat the description below as *intended*, not as\n" +
      "> something that shipped — and requeue it if it is still wanted.\n"
    : "";

  return `# ${subject}
${warning}
| | |
|---|---|
| **Commit** | ${sha ? `\`${sha.slice(0, 8)}\`` : "**none — nothing landed**"} |
| **Landed** | ${date || "**never**"} |
| **Task brief** | \`${taskFile}\` |
| **Verification** | ${orphan ? "_no verified change to attest to_" : `\`${config.commands.verify}\` — the static gate, plus whichever live checks the diff demanded`} |

## What changed

${table}

## The brief this was built from

The task file is reproduced verbatim below. It is the most complete statement of intent
this change has: what to build, what to leave alone, and how anyone could tell it worked.

---

${body || "_The brief recorded no detail beyond its subject._"}
`;
}

function writeRecord({ taskFile, sha, brief }) {
  mkdirSync(BUILDS, { recursive: true });
  const slug = slugFor(taskFile);
  const path = join(BUILDS, `${slug}.md`);
  const date = sha ? git(["show", "-s", "--format=%ad", "--date=short", sha]) : "";

  writeFileSync(
    path,
    renderRecord({
      subject: subjectOf(brief),
      sha,
      date,
      taskFile: basename(taskFile),
      body: bodyOf(brief),
      files: statFor(sha),
    }),
  );
  return { slug, path, subject: subjectOf(brief), date, sha };
}

/**
 * The index is regenerated from the directory every time rather than appended to, so a
 * record deleted or renamed by hand cannot leave a dangling row behind.
 */
function writeIndex() {
  mkdirSync(BUILDS, { recursive: true });
  const rows = readdirSync(BUILDS)
    .filter((f) => f.endsWith(".md") && f !== "README.md")
    .map((f) => {
      const text = readFileSync(join(BUILDS, f), "utf8");
      const subject = (text.match(/^#\s+(.+)$/m) || [, f])[1];
      const date = (text.match(/\*\*Landed\*\*\s*\|\s*([0-9-]{10})/) || [, ""])[1];
      const sha = (text.match(/\*\*Commit\*\*\s*\|\s*`([0-9a-f]+)`/) || [, ""])[1];
      return { file: f, subject, date, sha };
    })
    .sort((a, b) => (b.date || "").localeCompare(a.date || "") || a.file.localeCompare(b.file));

  const orphans = rows.filter((r) => !r.sha).length;

  const body = `# Build record

Every change that has landed through the task queue, newest first. One file per
build, each preserving the brief the change was built from.

These are written automatically — \`scripts/record-build.mjs\` runs as each task lands, so
this directory cannot drift from what was actually committed. Do not hand-edit a record;
correct the code that produced it.

To read the change itself: \`git show <commit>\`.

| Landed | Build | Commit |
|--------|-------|--------|
${rows
  .map(
    (r) =>
      `| ${r.date || "**never**"} | [${r.subject}](${r.file}) | ${r.sha ? `\`${r.sha}\`` : "**nothing landed**"} |`,
  )
  .join("\n")}

${rows.length} build(s) recorded${orphans ? `, of which **${orphans} produced no change on \`${WORK_BRANCH}\`**` : ""}.
${
  orphans
    ? "\nA row marked **nothing landed** is a task the queue filed as done without a commit to\n" +
      "show for it. Each one is either work still worth doing or a brief worth deleting;\n" +
      "neither resolves itself by being ignored.\n"
    : ""
}`;
  writeFileSync(join(BUILDS, "README.md"), body);
  return rows.length;
}

/**
 * Briefs whose work landed under a different commit subject, and why. A task absorbed by
 * another task's commit is normal; a task recovered later lands under the recovery's
 * subject. Both are stated here rather than guessed at by fuzzy matching, because a build
 * record that pairs the wrong commit to a brief is worse than one that admits it does not
 * know.
 */
const PAIRS = {
  // "03-some-brief.md": "d8fdf07",   ← a brief whose work landed under another commit
};

/**
 * Backfill pairs each finished brief with the commit whose subject it matches. Briefs and
 * commit subjects share a first line by construction, so this is a lookup rather than a
 * guess; anything that does not match is reported and skipped, never approximated.
 */
function backfill() {
  if (!existsSync(DONE)) {
    console.log("No .agent-queue/done/ — nothing to backfill.");
    return;
  }

  // %B is the full message, so the `Task:` trailer travels with the subject. Records are
  // separated by a NUL because a commit body contains newlines by definition.
  const log = git(["log", "--no-merges", "--format=%H%x09%s%x09%B%x00"])
    .split("\0")
    .map((r) => r.replace(/^\n/, ""))
    .filter(Boolean)
    .map((l) => {
      const [sha, subject, body = ""] = l.split("\t");
      return { sha, subject, task: taskTrailerOf(body) };
    });

  const briefs = readdirSync(DONE).filter((f) => f.endsWith(".md")).sort();
  const matched = [];
  const unmatched = [];

  for (const file of briefs) {
    const brief = readFileSync(join(DONE, file), "utf8");
    const subject = subjectOf(brief);
    // Commit subjects are truncated by some tooling, so match on a generous prefix rather
    // than demanding equality — but never on a prefix short enough to pair two builds.
    const key = subject.slice(0, 48);
    const override = PAIRS[file] ? git(["rev-parse", PAIRS[file]], "") : "";
    const stem = basename(file, ".md");
    // The trailer is exact and survives any commit subject; subject matching is the
    // fallback that keeps history written before the trailer existed pairing as it did.
    const hit = override
      ? { sha: override }
      : log.find((c) => c.task && c.task === stem) ??
        log.find((c) => c.subject === subject || c.subject.startsWith(key));

    // A record that already names a commit is evidence; this pass is a guess. Backfill
    // fills gaps and must never overwrite a sha someone recorded deliberately — doing so
    // turned a correct record into "no commit carries the work" and lost the provenance.
    const kept = hit?.sha ? "" : recordedShaFor(file);
    if (kept) {
      const rec = writeRecord({ taskFile: file, sha: kept, brief });
      matched.push({ ...rec, file, preserved: true });
      continue;
    }

    const rec = writeRecord({ taskFile: file, sha: hit?.sha || "", brief });
    (hit ? matched : unmatched).push({ ...rec, file });
  }

  for (const m of matched)
    console.log(`  ${m.preserved ? "kept    " : "recorded"}  ${m.date}  ${m.subject.slice(0, 62)}`);
  if (unmatched.length) {
    console.log(`\n  ${unmatched.length} brief(s) recorded without a matching commit:`);
    for (const u of unmatched) console.log(`    ${u.file} — "${u.subject.slice(0, 58)}"`);
    console.log("  These landed under a different subject, or predate the queue.");
  }
  console.log(`\n${matched.length} of ${briefs.length} brief(s) paired with a commit.`);
}

// --- entry -------------------------------------------------------------------------

try {
  if (flag("backfill")) {
    backfill();
    console.log(`Index rewritten: ${writeIndex()} build(s) in ${config.docs.builds}/.`);
  } else if (flag("index")) {
    console.log(`Index rewritten: ${writeIndex()} build(s) in ${config.docs.builds}/.`);
  } else {
    const taskFile = argOf("task");
    if (!taskFile) {
      console.error(
        'Usage: npm run record -- --task <brief.md> --commit <sha>\n' +
          "       npm run record -- --backfill\n" +
          "       npm run record -- --index",
      );
      process.exit(2);
    }
    if (!existsSync(taskFile)) {
      console.error(`No such task file: ${taskFile}`);
      process.exit(2);
    }
    const rec = writeRecord({
      taskFile,
      sha: argOf("commit", ""),
      brief: readFileSync(taskFile, "utf8"),
    });
    writeIndex();
    console.log(`Recorded ${config.docs.builds}/${rec.slug}.md`);
  }
} catch (err) {
  // Never take a drain down over documentation. Report and let the build stand.
  console.error(`record-build: ${err.message}`);
  process.exit(0);
}
