/**
 * Cross-run memory: what the loop learned, carried into the next task's brief.
 *
 * Without this, every task starts from a blank slate. `agent-run.mjs` already re-reads the
 * journal for prior failed attempts at *the same task*, so a retry knows why the last
 * attempt failed — but a mistake made in task 5 is unknown to task 20, and a rule the
 * repo's own conventions do not cover has to be rediscovered by every run that trips on it.
 *
 * The contract is deliberately small:
 *
 *   write  a session that learned something durable writes one file into
 *          .claude/memory/lessons/, because the session is the only party that knows what
 *          it learned and why.
 *   read   every task brief is prefixed with the lessons whose scope matches it.
 *
 * Two properties matter more than cleverness here.
 *
 * **Bounded.** Memory that grows without limit eventually crowds the task out of its own
 * brief, and a brief that is mostly history is worse than one with none. Injection is
 * capped by both count and characters, oldest dropped first, and the cap is reported so a
 * silent truncation cannot masquerade as "there were only three lessons".
 *
 * **Inert on failure.** A malformed lesson file is skipped, not thrown. Memory improves a
 * run; it must never be the reason one cannot start.
 *
 * Pure functions over a directory: no network, no database, no model calls.
 */
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

/** Injection ceiling. Briefs are already long; memory gets a corner of one, not the page. */
export const MAX_LESSONS = 12;
export const MAX_CHARS = 6000;

/**
 * A lesson is markdown with a small frontmatter block:
 *
 *   ---
 *   scope: queue
 *   learned: 2026-08-27
 *   task: 15-lead-module-config-tables
 *   ---
 *   # Title
 *   body…
 *
 * `scope` is a single keyword matched against the task, or `always` for lessons that apply
 * to every run. Everything else is descriptive and never affects matching.
 */
export function parseLesson(text, file) {
  // A lesson checked out with CRLF line endings is still a lesson.
  text = String(text).replace(/\r\n/g, "\n");
  const m = /^---\n([\s\S]*?)\n---\n?([\s\S]*)$/.exec(text.trim());
  if (!m) return null;

  const meta = {};
  for (const line of m[1].split("\n")) {
    const i = line.indexOf(":");
    if (i < 0) continue;
    meta[line.slice(0, i).trim().toLowerCase()] = line.slice(i + 1).trim();
  }

  const body = m[2].trim();
  if (!body) return null;

  const title = (/^#\s+(.+)$/m.exec(body) || [, file.replace(/\.md$/, "")])[1];
  return {
    file,
    scope: (meta.scope || "always").toLowerCase(),
    learned: meta.learned || "",
    task: meta.task || "",
    title,
    body,
  };
}

/** Every readable lesson on disk. Unreadable or malformed files are skipped silently. */
export function readLessons(memoryDir) {
  const dir = join(memoryDir, "lessons");
  if (!existsSync(dir)) return [];

  const out = [];
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".md")).sort()) {
    try {
      const parsed = parseLesson(readFileSync(join(dir, file), "utf8"), file);
      if (parsed) out.push(parsed);
    } catch {
      // A lesson that cannot be read is a lesson not applied. It is never a reason to
      // stop a run, so there is nothing to report here.
    }
  }
  return out;
}

/**
 * A lesson's scope keywords.
 *
 * `scope` is written as one keyword, but three lessons had been recorded with a list —
 * `scope: auth, hubspot, queue` — which the single-keyword match treated as one literal
 * string and therefore matched nothing at all. Splitting on commas and whitespace is what
 * their authors plainly meant, and a lesson that names three areas should reach all three.
 * Hyphens are kept, so `product-query` stays one keyword.
 */
export function scopesOf(lesson) {
  return String(lesson?.scope ?? "always")
    .toLowerCase()
    .split(/[,\s]+/)
    .map((s) => s.trim())
    .filter(Boolean);
}

/**
 * Which lessons apply to this task.
 *
 * `always` matches everything. Any other scope matches when its keyword appears in the
 * task's text — the brief names the files and areas it touches, so this is a good enough
 * signal without maintaining a second mapping that would drift from reality.
 *
 * Matching is deliberately generous: a lesson wrongly included costs a few hundred
 * characters, a lesson wrongly excluded costs the mistake being repeated.
 *
 * The generosity has one sharp edge, which is why `scopeReach` exists below: a scope that
 * reads like a sensible area name but appears in no brief matches nothing, silently, for
 * ever. Four lessons were scoped `frontend`, a word no brief in this repo has ever used,
 * and none of them was ever injected into anything.
 */
export function selectLessons(lessons, taskText) {
  const haystack = (taskText || "").toLowerCase();
  return lessons.filter((l) =>
    scopesOf(l).some((scope) => scope === "always" || haystack.includes(scope)),
  );
}

/**
 * How many of `texts` a scope keyword would reach.
 *
 * Used when recording a lesson, to say so at the time rather than leaving a dead lesson to
 * be discovered by an audit months later. Returns 0 for a scope nothing mentions; `always`
 * reaches everything.
 */
export function scopeReach(scope, texts) {
  const keys = scopesOf({ scope });
  if (keys.includes("always")) return (texts || []).length;
  return (texts || []).filter((t) => {
    const haystack = String(t || "").toLowerCase();
    return keys.some((k) => haystack.includes(k));
  }).length;
}

/**
 * Render the selected lessons as a brief prefix, within both caps.
 *
 * Newest first, because a recent lesson is likelier to still be true. When the cap drops
 * anything, the text says so — a run told "here is what we learned" while lessons were
 * silently discarded is being misled about how much it knows.
 */
export function formatForBrief(selected, { maxLessons = MAX_LESSONS, maxChars = MAX_CHARS } = {}) {
  if (selected.length === 0) return "";

  const ordered = [...selected].sort((a, b) => (b.learned || "").localeCompare(a.learned || ""));
  const kept = [];
  let chars = 0;

  for (const lesson of ordered.slice(0, maxLessons)) {
    const block = `### ${lesson.title}\n${stripHeading(lesson.body)}`;
    if (chars + block.length > maxChars) break;
    kept.push(block);
    chars += block.length;
  }

  if (kept.length === 0) return "";
  const dropped = selected.length - kept.length;

  return [
    "## What earlier runs in this repository learned",
    "",
    "These are lessons recorded by previous tasks, not general advice. They describe traps",
    "this specific codebase has already sprung on somebody. Read them before you start.",
    "",
    kept.join("\n\n"),
    dropped > 0
      ? `\n_(${dropped} older lesson(s) not shown — the brief's memory budget was reached.)_`
      : "",
  ]
    .filter(Boolean)
    .join("\n");
}

function stripHeading(body) {
  return body.replace(/^#\s+.+\n?/, "").trim();
}

/** Turn a title into the file name that holds it, so re-recording overwrites rather than duplicates. */
export function lessonSlug(title) {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 60);
}

/** Write one lesson. Overwrites an existing file of the same slug — a refined lesson replaces its earlier form. */
export function writeLesson(memoryDir, { title, body, scope = "always", learned = "", task = "" }) {
  const dir = join(memoryDir, "lessons");
  mkdirSync(dir, { recursive: true });
  const slug = lessonSlug(title);
  const path = join(dir, `${slug}.md`);
  writeFileSync(
    path,
    `---\nscope: ${scope}\nlearned: ${learned}\ntask: ${task}\n---\n\n# ${title}\n\n${body.trim()}\n`,
  );
  return path;
}
