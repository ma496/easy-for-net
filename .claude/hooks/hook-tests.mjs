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

/**
 * What Claude Code makes of a hook's exit: 0 lets the call through, 2 refuses it, and any
 * other status is a broken hook that Claude Code reports and then *ignores*. Counting that
 * third case as a block is how a guard that had stopped guarding passed here.
 */
const verdict = (res) => (res.status === 0 ? "allow" : res.status === 2 ? "block" : `crash (exit ${res.status})`);

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
  // An exemption covers its own segment, not the whole line: a leading `ls` used to excuse
  // everything after it.
  { label: "ls, then cat the env file", command: `ls . && cat ${DOTENV}`, expect: "block" },
  { label: "test, then read per-env appsettings", command: `test -f x; cat src/backend/Source/${DEV_SETTINGS}`, expect: "block" },
  { label: "ls piped into a read of the env file", command: `ls | cat ${DOTENV}`, expect: "block" },
  { label: "two existence checks stay allowed", command: `ls ${DOTENV} && test -f ${DOTENV}`, expect: "allow" },
  { label: "ls of the env file, then unrelated work", command: `ls -la ${DOTENV}; npm run build`, expect: "allow" },
  { label: "existence check, then the bootstrap copy", command: `Test-Path ${DOTENV}; Copy-Item ${DOTENV}.example ${DOTENV}`, expect: "allow" },

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
  // Global options between `git` and its verb, bundled short flags, `+` refspecs, `--`.
  { label: "git -C reset --hard", command: "git -C . reset --hard", expect: "block" },
  { label: "git -C with a quoted path, reset --hard", command: 'git -C "my repo" reset --hard HEAD', expect: "block" },
  { label: "git -C reset --soft keeps the tree", command: "git -C . reset --soft HEAD~1", expect: "allow" },
  { label: "git --no-pager clean -fd", command: "git --no-pager clean -fd", expect: "block" },
  { label: "git -C status only reads", command: "git -C src status", expect: "allow" },
  { label: "bundled -uf is a force push", command: "git push -uf origin feat/x", expect: "block" },
  { label: "a + refspec is a force push", command: "git push origin +feat/x", expect: "block" },
  { label: "bundled -u alone is not force", command: "git push -u origin feat/fix-flags", expect: "allow" },
  { label: "git add -- .", command: "git add -- .", expect: "block" },
  { label: "git add -vA", command: "git add -vA", expect: "block" },
  { label: "git add ./", command: "git add ./", expect: "block" },
  { label: "git add -- one path", command: "git add -- src/app.ts", expect: "allow" },
  { label: "git add -p one path", command: "git add -p src/app.ts", expect: "allow" },
  { label: "git -c k=v add -A", command: "git -c core.autocrlf=false add -A", expect: "block" },

  // --- nothing lands on main except through a PR ------------------------------------
  { label: "refspec push to main", command: "git push origin HEAD:main", expect: "block" },
  { label: "branch pushed onto main", command: "git push origin feat/x:main", expect: "block" },
  { label: "fully-qualified refspec to main", command: "git push origin HEAD:refs/heads/main", expect: "block" },
  { label: "pushing the main ref", command: "git push origin master", expect: "block" },
  { label: "normal feature-branch push", command: "git push -u origin feat/x", expect: "allow" },
  { label: "PowerShell refspec push to master", command: "git push origin HEAD:master; Write-Host done", expect: "block" },
  { label: "branch merely named like main", command: "git push -u origin feat/main-nav", expect: "allow" },
  { label: "git -C . push to main by refspec", command: "git -C . push origin HEAD:main", expect: "block" },
  { label: "a push option's value is not a refspec", command: "git push -o main origin feat/x", expect: "allow" },

  // --- merging is the owner's, always -------------------------------------------------
  { label: `git ${MERGE}`, command: `git ${MERGE} feat/x`, expect: "block" },
  { label: `git ${MERGE} --abort is recovery`, command: `git ${MERGE} --abort`, expect: "allow" },
  { label: `git -c k=v ${MERGE}`, command: `git -c core.editor=true ${MERGE} feat/x`, expect: "block" },
  { label: `git -C . ${MERGE}-base only reads`, command: `git -C . ${MERGE}-base HEAD origin/main`, expect: "allow" },
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

  // --- deploying is the owner's (hooks.deniedCommands in agentic.config.json) -----------
  { label: "npm run deploy:vps", command: "npm run deploy:vps -- --host 203.0.113.10 --domain a.example.com", expect: "block" },
  { label: "deploy:vps --check still deploys tooling", command: "npm run deploy:vps -- --check", expect: "block" },
  { label: "deploy:vps via npm run-script", command: "npm run-script deploy:vps", expect: "block" },
  { label: "deploy:vps after another command", command: "npm run gate && npm run deploy:vps", expect: "block" },
  { label: "the deploy script run by node", command: "node scripts/deploy-vps.mjs --yes", expect: "block" },
  { label: "the deploy script by a Windows path", command: `node .${BS}scripts${BS}deploy-vps.mjs`, expect: "block" },
  { label: "reading the deploy script", command: "cat scripts/deploy-vps.mjs", expect: "allow" },
  { label: "the deploy helpers' unit tests", command: "node --test scripts/tests/deploy-vps.test.mjs", expect: "allow" },
  { label: "grep for the deploy script name", command: "grep -rn 'deploy:vps' package.json", expect: "allow" },

  // --- quoting and wrapping do not change where a push goes ------------------------------
  { label: "push to a quoted main", command: "git push origin 'main'", expect: "block" },
  { label: "push to a double-quoted refspec destination", command: 'git push origin HEAD:"main"', expect: "block" },
  { label: "push wrapped in bash -c", command: 'bash -c "git push origin main"', expect: "block" },
  { label: "a quoted feature branch is still a feature branch", command: "git push -u origin 'feat/x'", expect: "allow" },
  { label: `${MERGE} through gh api`, command: `gh api -X PUT repos/o/r/pulls/1/${MERGE}`, expect: "block" },
  { label: `${MERGE} through gh api graphql`, command: `gh api graphql -f query='mutation { ${MERGE}PullRequest(input: {}) { clientMutationId } }'`, expect: "block" },
  { label: "reading a PR through gh api", command: "gh api repos/o/r/pulls/1", expect: "allow" },

  // --- a commit message is prose, not a command -----------------------------------------
  { label: "commit message naming the merge verb", command: `git commit -m "docs: never run git ${MERGE} here"`, expect: "allow" },
  { label: "commit message naming the compose command", command: `git commit -am '${DESTRUCTIVE} is refused'`, expect: "allow" },
  { label: "a real command after the commit still counts", command: `git commit -m "wip" && git ${MERGE} feat/x`, expect: "block" },
  { label: "substitution in a commit message still runs", command: `git commit -m "$(git ${MERGE} feat/x)"`, expect: "block" },
  { label: "echo naming the merge verb is still scanned", command: `echo "git ${MERGE} later" | sh`, expect: "block" },
  { label: "a commit message piped into a shell is kept", command: 'git commit -m "x; git push origin main" | sh', expect: "block" },
  { label: "a commit message piped into a text filter is prose", command: `git commit -m "never git ${MERGE}" | tail -1`, expect: "allow" },
  { label: "git -C . commit message naming the merge verb", command: `git -C . commit -m "never git ${MERGE} here"`, expect: "allow" },
  {
    label: "PowerShell: a message escaping its quote hides nothing",
    tool: "PowerShell",
    command: `git commit -m "a${BS}" ; git ${MERGE} feat ; echo ${BS}""`,
    expect: "block",
  },
  { label: "Bash: an escaped quote inside a message is not blanked", command: `git commit -m "a${BS}" ; git ${MERGE} feat"`, expect: "block" },

  // --- other ways to lose uncommitted work or data ---------------------------------------
  { label: "git checkout -- .", command: "git checkout -- .", expect: "block" },
  { label: "git restore .", command: "git restore .", expect: "block" },
  { label: "git checkout one file", command: "git checkout -- src/app.ts", expect: "allow" },
  { label: "git checkout a branch", command: "git checkout -b feat/x", expect: "allow" },
  { label: "git restore --staged . only unstages", command: "git restore --staged .", expect: "allow" },
  { label: "git stash clear", command: "git stash clear", expect: "block" },
  { label: "git stash list", command: "git stash list", expect: "allow" },
  { label: "the v1 compose binary dropping volumes", command: "docker-compose down -v", expect: "block" },
  { label: "the v1 compose binary keeping volumes", command: "docker-compose down", expect: "allow" },
  { label: "compose -f file, then down -v", command: "docker-compose -f docker-compose.prod.yml down --volumes", expect: "block" },
  { label: "compose -f file, then down", command: "docker compose -f docker-compose.prod.yml down", expect: "allow" },
  { label: "reverting every EF migration", command: "dotnet ef database update 0 --project src/backend/Source/Backend.csproj", expect: "block" },
  { label: "reverting every EF migration, options first", command: "dotnet ef database update --project src/backend/Source/Backend.csproj 0", expect: "block" },
  { label: "updating to a named EF migration", command: "dotnet ef database update AddOrders", expect: "allow" },
  { label: "find -delete", command: 'find . -name "*.cs" -delete', expect: "block" },
  { label: "find -exec rm", command: "find . -name '*.tmp' -exec rm {} ;", expect: "block" },
  { label: "find -exec rm by its path", command: "find . -name '*.tmp' -exec /bin/rm {} +", expect: "block" },
  { label: "find -exec sed -i", command: "find . -name '*.ts' -exec sed -i s/a/b/ {} +", expect: "block" },
  { label: "find -exec grep only reads", command: "find . -name '*.ts' -exec grep -l TODO {} +", expect: "allow" },
  { label: "find that only lists", command: 'find . -name "*.cs" -newer x', expect: "allow" },
  { label: "git branch -D", command: "git branch -D feat/x", expect: "block" },
  { label: "git branch -Dq, a bundled -D", command: "git branch -Dq feat/x", expect: "block" },
  { label: "git branch -d refuses unmerged work", command: "git branch -d feat/x", expect: "allow" },
  { label: "sed -n with -i edits in place", command: "sed -n -i 's/a/b/' src/x.ts", expect: "block" },
  { label: "sed -n that only prints", command: "sed -n '1,20p' src/x.ts", expect: "allow" },

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
    input: JSON.stringify({ ...(c.tool ? { tool_name: c.tool } : {}), tool_input: { command: c.command } }),
    encoding: "utf8",
  });
  const actual = verdict(res);
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
  // A push with no refspec sends the current branch; so does `HEAD`. Only the refspecs a
  // command named used to be checked, so a bare push from a checkout of main went through.
  { label: "bare git push while on main", branch: "main", command: "git push", expect: "block" },
  { label: "git push origin while on main", branch: "main", command: "git push origin", expect: "block" },
  { label: "git push origin HEAD while on main", branch: "main", command: "git push -u origin HEAD", expect: "block" },
  { label: "git -C . push while on main", branch: "main", command: "git -C . push", expect: "block" },
  { label: "git push --all reaches main", branch: "main", command: "git push --all origin", expect: "block" },
  { label: "commit on a feature branch", branch: "feat/x", expect: "allow" },
  { label: "bare git push from a feature branch", branch: "feat/x", command: "git push", expect: "allow" },
  { label: "git push origin HEAD from a feature branch", branch: "feat/x", command: "git push -u origin HEAD", expect: "allow" },
];

for (const c of branchCases) {
  if (c.branch !== "main") git("checkout", "-B", c.branch);
  const res = spawnSync("node", [HOOK], {
    input: JSON.stringify({ tool_input: { command: c.command ?? 'git commit -m "x"' } }),
    encoding: "utf8",
    cwd: repo,
  });
  const actual = verdict(res);
  report(actual === c.expect, c.label, c.expect, actual);
}

// A notebook edit names its target as notebook_path, and the path guard reads that too.
{
  const res = spawnSync("node", [PATHS_GUARD], {
    input: JSON.stringify({ tool_input: { notebook_path: `/repo/${DOTENV}` } }),
    encoding: "utf8",
  });
  report(verdict(res) === "block", "notebook edit of the env file", "block", verdict(res));
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
  const actual = verdict(res);
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
  // Every dotenv file is a secret, not only `.env` and `.env.local`.
  { label: "writing .env.production", file: `/repo/${DOTENV}.production`, expect: "block" },
  { label: "writing a dotted env template", file: `/repo/${DOTENV}.production.example`, expect: "allow" },
];

for (const c of pathCases) {
  const res = spawnSync("node", [PATHS_GUARD], {
    input: JSON.stringify({ tool_input: { file_path: c.file } }),
    encoding: "utf8",
  });
  const actual = verdict(res);
  report(actual === c.expect, c.label, c.expect, actual);
}

// --- guard-secret-reads.mjs ----------------------------------------------------
// The Read tool and Grep reach files the shell guard refuses to cat. Same list of secrets.
const READS_GUARD = join(HOOKS_DIR, "guard-secret-reads.mjs");
const readCases = [
  { label: "Read the live env file", tool: "Read", input: { file_path: `D:${BS}repo${BS}${DOTENV}` }, expect: "block" },
  { label: "Read .env.production", tool: "Read", input: { file_path: `/repo/${DOTENV}.production` }, expect: "block" },
  { label: "Read the env template", tool: "Read", input: { file_path: `/repo/${DOTENV}.example` }, expect: "allow" },
  { label: "Read per-env appsettings", tool: "Read", input: { file_path: `/repo/src/backend/Source/${DEV_SETTINGS}` }, expect: "block" },
  { label: "Read base appsettings.json", tool: "Read", input: { file_path: "/repo/src/backend/Source/appsettings.json" }, expect: "allow" },
  { label: "Grep inside the env file", tool: "Grep", input: { pattern: "KEY", path: `/repo/${DOTENV}` }, expect: "block" },
  { label: "Grep across a directory", tool: "Grep", input: { pattern: "KEY", path: "/repo/src", glob: "*.json" }, expect: "allow" },
  { label: "Read an ordinary source file", tool: "Read", input: { file_path: "/repo/src/environment.ts" }, expect: "allow" },
];
for (const c of readCases) {
  const res = spawnSync("node", [READS_GUARD], {
    input: JSON.stringify({ tool_name: c.tool, tool_input: c.input }),
    encoding: "utf8",
  });
  const actual = verdict(res);
  report(actual === c.expect, c.label, c.expect, actual);
}
extraCases += readCases.length;

// --- a config that does not parse ----------------------------------------------
// Every guard but the secret-reads one imports the config. If parsing it failing ended the
// hook with any status but 2, one stray comma in agentic.config.json would switch them off.
const brokenConfig = join(mkdtempSync(join(tmpdir(), "hook-broken-config-")), "agentic.config.json");
writeFileSync(brokenConfig, '{ "project": { "name": "x", }');
const brokenCases = [
  { label: "broken config: the shell guard still refuses", hook: HOOK, input: { tool_input: { command: `cat ${DOTENV}` } } },
  { label: "broken config: the path guard still refuses", hook: PATHS_GUARD, input: { tool_input: { file_path: `/repo/${DOTENV}` } } },
];
for (const c of brokenCases) {
  const res = spawnSync("node", [c.hook], {
    input: JSON.stringify(c.input),
    encoding: "utf8",
    env: { ...process.env, AGENTIC_CONFIG: brokenConfig },
  });
  report(verdict(res) === "block", c.label, "block", verdict(res));
}
extraCases += brokenCases.length;

console.log("");
if (failures > 0) {
  console.error(`${failures} hook case(s) failed. A guard is not behaving as documented.`);
  process.exit(1);
}
console.log(
  `All ${cases.length + branchCases.length + conventionCases.length + pathCases.length + extraCases} hook cases pass.`,
);
