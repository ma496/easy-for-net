import { strict as assert } from "node:assert";
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { parsePorcelainZ, treeFingerprint, workingTreePaths } from "../lib/changed-paths.mjs";

test("modified, added and untracked entries each yield their path", () => {
  const out = " M src/a.ts\0A  src/b.ts\0?? src/c d.ts\0";
  assert.deepEqual(parsePorcelainZ(out), ["src/a.ts", "src/b.ts", "src/c d.ts"]);
});

test("a rename yields its new path, and its old one only when asked", () => {
  const out = "R  src/new.ts\0src/old.ts\0 M src/x.ts\0";
  assert.deepEqual(parsePorcelainZ(out), ["src/new.ts", "src/x.ts"]);
  assert.deepEqual(parsePorcelainZ(out, { includeRenamedFrom: true }), ["src/new.ts", "src/old.ts", "src/x.ts"]);
});

test("empty or missing output is no paths", () => {
  assert.deepEqual(parsePorcelainZ(""), []);
  assert.deepEqual(parsePorcelainZ(undefined), []);
});

test("a new directory is listed file by file, so path rules can match what is inside it", () => {
  const dir = mkdtempSync(join(tmpdir(), "changed-paths-"));
  try {
    const git = (...args) => spawnSync("git", args, { cwd: dir, encoding: "utf8" });
    git("init", "-q");
    git("config", "user.email", "t@example.com");
    git("config", "user.name", "t");
    writeFileSync(join(dir, "keep.txt"), "x");
    git("add", "keep.txt");
    git("commit", "-qm", "init");
    mkdirSync(join(dir, "app", "reports", "_components"), { recursive: true });
    writeFileSync(join(dir, "app", "reports", "page.tsx"), "x");
    writeFileSync(join(dir, "app", "reports", "_components", "table.tsx"), "x");
    assert.deepEqual(workingTreePaths(dir).sort(), [
      "app/reports/_components/table.tsx",
      "app/reports/page.tsx",
    ]);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test("the tree fingerprint is stable for the same bytes and moves with any edit", () => {
  const dir = mkdtempSync(join(tmpdir(), "fingerprint-"));
  try {
    const git = (...args) => spawnSync("git", args, { cwd: dir, encoding: "utf8" });
    git("init", "-q");
    git("config", "user.email", "t@example.com");
    git("config", "user.name", "t");
    writeFileSync(join(dir, "keep.txt"), "x");
    git("add", "keep.txt");
    git("commit", "-qm", "init");

    const clean = treeFingerprint(dir);
    assert.equal(treeFingerprint(dir), clean);

    writeFileSync(join(dir, "keep.txt"), "y");
    const edited = treeFingerprint(dir);
    assert.notEqual(edited, clean);

    writeFileSync(join(dir, "new.txt"), "1");
    const added = treeFingerprint(dir);
    assert.notEqual(added, edited);
    writeFileSync(join(dir, "new.txt"), "2");
    assert.notEqual(treeFingerprint(dir), added, "an untracked file's content counts, not only its name");
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
