/**
 * Where a task's build record goes, what it says, and what each spec was planned at. The
 * path and record rules are pure; reading and writing the plan records is the only I/O.
 *
 * Tasks live in their spec's folder, named for the spec alone (lib/task-names.mjs); records
 * are grouped by *planning*: each time a spec is planned, its plan record names a directory
 * `<date-time>-<spec-slug>` — the time it was planned, so a listing reads in order — and
 * every task of that spec records into it. A task queued by hand records into `adhoc/`.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { specSlug } from "./planning.mjs";
import { ADHOC, nameOf, scopeOf } from "./task-names.mjs";

/** `2026-10-06T14-03-27` — sortable, and safe in a file name on every platform. */
export const stampOf = (date = new Date()) => date.toISOString().replace(/[:.]/g, "-").slice(0, 19);

/** The record directory that planning the spec `slug` at `date` names. */
export const planDirName = (slug, date = new Date()) => `${stampOf(date)}-${slug}`;

/**
 * What every spec was planned at, keyed by spec file: `.agent-queue/planned/<spec>.json`,
 * one file per spec, laid over the legacy single `planned.json`.
 *
 * One shared object meant two branches that each planned a different spec both rewrote its
 * closing lines, and nearly every pair of spec pull requests conflicted there. A file per
 * spec is written by the one branch that plans it. The legacy file is still read — a spec
 * planned before this keeps its record — and never written again.
 */
export function readPlanned(queueDir) {
  let planned = {};
  try {
    planned = JSON.parse(readFileSync(join(queueDir, "planned.json"), "utf8")) ?? {};
  } catch {
    /* nothing planned the old way */
  }
  const dir = join(queueDir, PLANNED_DIR);
  if (existsSync(dir)) {
    for (const f of readdirSync(dir).filter((n) => n.endsWith(".json"))) {
      try {
        planned[f.replace(/\.json$/, "")] = JSON.parse(readFileSync(join(dir, f), "utf8"));
      } catch {
        /* a broken record plans that spec again, which is the safe direction */
      }
    }
  }
  return planned;
}

/** The folder of per-spec plan records under `.agent-queue/`. */
export const PLANNED_DIR = "planned";

/** The repo-relative path of one spec's plan record — what `Plan <spec>` commits. */
export const plannedRecordPath = (specFile) => `.agent-queue/${PLANNED_DIR}/${specFile}.json`;

export function writePlanned(queueDir, specFile, entry) {
  mkdirSync(join(queueDir, PLANNED_DIR), { recursive: true });
  writeFileSync(join(queueDir, PLANNED_DIR, `${specFile}.json`), `${JSON.stringify(entry, null, 2)}\n`);
}

/** A plan record once held a bare content hash; it now holds `{ hash, builds }`. */
export function plannedEntry(value) {
  if (typeof value === "string") return { hash: value, builds: null };
  return { hash: value?.hash ?? null, builds: value?.builds ?? null };
}

const STAMPED = /^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}-/;

/**
 * The directory, under the builds root, the spec whose scope is `scope` records into: the one
 * its latest planning named in its plan record, else the newest existing `<date-time>-<scope>`
 * (tasks split by hand, or planned before plan records named directories), else a new one
 * stamped `date`.
 */
export function buildsDirFor(scope, { planned = {}, existing = [], date = new Date() } = {}) {
  for (const [specFile, value] of Object.entries(planned)) {
    const { builds } = plannedEntry(value);
    if (builds && specSlug(specFile) === scope) return builds;
  }
  const match = existing.filter((d) => STAMPED.test(d) && d.replace(STAMPED, "") === scope).sort();
  return match.at(-1) ?? planDirName(scope, date);
}

/**
 * The record's path under the builds root for the task `stem` (`scope/name`). A task with no
 * scope is filed as adhoc; an adhoc record leads with a date-time, which a task queued by
 * `queue -- add` already carries as its intake stamp.
 */
export function recordPathFor(stem, { planned = {}, existing = [], date = new Date() } = {}) {
  const scope = scopeOf(stem) || ADHOC;
  const name = nameOf(stem);
  if (scope === ADHOC) return `${ADHOC}/${STAMPED.test(name) ? "" : `${stampOf(date)}-`}${name}.md`;
  return `${buildsDirFor(scope, { planned, existing, date })}/${name}.md`;
}

/** The brief's first non-empty line — the commit subject. */
export function subjectOf(brief) {
  for (const line of String(brief ?? "").split("\n")) {
    const t = line.trim();
    if (t) return t.replace(/\.$/, "");
  }
  return "(no subject)";
}

/**
 * The record. It is written before the task's commit, so it cannot name that commit's
 * hash; the commit names the task instead (`Task: <stem>`), and `git log --grep "Task: <stem>"`
 * finds it from the record.
 */
export function renderRecord({ stem, brief, date = new Date(), files = [], verify = "npm run verify" }) {
  const lines = String(brief ?? "").replace(/\r\n/g, "\n").split("\n");
  const first = lines.findIndex((l) => l.trim());
  const body = lines.slice(first + 1).join("\n").trim();
  const table = files.length
    ? ["| File | + | − |", "|------|---|---|", ...files.map((f) => `| \`${f.file}\` | ${f.add} | ${f.del} |`)].join("\n")
    : "_No file changes were found._";
  return `# ${subjectOf(brief)}

| | |
|---|---|
| **Task** | \`${stem}\` |
| **Landed** | ${date.toISOString().slice(0, 10)} |
| **Commit** | the one whose message carries \`Task: ${stem}\` |
| **Verification** | \`${verify}\` — the static gate, plus whichever live checks the diff demanded |

## What changed

${table}

## The brief this was built from

${body}
`;
}
