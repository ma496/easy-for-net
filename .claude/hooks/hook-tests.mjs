#!/usr/bin/env node
/**
 * Tests for the .claude/ hooks. Run: node .claude/hooks/hook-tests.mjs (or `npm run gate`).
 *
 * Hooks fail closed and run on every tool call, so a broken one either blocks all work
 * or silently stops guarding. Both failure modes are invisible without this.
 *
 * Every rule in guard-bash.mjs has a block case AND a neighbouring allow case. The allow
 * cases are the point: a guard that blocks everything is as useless as one that blocks
 * nothing, and over-broad regexes are how these rules actually rot.
 */
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { mkdirSync, mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";

const HOOKS_DIR = dirname(fileURLToPath(import.meta.url));
const HOOK = join(HOOKS_DIR, "guard-bash.mjs");
const CONVENTIONS = join(HOOKS_DIR, "project-conventions.mjs");
const PATHS_GUARD = join(HOOKS_DIR, "guard-protected-paths.mjs");

// Assembled at runtime so this file can be read, grepped, and edited by an agent whose
// own Bash calls are screened by the very guard under test.
const DESTRUCTIVE = ["docker", "compose", "down", "-v"].join(" ");
const MERGE = "mer" + "ge";
const DOTENV = ".env";
const DEV_SETTINGS = ["appsettings", "Development", "json"].join(".");
const BS = "\\"; // one backslash, for Windows-shaped paths

const cases = [
  // --- heredoc handling: documenting a command is not running it ---------------
  { label: "real destructive compose command", command: DESTRUCTIVE, expect: "block" },
  {
    label: "heredoc that only documents it",
    command: `cat > doc.md <<'EOF'\nNever run ${DESTRUCTIVE} here.\nEOF`,
    expect: "allow",
  },
  {
    label: "heredoc body, then the real command after",
    command: `cat > doc.md <<'EOF'\nharmless text\nEOF\n${DESTRUCTIVE}`,
    expect: "block",
  },
  { label: "quoted python heredoc with docs", command: `python3 - <<'PY'\n# ${DESTRUCTIVE}\nPY`, expect: "allow" },

  // --- secrets -----------------------------------------------------------------
  { label: "cat the live env file", command: `cat ${DOTENV}`, expect: "block" },
  { label: "grep a key out of the env file", command: `grep OPENAI_API_KEY ${DOTENV}`, expect: "block" },
  { label: "copy the env file elsewhere", command: `cp ${DOTENV} /tmp/x`, expect: "block" },
  { label: "env file via a pipe", command: `sed -n '1,5p' ${DOTENV} | head`, expect: "block" },
  { label: "env file in a subdirectory", command: `cat services/api/${DOTENV}`, expect: "block" },
  { label: "env.local is also secret", command: `cat ${DOTENV}.local`, expect: "block" },
  { label: "append to the env file", command: `echo K=v >> ${DOTENV}`, expect: "block" },
  { label: "the example template is shared", command: `cat ${DOTENV}.example`, expect: "allow" },
  { label: "the prod example is shared too", command: `cat ${DOTENV}.prod.example`, expect: "allow" },
  { label: "existence check reveals nothing", command: `ls -la ${DOTENV}`, expect: "allow" },
  { label: "documented bootstrap copy", command: `cp ${DOTENV}.example ${DOTENV}`, expect: "allow" },
  { label: "env file by a Windows path", command: `type D:${BS}repo${BS}${DOTENV}`, expect: "block" },
  { label: "env file via .\\ in PowerShell", command: `Get-Content .${BS}${DOTENV}`, expect: "block" },
  { label: "PowerShell reads the template", command: `Get-Content .${BS}${DOTENV}.example`, expect: "allow" },
  { label: "PowerShell existence check", command: `Test-Path ${DOTENV}`, expect: "allow" },
  { label: "PowerShell bootstrap copy", command: `Copy-Item ${DOTENV}.example ${DOTENV}`, expect: "allow" },
  { label: "PowerShell env variable is not the file", command: "$env:ASPNETCORE_ENVIRONMENT = 'Development'", expect: "allow" },
  { label: "reading per-env appsettings", command: `cat src/backend/Source/${DEV_SETTINGS}`, expect: "block" },
  { label: "per-env appsettings in PowerShell", command: `Get-Content src${BS}backend${BS}Source${BS}${DEV_SETTINGS}`, expect: "block" },
  { label: "base appsettings.json is shared", command: "cat src/backend/Source/appsettings.json", expect: "allow" },
  { label: "per-env appsettings existence check", command: `Test-Path src/backend/Source/${DEV_SETTINGS}`, expect: "allow" },

  // --- database loss -------------------------------------------------------------
  { label: "TRUNCATE via psql", command: 'psql -c "TRUNCATE TABLE accounts"', expect: "block" },
  { label: "DROP TABLE", command: 'psql -c "DROP TABLE documents"', expect: "block" },
  { label: "removing a database volume", command: "docker volume rm app_pg", expect: "block" },
  { label: "pruning volumes", command: "docker volume prune -f", expect: "block" },
  { label: "listing volumes is read-only", command: "docker volume ls", expect: "allow" },
  { label: "DELETE FROM through psql", command: 'psql -c "DELETE FROM tenants"', expect: "block" },
  { label: "dropping a column", command: 'psql -c "ALTER TABLE tenants DROP COLUMN name"', expect: "block" },
  { label: "SELECT is read-only", command: 'psql -c "SELECT count(*) FROM tenants"', expect: "allow" },
  { label: "npm run docker:down (keeps data)", command: "npm run docker:down", expect: "allow" },
  { label: "dropping the EF database", command: "dotnet ef database drop --force", expect: "block" },
  { label: "updating the EF database", command: "dotnet ef database update", expect: "allow" },
  { label: "adding an EF migration", command: "dotnet ef migrations add AddOrders", expect: "allow" },

  // --- destroying uncommitted work -----------------------------------------------
  { label: "git reset --hard", command: "git reset --hard origin/main", expect: "block" },
  { label: "git reset --soft keeps the tree", command: "git reset --soft HEAD~1", expect: "allow" },
  { label: "git clean -fd", command: "git clean -fd", expect: "block" },

  // --- staging and history --------------------------------------------------------
  { label: "git add -A", command: "git add -A", expect: "block" },
  { label: "git add with a glob", command: "git add *", expect: "block" },
  { label: "git add explicit path", command: "git add src/config.ts", expect: "allow" },
  { label: "git add a path with a wildcard", command: "git add src/*.ts", expect: "allow" },
  { label: "git push --force", command: "git push --force origin feat/x", expect: "block" },
  { label: "git push --force-with-lease", command: "git push --force-with-lease origin feat/x", expect: "block" },

  // --- nothing lands on main except through a PR ------------------------------------
  { label: "refspec push to main", command: "git push origin HEAD:main", expect: "block" },
  { label: "branch pushed onto main", command: "git push origin feat/x:main", expect: "block" },
  { label: "fully-qualified refspec to main", command: "git push origin HEAD:refs/heads/main", expect: "block" },
  { label: "pushing the main ref", command: "git push origin master", expect: "block" },
  { label: "normal feature-branch push", command: "git push -u origin feat/x", expect: "allow" },
  { label: "PowerShell refspec push to master", command: "git push origin HEAD:master; Write-Host done", expect: "block" },
  { label: "branch merely named like main", command: "git push -u origin feat/main-nav", expect: "allow" },

  // --- merging is the owner's, always -------------------------------------------------
  { label: `git ${MERGE}`, command: `git ${MERGE} feat/x`, expect: "block" },
  { label: `git ${MERGE} --abort is recovery`, command: `git ${MERGE} --abort`, expect: "allow" },
  {
    label: `git ${MERGE}-base only reads`,
    command: `git ${MERGE}-base HEAD origin/main`,
    expect: "allow",
  },
  {
    label: `git ${MERGE}-tree only reads`,
    command: `git ${MERGE}-tree --write-tree HEAD origin/main`,
    expect: "allow",
  },
  {
    label: `git ${MERGE} with no argument still blocks`,
    command: `git ${MERGE}`,
    expect: "block",
  },
  { label: `gh pr ${MERGE}`, command: `gh pr ${MERGE} 12 --squash`, expect: "block" },
  {
    label: `bitbucket ${MERGE} API`,
    command: `curl -X POST https://api.bitbucket.org/2.0/repositories/o/r/pullrequests/1/${MERGE}`,
    expect: "block",
  },
  {
    label: `github ${MERGE} API`,
    command: `curl -X PUT https://api.github.com/repos/o/r/pulls/1/${MERGE}`,
    expect: "block",
  },
  {
    label: "reading a PR is fine",
    command: "curl -s https://api.bitbucket.org/2.0/repositories/o/r/pullrequests/1",
    expect: "allow",
  },

  // --- searching for a command is not running it -----------------------------------------
  // The pattern handed to grep is data. The paths beside it are not, and neither is
  // anything downstream that could execute what the search prints.
  { label: "grep for the merge verb", command: `grep -rn 'git ${MERGE}' .claude/`, expect: "allow" },
  {
    label: "search piped to a text filter",
    command: `rg 'git push --force' docs/ | head -20`,
    expect: "allow",
  },
  {
    label: "grep for the destructive compose command",
    command: `grep -rn '${DESTRUCTIVE}' docs/`,
    expect: "allow",
  },
  {
    label: "quoting the env path does not hide it",
    command: `grep OPENAI_API_KEY '${DOTENV}'`,
    expect: "block",
  },
  {
    label: "search feeding psql keeps its text",
    command: `grep 'DELETE FROM accounts' dump.sql | psql`,
    expect: "block",
  },
  {
    label: "command substitution inside the pattern still runs",
    command: `grep "$(git ${MERGE} feat/x)" notes.md`,
    expect: "block",
  },
  { label: "echo is not a search tool", command: `echo '${DESTRUCTIVE}' | sh`, expect: "block" },
  {
    label: "Select-String for the merge verb",
    command: `Select-String -Path .claude${BS}*.md -Pattern 'git ${MERGE}' | Select-Object -First 5`,
    expect: "allow",
  },
  {
    label: "PowerShell here-string that documents it",
    command: `@'\nNever run ${DESTRUCTIVE} here.\n'@ | Set-Content doc.md`,
    expect: "allow",
  },
  {
    label: "PowerShell here-string, then the real command",
    command: `@'\nharmless\n'@ | Set-Content doc.md\n${DESTRUCTIVE}`,
    expect: "block",
  },
  {
    label: "unquoted verb after a search is still real",
    command: `grep -rn 'notes' docs/ && git ${MERGE} feat/x`,
    expect: "block",
  },

  // --- ordinary work must stay unblocked -----------------------------------------------
  { label: "npm run gate", command: "npm run gate", expect: "allow" },
  { label: "npm run verify", command: "npm run verify", expect: "allow" },
  // NOTE: no commit/branch case belongs in this array. These run the guard against
  // whatever repo the suite happens to sit in, and the branch rule asks git what is checked
  // out — so an "allow" assertion here passes on a feature branch and fails on main, making
  // the suite report on the developer's checkout rather than on the guard. The real
  // both-directions coverage is in `branchCases` below, which builds a throwaway repo and
  // checks out the branch each case actually means.
];

let failures = 0;
/** Cases counted outside the four arrays, for the final tally. */
let extraCases = 0;
const report = (ok, label, expect, actual) => {
  if (!ok) failures++;
  console.log(`${ok ? "PASS" : "FAIL"}  ${label.padEnd(38)} expected=${expect} actual=${actual}`);
};

for (const c of cases) {
  const res = spawnSync("node", [HOOK], {
    input: JSON.stringify({ tool_input: { command: c.command } }),
    encoding: "utf8",
  });
  const actual = res.status === 0 ? "allow" : "block";
  report(actual === c.expect, c.label, c.expect, actual);
}

// --- the branch check needs a real repo to look at ---------------------------------
// guard-bash asks git for the current branch, so this case is only meaningful with a
// working tree actually sitting on main.
const repo = mkdtempSync(join(tmpdir(), "hooktest-repo-"));
const git = (...args) => spawnSync("git", args, { cwd: repo, encoding: "utf8" });
git("init", "-b", "main");
git("config", "user.email", "test@example.com");
git("config", "user.name", "Hook Test");
git("commit", "--allow-empty", "-m", "init");

const branchCases = [
  // Committing on main is allowed by design: work happens on the default branch in place,
  // and what keeps the owner's review ahead of the code is the push rule, not a commit rule.
  { label: "commit on main is allowed now", branch: "main", expect: "allow" },
  {
    label: "commit-tree on main only writes an object",
    branch: "main",
    command: "git commit-tree $TREE",
    expect: "allow",
  },
  // The merge-recovery exemption is scoped to `git merge --abort` / `--continue`. These
  // three pin that scope: the flag inside a merge message must not buy an exemption,
  // and the genuine recovery command must still get one.
  {
    label: "--continue inside a merge message buys no exemption",
    branch: "main",
    command: 'git merge -m "handle --continue properly" other',
    expect: "block",
  },
  {
    label: "--abort inside a merge message buys no exemption",
    branch: "main",
    command: 'git merge -m "fix the --abort path" other',
    expect: "block",
  },
  {
    label: "git merge --abort on main is still recovery",
    branch: "main",
    command: "git merge --abort",
    expect: "allow",
  },
  { label: "commit on a feature branch", branch: "feat/x", expect: "allow" },
];

for (const c of branchCases) {
  if (c.branch !== "main") git("checkout", "-b", c.branch);
  const res = spawnSync("node", [HOOK], {
    input: JSON.stringify({ tool_input: { command: c.command ?? 'git commit -m "x"' } }),
    encoding: "utf8",
    cwd: repo,
  });
  const actual = res.status === 0 ? "allow" : "block";
  report(actual === c.expect, c.label, c.expect, actual);
}

// A notebook edit names its target as notebook_path, and the path guard reads that too.
{
  const res = spawnSync("node", [PATHS_GUARD], {
    input: JSON.stringify({ tool_input: { notebook_path: `/repo/${DOTENV}` } }),
    encoding: "utf8",
  });
  report(res.status !== 0, "notebook edit of the env file", "block", res.status === 0 ? "allow" : "block");
  extraCases += 1;
}

// --- project-conventions.mjs --------------------------------------------------
// The rules are a project's own, so the fixture ships its own config and points the hook
// at it with AGENTIC_CONFIG. The two below are the classic pair: a suffix the compiler
// does not require, and an environment variable read outside the one file allowed to.
const scratch = mkdtempSync(join(tmpdir(), "hooktest-"));
const fixtureConfig = join(scratch, "agentic.config.json");
writeFileSync(
  fixtureConfig,
  JSON.stringify({
    hooks: {
      conventions: [
        {
          paths: ["/src/.*\\.ts$"],
          forbid: "from\\s*[\"'](\\.[^\"']*)[\"']",
          allowWhen: "\\.(js|json)[\"']$",
          message: "relative imports need an explicit .js suffix",
        },
        {
          paths: ["/src/.*\\.ts$"],
          except: ["/src/config\\.ts$"],
          forbid: "process\\.env\\b",
          message: "process.env is read outside config.ts",
        },
      ],
    },
  }),
);

const conventionCases = [
  {
    label: "relative import missing .js",
    file: "src/thing.ts",
    source: 'import { config } from "./config";\n',
    expect: "block",
  },
  {
    label: "relative import with .js",
    file: "src/thing.ts",
    source: 'import { config } from "./config.js";\n',
    expect: "allow",
  },
  {
    label: "process.env outside config.ts",
    file: "src/thing.ts",
    source: "const k = process.env.FOO;\n",
    expect: "block",
  },
  {
    label: "process.env inside config.ts",
    file: "src/config.ts",
    source: "const k = process.env.FOO;\n",
    expect: "allow",
  },
  {
    label: "package import needs no suffix",
    file: "src/thing.ts",
    source: 'import OpenAI from "openai";\n',
    expect: "allow",
  },
  {
    label: "a file outside the watched paths is not judged",
    file: "docs/thing.ts",
    source: "const k = process.env.FOO;\n",
    expect: "allow",
  },
];

for (const c of conventionCases) {
  const realPath = join(scratch, c.file);
  mkdirSync(dirname(realPath), { recursive: true });
  writeFileSync(realPath, c.source);
  const res = spawnSync("node", [CONVENTIONS], {
    input: JSON.stringify({ tool_input: { file_path: realPath } }),
    encoding: "utf8",
    env: { ...process.env, AGENTIC_CONFIG: fixtureConfig },
  });
  const actual = res.status === 0 ? "allow" : "block";
  report(actual === c.expect, c.label, c.expect, actual);
}

// --- guard-protected-paths.mjs ------------------------------------------------
const pathCases = [
  { label: "writing the live env file", file: DOTENV, expect: "block" },
  { label: "writing the env template", file: `${DOTENV}.example`, expect: "allow" },
  { label: "writing build output", file: "/repo/dist/app.js", expect: "block" },
  { label: "writing source", file: "/repo/src/app.ts", expect: "allow" },
  { label: "env file by a Windows path", file: `D:${BS}repo${BS}src${BS}${DOTENV}`, expect: "block" },
  { label: "env template by a Windows path", file: `D:${BS}repo${BS}${DOTENV}.example`, expect: "allow" },
  { label: "writing .NET build output", file: `D:${BS}repo${BS}src${BS}backend${BS}Source${BS}obj${BS}x.cs`, expect: "block" },
  { label: "writing Next.js build output", file: "/repo/src/frontend/web/.next/server/app.js", expect: "block" },
  { label: "writing per-env appsettings", file: `/repo/src/backend/Source/${DEV_SETTINGS}`, expect: "block" },
  { label: "writing base appsettings.json", file: "/repo/src/backend/Source/appsettings.json", expect: "allow" },
  { label: "a folder merely named like binary", file: "/repo/src/binaries/readme.md", expect: "allow" },
];

for (const c of pathCases) {
  const res = spawnSync("node", [PATHS_GUARD], {
    input: JSON.stringify({ tool_input: { file_path: c.file } }),
    encoding: "utf8",
  });
  const actual = res.status === 0 ? "allow" : "block";
  report(actual === c.expect, c.label, c.expect, actual);
}

console.log("");
if (failures > 0) {
  console.error(`${failures} hook case(s) failed. A guard is not behaving as documented.`);
  process.exit(1);
}
console.log(
  `All ${cases.length + branchCases.length + conventionCases.length + pathCases.length + extraCases} hook cases pass.`,
);
