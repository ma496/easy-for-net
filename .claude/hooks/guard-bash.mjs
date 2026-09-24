#!/usr/bin/env node
/**
 * PreToolUse guard for the shell tools — Bash and PowerShell alike.
 *
 * settings.json `deny` matches literal command shapes; this catches the variants and
 * compound commands (`a && b`, `sh -c "..."`) that slip past a prefix match. Both shells
 * reach the same files and the same git, so one set of rules covers both: paths may use
 * either slash, and the PowerShell spellings of the read and search commands count too.
 *
 * Heredoc bodies and PowerShell here-strings are stripped before matching: writing a file
 * that *documents* a destructive command is not running one. Search patterns are blanked
 * for the same reason — `grep -rn 'git merge' .claude/` looks for the word, it does not
 * merge.
 *
 * Exit 2 blocks the call and returns stderr to the model. Exit 0 allows it.
 *
 * Every rule here is covered by hook-tests.mjs, which `npm run gate` runs. A rule without
 * a test is a rule that will be silently broken by the next edit.
 */
import { execSync } from "node:child_process";
import { BASE_BRANCH, config } from "../../scripts/lib/project-config.mjs";

const raw = await new Promise((resolve) => {
  let buf = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", (c) => (buf += c));
  process.stdin.on("end", () => resolve(buf));
});

let command = "";
try {
  command = JSON.parse(raw)?.tool_input?.command ?? "";
} catch {
  process.exit(0);
}
if (!command.trim()) process.exit(0);

/**
 * Remove heredoc bodies. `cat > f <<'EOF' ... EOF` writes data; the data is not a command
 * and must not be scanned for destructive patterns, or every doc mentioning them is blocked.
 */
function stripHeredocs(input) {
  const lines = input.split("\n");
  const out = [];
  let delimiter = null;
  let allowIndent = false;

  for (const line of lines) {
    if (delimiter !== null) {
      const candidate = allowIndent ? line.trim() : line;
      if (candidate === delimiter) delimiter = null;
      continue; // drop body and terminator
    }
    const match = line.match(/<<(-?)\s*(["']?)([A-Za-z_][A-Za-z0-9_]*)\2/);
    if (match) {
      allowIndent = match[1] === "-";
      delimiter = match[3];
    }
    out.push(line);
  }
  return out.join("\n");
}

// Commands that only ever *read* text. Their search pattern is data, never something the
// shell runs, so the pattern must not be matched against the rules below.
const SEARCH_TOOLS = new Set(["grep", "egrep", "fgrep", "rg", "ag", "ack", "select-string", "sls", "findstr"]);

// Commands that can safely stand downstream of a search in a pipeline: they reshape or
// display text and cannot execute it. Anything else — psql, sh, xargs — can, so a search
// feeding one of those keeps its pattern in the scan.
const TEXT_FILTERS = new Set([
  "head", "tail", "wc", "sort", "uniq", "cut", "nl", "column", "less", "more", "cat", "tr",
  "select-object", "select", "sort-object", "measure-object", "format-table", "format-list",
  "out-string", "out-host",
]);

/**
 * Split `text` into pieces at the top-level occurrences of any separator `isSep` accepts,
 * ignoring separators inside quotes. Returns absolute {start, end} spans.
 */
function splitTopLevel(text, isSep) {
  const spans = [];
  let start = 0;
  let quote = null;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quote) {
      if (ch === "\\" && quote === '"') i++;
      else if (ch === quote) quote = null;
      continue;
    }
    if (ch === "'" || ch === '"') {
      quote = ch;
      continue;
    }
    if (ch === "\\") {
      i++;
      continue;
    }
    const width = isSep(text, i);
    if (width > 0) {
      spans.push({ start, end: i });
      i += width - 1;
      start = i + 1;
    }
  }
  spans.push({ start, end: text.length });
  return spans;
}

/** Tokenise one simple command, recording where each word sits and whether it was quoted. */
function tokenise(text, offset) {
  const tokens = [];
  let i = 0;

  while (i < text.length) {
    while (i < text.length && /\s/.test(text[i])) i++;
    if (i >= text.length) break;

    const start = i;
    let value = "";
    let quoted = false;

    while (i < text.length && !/\s/.test(text[i])) {
      const ch = text[i];
      if (ch === "'" || ch === '"') {
        quoted = true;
        i++;
        while (i < text.length && text[i] !== ch) {
          if (ch === '"' && text[i] === "\\") i++;
          value += text[i];
          i++;
        }
        i++; // past the closing quote
        continue;
      }
      if (ch === "\\") {
        i++;
        value += text[i] ?? "";
        i++;
        continue;
      }
      value += ch;
      i++;
    }
    tokens.push({ value, quoted, start: offset + start, end: offset + i });
  }
  return tokens;
}

/** The executable a simple command runs, with any leading VAR=value assignments skipped. */
function commandHead(tokens) {
  const word = tokens.find((t) => t.quoted || !/^[A-Za-z_][A-Za-z0-9_]*=/.test(t.value));
  return word ? word.value.replace(/^.*[\\/]/, "").replace(/\.exe$/i, "").toLowerCase() : "";
}

/**
 * Blank the search pattern handed to grep and friends.
 *
 * Every rule below matches the command as text, so `grep -rn 'git merge' .claude/` reads
 * as a merge and gets refused — but searching for the name of a dangerous command is not
 * running one, the same principle that already drops heredoc bodies.
 *
 * Only the pattern is blanked, never the flags or paths: `grep KEY '.env'` still trips the
 * secrets rule. And only when nothing downstream could execute what the search prints, so
 * `grep DROP dump.sql | psql` keeps its text and stays blocked.
 */
function blankSearchPatterns(input) {
  const isPipe = (t, i) => (t[i] === "|" && t[i + 1] !== "|" && t[i - 1] !== "|" ? 1 : 0);
  const isBreak = (t, i) => {
    if (t[i] === "\n" || t[i] === ";") return 1;
    if ((t[i] === "&" || t[i] === "|") && t[i + 1] === t[i]) return 2;
    if (t[i] === "&") return 1;
    return 0;
  };

  const blanks = [];

  for (const piece of splitTopLevel(input, isBreak)) {
    const pieceText = input.slice(piece.start, piece.end);
    const stages = splitTopLevel(pieceText, isPipe).map((s) => {
      const offset = piece.start + s.start;
      return { tokens: tokenise(pieceText.slice(s.start, s.end), offset) };
    });

    stages.forEach((stage, index) => {
      if (!SEARCH_TOOLS.has(commandHead(stage.tokens))) return;
      const downstreamExecutes = stages
        .slice(index + 1)
        .some((later) => !TEXT_FILTERS.has(commandHead(later.tokens)));
      if (downstreamExecutes) return;

      // The pattern is the first argument that is not an option. `-e PATTERN` lands here
      // too, since the flag itself is skipped and its value is not.
      // PowerShell's Select-String names it instead: `-Pattern PATTERN`, wherever it sits.
      const args = stage.tokens.slice(1);
      const named = args.findIndex((t) => !t.quoted && /^-pattern$/i.test(t.value));
      const pattern = named >= 0 ? args[named + 1] : args.find((t) => t.quoted || !t.value.startsWith("-"));
      if (!pattern || !pattern.quoted) return;

      // `$(...)` and backticks run a command even inside quotes — never blank those.
      const raw = input.slice(pattern.start, pattern.end);
      if (/\$\(|`/.test(raw)) return;

      blanks.push(pattern);
    });
  }

  if (blanks.length === 0) return input;

  let out = "";
  let cursor = 0;
  for (const span of blanks.sort((a, b) => a.start - b.start)) {
    out += input.slice(cursor, span.start) + "''";
    cursor = span.end;
  }
  return out + input.slice(cursor);
}

/**
 * Remove PowerShell here-string bodies — `@'` / `@"` on the end of a line, closed by `'@` /
 * `"@` at the start of one. They are PowerShell's heredoc: data, not a command.
 */
function stripHereStrings(input) {
  const lines = input.split("\n");
  const out = [];
  let closer = null;
  for (const line of lines) {
    if (closer !== null) {
      if (line.startsWith(closer)) {
        closer = null;
        out.push(line.slice(2));
      }
      continue;
    }
    const open = line.match(/@(['"])\s*$/);
    if (open) closer = `${open[1]}@`;
    out.push(line);
  }
  return out.join("\n");
}

const scanned = blankSearchPatterns(stripHereStrings(stripHeredocs(command)));

const block = (reason) => {
  console.error(`Blocked: ${reason}`);
  process.exit(2);
};

// --- secrets: .env is never read through the shell ---------------------------
// settings.json denies the Read tool on .env, but the allow-list pre-approves
// `cat *`, `grep *`, `head *`, `sed -n *` — so the shell is the open door to whatever live
// keys the file holds. Any reference to a real dotenv file is refused; `*.example` files
// are shared templates and carry no secrets, so they stay readable.
const ENV_REF = /(?:^|[\s"'`=(:;|&/\\,])((?:[\w.:/\\-]*[/\\])?\.env(?:\.[\w-]+)*)/g;
const envRefs = [...scanned.matchAll(ENV_REF)]
  .map((m) => m[1])
  .filter((ref) => !/\.example$/.test(ref));

if (envRefs.length > 0) {
  // Existence checks reveal nothing, and creating .env from the template is the
  // documented bootstrap step — both stay allowed.
  const harmless =
    /^\s*(ls|stat|test|\[|Test-Path)\s/i.test(scanned) ||
    /^\s*(cp|Copy-Item)\s+(-[\w-]+\s+)*\.env\.example\s+\.env\s*$/i.test(scanned.trim());

  if (!harmless) {
    block(
      `this command touches ${envRefs[0]}, which holds live credentials. Never read, copy, ` +
        "or print it — not with cat, grep, sed, cp, or anything else. `.env.example` is the " +
        "shared template; use that. If you need a value from .env, ask the user.",
    );
  }
}

// The per-environment appsettings files are this stack's .env: they hold the database
// password and signing keys, and are not in source control. The base appsettings.json
// carries placeholders only and stays readable.
const APPSETTINGS_REF = /appsettings\.(Development|Testing|Production|Staging)\.json\b/i;
if (APPSETTINGS_REF.test(scanned) && !/^\s*(ls|stat|test|\[|Test-Path)\s/i.test(scanned)) {
  block(
    `this command touches ${scanned.match(APPSETTINGS_REF)[0]}, which holds live credentials. ` +
      "Never read, copy, or print it. appsettings.json is the shared template; use that. If you " +
      "need a value from the per-environment file, ask the user.",
  );
}

// --- irreversible database loss ---------------------------------------------
if (/\bdotnet[\s-]+ef\s+database\s+drop\b/i.test(scanned)) {
  block(
    "this drops the whole database — every row in it, with no undo. Only the user can ask for " +
      "it, in the turn it happens. To reshape the schema, add a migration instead.",
  );
}

if (/\bdocker\s+compose\s+down\b[^\n]*(-v\b|--volumes\b)/.test(scanned)) {
  block(
    "this drops the database volume — every row in it, with no undo. Only the user can ask " +
      "for it, in the turn it happens. Stop the stack without the volume flag to keep the " +
      "data.",
  );
}

if (/\bdocker\s+volume\s+(rm|prune)\b/.test(scanned)) {
  block(
    "removing a Docker volume destroys the data directory inside it — the same total loss " +
      "as `docker compose down -v`, by another route. Stop the stack without the volume " +
      "flag, which keeps the data.",
  );
}

if (/\b(DROP\s+(TABLE|DATABASE|SCHEMA)|TRUNCATE\s+(TABLE\s+)?\w)/i.test(scanned)) {
  block(
    "this contains a destructive SQL statement. Change the schema through this project's " +
      "own migration path, not by hand against the live database. If data really must be " +
      "dropped, say so and let the user decide.",
  );
}

if (/\bALTER\s+TABLE\s+[\w."]+\s+DROP\b/i.test(scanned)) {
  block(
    "dropping a column loses its data and cannot be undone. Change the schema through this " +
      "project's own migration path instead.",
  );
}

// DELETE has no schema equivalent to point at, so it is only refused where it actually
// runs against the database — a psql invocation. SQL inside application source reaches
// the disk through Write/Edit, not through here.
if (/\bpsql\b/.test(scanned) && /\bDELETE\s+FROM\b/i.test(scanned)) {
  block(
    "this deletes rows from the live database, and what it removes is usually expensive to " +
      "rebuild. Only the user can ask for it, in the turn it happens.",
  );
}

// --- destroying uncommitted work ----------------------------------------------
if (/\bgit\s+reset\b[^\n]*--hard\b/.test(scanned)) {
  block(
    "`git reset --hard` throws away every uncommitted change in the working tree, " +
      "including work this session has not shown the user yet. Use `git stash` if you need " +
      "a clean tree, or `git checkout -- <path>` for one file.",
  );
}

if (/\bgit\s+clean\b[^\n]*-[a-z]*[fx]/.test(scanned)) {
  block(
    "`git clean -f` deletes untracked files outright — including anything written this " +
      "session that was never staged. Remove the specific paths you meant instead.",
  );
}

// --- history rewrites and blind staging --------------------------------------
if (/\bgit\s+push\b[^\n]*(--force\b|(^|\s)-f(\s|$))/.test(scanned)) {
  block(
    "`git push --force` rewrites remote history. Push normally, or if the branch really " +
      "needs a rewrite, ask the user first.",
  );
}

if (/\bgit\s+add\s+(-A\b|--all\b|\.(\s|$)|\*(\s|$))/.test(scanned)) {
  block(
    "`git add -A` / `git add .` / `git add *` stages everything, including unrelated edits " +
      "and scratch " +
      "files. Run `git status` and stage the specific paths that belong to this change.",
  );
}

// --- nothing reaches the protected branch without the owner --------------------
// `git push origin HEAD:main` puts unreviewed work on the default branch without ever
// checking it out, so a check on the *current* branch would never see it. The destination
// of every refspec is inspected instead. Which branches are protected: the base branch pull
// requests target, `project.branch` when agentic.config.json names one, and the usual
// default names.
const PROTECTED_BRANCHES = new Set([BASE_BRANCH, config.project.branch, "main", "master"].filter(Boolean));
for (const segment of scanned.matchAll(/\bgit\s+push\b([^\n;&|]*)/g)) {
  const tokens = segment[1].trim().split(/\s+/).filter(Boolean);
  const positional = tokens.filter((t) => !t.startsWith("-"));
  for (const token of positional.slice(1)) {
    const destination = (token.includes(":") ? token.split(":").pop() : token)
      .replace(/^refs\/heads\//, "")
      .replace(/^\+/, "");
    if (PROTECTED_BRANCHES.has(destination)) {
      block(
        `this pushes straight to \`${destination}\`. That branch only ever moves when the ` +
          "repo owner says so, in the turn it happens. Leave the commits local and tell " +
          "them what is waiting to be pushed.",
      );
    }
  }
}

// Recovery is a real `git merge --abort` / `--continue`, and nothing else. Matching the
// bare flags anywhere in the command let any text that merely carries them slip past the
// two rules below: `git commit -m "handle --continue"` on main was allowed to commit.
// That is exactly how an over-broad exemption quietly disables the guard it belongs to.
const MERGE_RECOVERY = /\bgit\s+merge\s+--(?:abort|continue)\b/;

// --- merging a pull request is the repo owner's, always -----------------------
// They said it plainly: they merge their own PRs. No script, agent, or API call here
// may do it for them. This holds even under --permission-mode bypassPermissions.
if (/(?:api\.bitbucket\.org|api\.github\.com)[^\n]*\/(?:pullrequests|pulls)\/[^\n]*\/merge/.test(scanned)) {
  block(
    "this merges a pull request through a hosting API. Merging is the repo owner's " +
      "call — they merge their own PRs. Open the PR and hand them the URL instead.",
  );
}

// `\b` after the verb also fires on the read-only plumbing — `merge-base`, `merge-tree`,
// `merge-file` — none of which move a branch. Require the next character to be neither a
// word character nor a hyphen, so only the real merge is caught.
if (/\bgit\s+merge(?![-\w])/.test(scanned) && !MERGE_RECOVERY.test(scanned)) {
  block(
    "merging branches is the repo owner's call. Leave the branch as it is and give them " +
      "the PR URL (`npm run pr`) so they can review and merge it themselves.",
  );
}

if (/\bgh\s+pr\s+merge\b/.test(scanned)) {
  block("merging a pull request is the repo owner's call. Hand them the PR URL instead.");
}

// --- committing on the work branch is allowed, deliberately ----------------------
// The task queue builds in place on the work branch rather than on a branch per task, so a
// commit there is normal. The review gate did not disappear, it moved — from "before the
// merge" to "before the push". What still holds the line is the push rule above, which
// refuses a protected branch as a destination by any refspec, so local commits pile up and
// nothing reaches a protected branch until the owner pushes it themselves.
//
// `git merge` stays blocked by the dedicated rule above; nothing here re-permits it.

// --- rules this project added for itself --------------------------------------
// `hooks.deniedCommands` in agentic.config.json: `{ "pattern": "...", "message": "..." }`.
// Anything genuinely dangerous about *this* product — a script that writes to production,
// a command that spends money — belongs here rather than in a shared guard, and gets a
// block case and a neighbouring allow case in hook-tests.mjs like every rule above.
for (const rule of config.hooks.deniedCommands ?? []) {
  let pattern;
  try {
    pattern = new RegExp(rule.pattern, rule.flags ?? "");
  } catch {
    continue; // a broken rule must not take the whole guard down with it
  }
  if (pattern.test(scanned)) block(rule.message ?? `this matches a denied command pattern (${rule.pattern}).`);
}

process.exit(0);
