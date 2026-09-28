#!/usr/bin/env node
/**
 * PostToolUse guard: the conventions a typecheck cannot catch.
 *
 * Every codebase has two or three rules that compile perfectly and fail at runtime — an
 * import that needs a suffix the compiler does not require, an environment variable read
 * somewhere it must not be, a direct database call outside the one module allowed to make
 * them. They are exactly the rules a new session forgets, and exactly the rules no gate
 * reports, so they are reported here: after the file is written, while it is still the
 * thing the session is thinking about.
 *
 * The rules themselves are this project's own and live in `agentic.config.json`:
 *
 *   "hooks": {
 *     "conventions": [
 *       {
 *         "paths": ["/src/.*\\.ts$"],
 *         "except": ["/src/config\\.ts$"],
 *         "forbid": "process\\.env\\b",
 *         "message": "process.env is read outside config.ts — add the setting there instead"
 *       }
 *     ]
 *   }
 *
 * Claude Code pipes the tool call as JSON on stdin. Exit 2 sends stderr back to the model
 * as feedback; exit 0 stays silent.
 */
import { readFileSync } from "node:fs";
import { config } from "../../scripts/lib/project-config.mjs";

const raw = await new Promise((resolve) => {
  let buf = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (c) => (buf += c));
  process.stdin.on("end", () => resolve(buf));
});

let filePath = "";
try {
  filePath = JSON.parse(raw)?.tool_input?.file_path ?? "";
} catch {
  process.exit(0);
}
if (!filePath) process.exit(0);

const rules = config.hooks.conventions ?? [];
if (rules.length === 0) process.exit(0);

const normalized = filePath.replace(/\\/g, "/");

let content = "";
try {
  content = readFileSync(filePath, "utf8");
} catch {
  // The file may have been deleted or moved between the write and this hook. Nothing to say.
  process.exit(0);
}

const lineOf = (index) => content.slice(0, index).split("\n").length;
const anyMatch = (patterns, value) =>
  (patterns ?? []).some((p) => {
    try {
      return new RegExp(p).test(value);
    } catch {
      return false;
    }
  });

const problems = [];

for (const rule of rules) {
  if (!anyMatch(rule.paths, normalized)) continue;
  if (rule.except && anyMatch(rule.except, normalized)) continue;

  let forbid;
  try {
    forbid = new RegExp(rule.forbid, rule.flags ?? "g");
  } catch {
    continue; // a broken rule must not take the guard down with it
  }
  // A rule written without /g would match once and loop for ever on matchAll.
  const global = forbid.flags.includes("g") ? forbid : new RegExp(forbid.source, `${forbid.flags}g`);

  for (const match of content.matchAll(global)) {
    // `allowWhen` is for the common near-miss: a pattern that is only a violation when the
    // match does *not* already satisfy something — an import without its suffix, say.
    if (rule.allowWhen && new RegExp(rule.allowWhen).test(match[0])) continue;
    problems.push(`  ${normalized}:${lineOf(match.index)} — ${rule.message}`);
  }
}

if (problems.length === 0) process.exit(0);

console.error(
  [
    `Repo convention violations in the file you just wrote (${problems.length}):`,
    "",
    ...problems.slice(0, 20),
    "",
    "These pass the compiler and fail at runtime. Fix them before moving on.",
  ].join("\n"),
);
process.exit(2);
