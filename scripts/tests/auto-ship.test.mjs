import { strict as assert } from "node:assert";
import { spawnSync } from "node:child_process";
import { mkdtempSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

/**
 * auto-ship pushes from inside its own process, where the shell guard never looks, so its
 * refusals are the only thing between `--push` and a protected branch. Each case runs it in
 * a throwaway repository with one change to commit.
 */
const SCRIPT = join(dirname(fileURLToPath(import.meta.url)), "..", "auto-ship.mjs");

function repo(config) {
  const dir = mkdtempSync(join(tmpdir(), "auto-ship-"));
  const git = (...args) => spawnSync("git", args, { cwd: dir, encoding: "utf8" });
  git("init", "-q");
  git("symbolic-ref", "HEAD", "refs/heads/main");
  git("config", "user.email", "test@example.com");
  git("config", "user.name", "Test");
  git("commit", "-q", "--allow-empty", "-m", "init");
  writeFileSync(join(dir, "a.txt"), "x\n");
  const cfg = join(dir, "agentic.config.json");
  writeFileSync(cfg, JSON.stringify(config ?? {}));
  return { dir, git, env: { ...process.env, AGENTIC_CONFIG: cfg } };
}

const ship = ({ dir, env }, ...args) => spawnSync("node", [SCRIPT, "add a", ...args], { cwd: dir, env, encoding: "utf8" });

test("--branch naming a protected branch is refused before anything is committed", () => {
  const r = repo();
  r.git("checkout", "-q", "-b", "feat/x");
  const res = ship(r, "--push", "--branch", "main");
  assert.equal(res.status, 1);
  assert.match(res.stderr, /protected branch/);
  assert.equal(r.git("rev-list", "--count", "HEAD").stdout.trim(), "1", "nothing was committed");
});

test("in a team, nothing commits on the base branch", () => {
  const r = repo({ project: { workflow: "team", baseBranch: "main" } });
  const res = ship(r);
  assert.equal(res.status, 1);
  assert.equal(r.git("rev-list", "--count", "HEAD").stdout.trim(), "1");
});

test("solo on the base branch commits, and pushes nothing without --push", () => {
  const r = repo();
  const res = ship(r);
  assert.equal(res.status, 0, res.stderr);
  assert.equal(r.git("rev-list", "--count", "HEAD").stdout.trim(), "2");
  assert.match(res.stdout, /Nothing was pushed/);
});

test("a full ref name is not a branch name, so it cannot reach a protected branch", () => {
  for (const name of ["refs/heads/main", "HEAD:main", "+main", "feat/x~1", "@{-1}"]) {
    const r = repo();
    const res = ship(r, "--push", "--branch", name);
    assert.equal(res.status, 1, name);
    assert.match(res.stderr, /plain branch name/, name);
    assert.equal(r.git("rev-list", "--count", "HEAD").stdout.trim(), "1", `${name}: nothing was committed`);
  }
});
