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
let tool = "";
try {
  const input = JSON.parse(raw);
  command = input?.tool_input?.command ?? "";
  tool = String(input?.tool_name ?? "");
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
const isPipe = (t, i) => (t[i] === "|" && t[i + 1] !== "|" && t[i - 1] !== "|" ? 1 : 0);
const isBreak = (t, i) => {
  if (t[i] === "\n" || t[i] === ";") return 1;
  if ((t[i] === "&" || t[i] === "|") && t[i + 1] === t[i]) return 2;
  if (t[i] === "&") return 1;
  return 0;
};

function blankSearchPatterns(input) {
  const blanks = [];

  for (const piece of splitTopLevel(input, isBreak)) {
    const pieceText = input.slice(piece.start, piece.end);
    const stages = splitTopLevel(pieceText, isPipe).map((s) => {
      const offset = piece.start + s.start;
      return { tokens: tokenise(pieceText.slice(s.start, s.end), offset) };
    });

    stages.forEach((stage, index) => {
      // Text a later stage could run is never blanked: `git commit -m "…" | sh` prints the
      // message on its summary line, and `grep 'DROP …' f | psql` hands the pattern on.
      const downstreamExecutes = stages
        .slice(index + 1)
        .some((later) => !TEXT_FILTERS.has(commandHead(later.tokens)));
      if (downstreamExecutes) return;

      // A commit or tag message is prose about the change, and prose names commands:
      // "never git merge here" was refused as a merge. Its quoted value is blanked like a
      // search pattern — the rest of the command, every flag and path, is still scanned.
      // Not under PowerShell, whose strings do not end where this tokeniser's do: `\"` is
      // an escaped quote to bash and a backslash then a closing quote to PowerShell, so a
      // message could hide a command PowerShell then runs.
      if (commandHead(stage.tokens) === "git") {
        if (/powershell/i.test(tool)) return;
        const args = stage.tokens.slice(1);
        // Past git's global options, the same ones stripGitGlobals sees through.
        let at = 0;
        while (at < args.length && args[at].value.startsWith("-")) {
          at += /^(-C|-c|--git-dir|--work-tree|--namespace|--exec-path|--config-env)$/.test(args[at].value) ? 2 : 1;
        }
        const verb = args[at];
        if (verb && /^(commit|tag)$/.test(verb.value)) {
          stage.tokens.forEach((t, i) => {
            const next = stage.tokens[i + 1];
            const takesMessage = /^-[a-zA-Z]*m$/.test(t.value) || t.value === "--message";
            const candidate = takesMessage ? next : /^(-m|--message=)./.test(t.value) && t.quoted ? t : null;
            if (!candidate?.quoted) return;
            if (/\$\(|`|\\"/.test(input.slice(candidate.start, candidate.end))) return;
            blanks.push(candidate);
          });
        }
        return;
      }
      if (!SEARCH_TOOLS.has(commandHead(stage.tokens))) return;

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

/**
 * Every simple command in the line, separately: split at `;`, `&&`, `||`, `&`, newlines and
 * pipes. An exemption is judged per segment. Judged on the whole line, `ls . && cat .env`
 * was an "existence check" because it happened to start with `ls`.
 */
const segments = [];
for (const piece of splitTopLevel(scanned, isBreak)) {
  const text = scanned.slice(piece.start, piece.end);
  for (const stage of splitTopLevel(text, isPipe)) {
    const s = text.slice(stage.start, stage.end).trim();
    if (s) segments.push(s);
  }
}
const EXISTENCE_CHECK = /^\s*(ls|stat|test|\[|Test-Path)\s/i;

/**
 * The line with git's global options removed, so a rule anchored on `git <verb>` sees the
 * verb. `git -C . reset --hard`, `git -c k=v merge x` and `git -C . push origin HEAD:main`
 * each put an option between `git` and its verb, and every git rule below missed them.
 */
const GIT_OPTION_WITH_VALUE =
  /^\s+(?:-C|-c|--git-dir|--work-tree|--namespace|--exec-path|--config-env)(?:=(?:"[^"]*"|'[^']*'|\S+)|\s+(?:"[^"]*"|'[^']*'|\S+))/;
const GIT_OPTION_FLAG = /^\s+(?:--no-pager|--paginate|--bare|--no-replace-objects|--literal-pathspecs|--no-optional-locks|-P|-p)(?=\s|$)/;
function stripGitGlobals(text) {
  let out = "";
  let cursor = 0;
  const git = /\bgit(?=\s)/g;
  for (let m = git.exec(text); m; m = git.exec(text)) {
    const after = m.index + 3;
    out += text.slice(cursor, after);
    let rest = text.slice(after);
    for (let opt = rest.match(GIT_OPTION_WITH_VALUE) ?? rest.match(GIT_OPTION_FLAG); opt; opt = rest.match(GIT_OPTION_WITH_VALUE) ?? rest.match(GIT_OPTION_FLAG)) {
      rest = rest.slice(opt[0].length);
    }
    cursor = text.length - rest.length;
    git.lastIndex = cursor;
  }
  return out + text.slice(cursor);
}
const gitScanned = stripGitGlobals(scanned);

/** The branch checked out here — what a bare `git push` sends, under git's default push mode. */
function currentBranch() {
  try {
    return execSync("git rev-parse --abbrev-ref HEAD", { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return "";
  }
}

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
  // documented bootstrap step — both stay allowed. Judged per segment: each segment that
  // names a dotenv file must itself be one of the two.
  const harmless = (s) =>
    EXISTENCE_CHECK.test(s) || /^\s*(cp|Copy-Item)\s+(-[\w-]+\s+)*\.env\.example\s+\.env\s*$/i.test(s);
  const offending = segments.find((s) => [...s.matchAll(ENV_REF)].some((m) => !/\.example$/.test(m[1])) && !harmless(s));

  if (offending) {
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
if (segments.some((s) => APPSETTINGS_REF.test(s) && !EXISTENCE_CHECK.test(s))) {
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

// `docker-compose` (the v1 binary) as well as `docker compose`, and options such as
// `-f docker-compose.prod.yml` between it and `down`.
if (/\bdocker[\s-]+compose\b[^\n;&|]*\sdown\b[^\n;&|]*(\s-v\b|--volumes\b)/.test(scanned)) {
  block(
    "this drops the database volume — every row in it, with no undo. Only the user can ask " +
      "for it, in the turn it happens. Stop the stack without the volume flag to keep the " +
      "data.",
  );
}

// Reverting to migration 0 runs every Down() — each table dropped in turn. The same loss
// as `database drop`, spelled as an update.
// The target is the first positional argument wherever it sits among the options.
if (/\bdotnet[\s-]+ef\s+database\s+update\b[^\n;&|]*?\s0(?=\s|$|[;&|])/i.test(scanned)) {
  block(
    "`dotnet ef database update 0` reverts every migration, dropping every table and its rows. " +
      "Only the user can ask for it, in the turn it happens. To undo one migration, name the one before it.",
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
if (/\bgit\s+reset\b[^\n]*--hard\b/.test(gitScanned)) {
  block(
    "`git reset --hard` throws away every uncommitted change in the working tree, " +
      "including work this session has not shown the user yet. Use `git stash` if you need " +
      "a clean tree, or `git checkout -- <path>` for one file.",
  );
}

if (/\bgit\s+clean\b[^\n]*-[a-z]*[fx]/.test(gitScanned)) {
  block(
    "`git clean -f` deletes untracked files outright — including anything written this " +
      "session that was never staged. Remove the specific paths you meant instead.",
  );
}

// `git checkout -- .` and `git restore .` are `reset --hard` for the working tree, by
// another name. Naming files is fine; a whole-tree pathspec is not. `restore --staged`
// alone only unstages, and stays allowed.
for (const s of segments.map(stripGitGlobals)) {
  const m = /\bgit\s+(checkout|restore)\b(.*)$/.exec(s);
  if (!m) continue;
  const args = m[2].trim().split(/\s+/).filter(Boolean).map((t) => t.replace(/^['"]|['"]$/g, ""));
  const wholeTree = args.some((t) => t === "." || t === "./" || t === "*" || t === ":/");
  const unstageOnly = m[1] === "restore" && args.some((t) => /^(--staged|-S)$/.test(t)) && !args.some((t) => /^(--worktree|-W)$/.test(t));
  if (wholeTree && !unstageOnly) {
    block(
      `\`git ${m[1]} .\` throws away every uncommitted change in the tree, the same loss as ` +
        "`git reset --hard`. Name the files you mean, or `git stash` to set work aside.",
    );
  }
}

if (/\bgit\s+stash\s+clear\b/.test(gitScanned)) {
  block("`git stash clear` deletes every stash at once, with no undo. Drop the one you mean by name.");
}

// `find` and `sed -n` are pre-approved in settings.json as reads. `find` writes through
// -delete and its -fprint family, and runs anything through -exec; only the read-only
// commands below may follow -exec unprompted. For `sed -n`, the common write (-i) is refused
// here; its script-level `w`/`e` commands are not parsed, which is why the allow-list stays
// narrow to the `-n` form.
const FIND_READ_ONLY_EXEC = /^(grep|egrep|fgrep|rg|cat|head|tail|wc|ls|stat|file|echo|printf|basename|dirname|realpath|sha1sum|sha256sum|md5sum)$/;
for (const s of segments) {
  if (!/^\s*find\b/.test(s)) continue;
  const runs = [...s.matchAll(/\s-(?:exec|execdir|ok|okdir)\s+(\S+)/g)].map((m) => m[1].replace(/^.*[\\/]/, ""));
  if (/\s-(delete|fprint0?|fprintf|fls)\b/.test(s) || runs.some((cmd) => !FIND_READ_ONLY_EXEC.test(cmd))) {
    block(
      "`find` is approved as a read, and this makes it write or run something — `-delete`, `-fprint`, or " +
        "`-exec` with a command that is not a read. List the paths, then act on them by name.",
    );
  }
}
if (segments.some((s) => /^\s*sed\s+-n\b/.test(s) && /\s(-[a-zA-Z]*i\b|--in-place\b)/.test(s))) {
  block("`sed -n` is approved as a read; with `-i` it edits files in place without the prompt. Use the Edit tool.");
}

// `git branch -D` deletes a branch whether or not it was merged — the only copy of its
// commits, if it was never pushed. `-d` refuses an unmerged branch and stays allowed.
if (/\bgit\s+branch\b[^\n;&|]*\s(-[a-zA-Z]*D[a-zA-Z]*\b|--delete\s+--force\b|--force\s+--delete\b)/.test(gitScanned)) {
  block("`git branch -D` deletes a branch even when its commits are merged nowhere. Use `git branch -d`, which refuses an unmerged branch.");
}

// --- history rewrites and blind staging --------------------------------------
// `-f` bundled with other short flags (`-uf`) and a `+` on a refspec force just the same.
const pushSegments = [...gitScanned.matchAll(/\bgit\s+push\b([^\n;&|]*)/g)].map((m) => m[1]);
if (
  pushSegments.some(
    (args) =>
      /(--force\b|--force-with-lease\b|--force-if-includes\b|(^|\s)-[a-zA-Z]*f[a-zA-Z]*(\s|$))/.test(args) ||
      args.trim().split(/\s+/).some((t) => /^\+\S/.test(t)),
  )
) {
  block(
    "`git push --force` rewrites remote history. Push normally, or if the branch really " +
      "needs a rewrite, ask the user first.",
  );
}

// Blind staging, however it is spelled: `-A`, `--all`, bundled (`-vA`), or `.`/`*` after a
// `--` separator.
if (/\bgit\s+add\b(?:\s+-[\w-]+)*(?:\s+--)?\s+(-[a-zA-Z]*A[a-zA-Z]*\b|--all\b|\.\/?(\s|$)|\*(\s|$))/.test(gitScanned)) {
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
//
// A push that names no refspec — `git push`, `git push origin` — sends the current branch,
// and so does a refspec of `HEAD`. Checking only named refspecs let a bare `git push` from a
// checkout of main through, and main is the work branch whenever `project.branch` is unset.
const PROTECTED_BRANCHES = new Set([BASE_BRANCH, config.project.branch, "main", "master"].filter(Boolean));
for (const args of pushSegments) {
  // Quotes are the shell's, not the ref's: `'main'`, `HEAD:"main"` and the `main"` left by
  // `bash -c "git push origin main"` all push to main, and each slipped past unstripped.
  const tokens = args
    .trim()
    .split(/\s+/)
    .map((t) => t.replace(/["']/g, ""))
    .filter(Boolean);
  // Options that take a value, so the value is not read as a remote or refspec.
  const positional = [];
  for (let i = 0; i < tokens.length; i++) {
    if (/^(-o|--push-option|--repo|--receive-pack|--exec)$/.test(tokens[i])) {
      i++;
      continue;
    }
    if (!tokens[i].startsWith("-")) positional.push(tokens[i]);
  }
  const refspecs = positional.length > 1 ? positional.slice(1) : ["HEAD"];
  const pushesAll = tokens.some((t) => /^--(all|mirror)$/.test(t));
  for (const token of pushesAll ? [...PROTECTED_BRANCHES] : refspecs) {
    let destination = (token.includes(":") ? token.split(":").pop() : token)
      .replace(/^\+/, "")
      .replace(/^refs\/heads\//, "");
    if (destination === "HEAD" || destination === "@") destination = currentBranch();
    if (PROTECTED_BRANCHES.has(destination)) {
      block(
        `this pushes straight to \`${destination}\`. That branch only ever moves when the ` +
          "repo owner moves it. " +
          (config.project.workflow === "team"
            ? "In a team it moves through a pull request: push a branch of your own and give them the PR URL (`npm run pr`)."
            : "Leave the commits local, tell them what is waiting, and give them the command to run " +
              "themselves (`! git push` in this prompt)."),
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
// `gh api` reaches the same endpoint with no host in the command, and GraphQL by mutation name.
if (
  /(?:api\.bitbucket\.org|api\.github\.com)[^\n]*\/(?:pullrequests|pulls)\/[^\n]*\/merge/.test(scanned) ||
  /\bgh\s+api\b[^\n]*\/pulls\/[^\s/]+\/merge\b/.test(scanned) ||
  /\bgh\s+api\b[^\n]*\bmergePullRequest\b/.test(scanned)
) {
  block(
    "this merges a pull request through a hosting API. Merging is the repo owner's " +
      "call — they merge their own PRs. Open the PR and hand them the URL instead.",
  );
}

// `\b` after the verb also fires on the read-only plumbing — `merge-base`, `merge-tree`,
// `merge-file` — none of which move a branch. Require the next character to be neither a
// word character nor a hyphen, so only the real merge is caught.
if (/\bgit\s+merge(?![-\w])/.test(gitScanned) && !MERGE_RECOVERY.test(gitScanned)) {
  block(
    config.project.workflow === "team"
      ? "merging branches is the repo owner's call. Leave the branch as it is and give them " +
          "the PR URL (`npm run pr`) so they can review and merge it themselves."
      : "merging branches is the repo owner's call. Leave the branch as it is and give them " +
          "the exact command to run themselves (`! git merge --ff-only <branch>` in this prompt).",
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
