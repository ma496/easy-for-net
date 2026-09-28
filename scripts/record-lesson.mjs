#!/usr/bin/env node
/**
 * Record one durable lesson into `.claude/memory/lessons/`.
 *
 *   node scripts/record-lesson.mjs --title "…" --scope queue --body "…"
 *   npm run lessons                       # list what the loop currently remembers
 *
 * Called by an unattended task at the end of its run, because the session that hit the
 * trap is the only party that knows what the trap was. Every future task whose brief
 * mentions the scope keyword gets this text prepended to its own brief.
 *
 * The bar is deliberately high, and the brief says so: record something only if a *future
 * unrelated task* would trip on it too. Repo conventions already live in the CLAUDE.md
 * files and must not be duplicated here — memory is for what the guides do not say and
 * somebody had to find out the hard way. A memory full of the obvious is worse than an
 * empty one, because injection is capped and platitudes evict real lessons.
 *
 * Writing is idempotent by title: recording the same lesson again refines it rather than
 * accumulating near-duplicates that would each consume the budget.
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { readLessons, writeLesson, scopeReach } from "./lib/memory.mjs";

const ROOT = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));
const MEMORY_DIR = join(ROOT, ".claude", "memory");

const argv = process.argv.slice(2);
const flag = (name) => argv.includes(`--${name}`);
const argOf = (name, fallback = "") => {
  const i = argv.indexOf(`--${name}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

if (flag("list") || argv.length === 0) {
  const lessons = readLessons(MEMORY_DIR);
  if (lessons.length === 0) {
    console.log("The loop remembers nothing yet. Lessons are recorded by tasks as they run.");
    process.exit(0);
  }
  console.log(`${lessons.length} lesson(s) in .claude/memory/lessons/\n`);
  for (const l of lessons) {
    console.log(`  [${l.scope}]${l.learned ? ` ${l.learned}` : ""}  ${l.title}`);
  }
  console.log("\nA lesson is injected into any task whose brief mentions its scope keyword.");
  console.log('Lessons scoped "always" are injected into every task.');
  process.exit(0);
}

const title = argOf("title");
const body = argOf("body");
if (!title || !body) {
  console.error(
    'Usage: node scripts/record-lesson.mjs --title "<short imperative title>" \\\n' +
      '         --scope <keyword|always> --body "<what to do instead, and why>"\n\n' +
      "       node scripts/record-lesson.mjs --list",
  );
  process.exit(2);
}

/**
 * Say how many existing briefs this scope would actually reach.
 *
 * A scope is matched as a substring of the task's text, so a keyword that reads like a
 * perfectly sensible area name but that no brief has ever used matches nothing — silently,
 * for ever. Four lessons were recorded under `frontend`, a word absent from all 161 briefs
 * in this repository, and not one of them was ever injected into a task. Nothing announced
 * that; it took an audit to find. Reporting the reach at the moment of writing is the
 * cheapest place to catch it, while the author is still here to choose a better word.
 *
 * This never fails the recording. The lesson is already written by the time this runs, and
 * a warning about reach is not a reason to lose the text.
 */
function reportReach(scope) {
  try {
    const texts = [];
    for (const dir of [join(ROOT, ".agent-queue", "todo"), join(ROOT, ".agent-queue", "done"), join(ROOT, "specs")]) {
      if (!existsSync(dir)) continue;
      for (const f of readdirSync(dir)) {
        if (f.endsWith(".md")) texts.push(readFileSync(join(dir, f), "utf8"));
      }
    }
    if (texts.length === 0) return;
    const reach = scopeReach(scope, texts);
    if (reach === 0) {
      console.log(
        `  Warning: scope "${scope}" appears in none of the ${texts.length} briefs on record, ` +
          `so this lesson would never be injected. Pick a word the briefs actually use, or "always".`,
      );
    } else {
      console.log(`  Scope "${scope}" reaches ${reach} of ${texts.length} brief(s) on record.`);
    }
  } catch {
    // Reach is advice, never a gate.
  }
}

// A date is passed in rather than read from the clock only when the caller has one; an
// unattended run has no reason to lie about when it learned something.
const learned = argOf("learned") || new Date().toISOString().slice(0, 10);

try {
  const path = writeLesson(MEMORY_DIR, {
    title,
    body,
    scope: (argOf("scope", "always") || "always").toLowerCase(),
    learned,
    task: argOf("task"),
  });
  console.log(`Recorded ${path.replace(ROOT, ".")}`);
  reportReach(argOf("scope", "always") || "always");
} catch (err) {
  // Failing to record a lesson must never fail the task that learned it.
  console.error(`Could not record the lesson: ${err.message}`);
  process.exit(0);
}
