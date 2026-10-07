import test from "node:test";
import assert from "node:assert/strict";
import { mkdtempSync, mkdirSync, writeFileSync, existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import {
  dirtyOutsideQueueLines,
  hasSalvage,
  parkDirForSlug,
  salvageBrief,
} from "../lib/salvage.mjs";

test("dirtyOutsideQueueLines ignores .agent-queue paths", () => {
  const porcelain = [
    " D .agent-queue/todo/28-foo.md",
    " M apps/api/src/config.ts",
    "?? apps/api/src/lib/leads/quota.ts",
  ].join("\n");
  const dirty = dirtyOutsideQueueLines(porcelain);
  assert.equal(dirty.length, 2);
  assert.ok(dirty.some((l) => l.includes("config.ts")));
});

test("hasSalvage requires a real patch or moved files", () => {
  const root = mkdtempSync(join(tmpdir(), "salvage-"));
  const dir = parkDirForSlug(root, "28-daily-call");
  mkdirSync(dir, { recursive: true });
  assert.equal(hasSalvage(dir), false);

  writeFileSync(join(dir, "tracked.patch"), "");
  assert.equal(hasSalvage(dir), false);

  writeFileSync(join(dir, "tracked.patch"), "diff --git a/x b/x\n");
  assert.equal(hasSalvage(dir), true);
});

test("salvageBrief tells Claude not to rebuild when verify passes", () => {
  const text = salvageBrief({ verifies: true, verifyOutput: "" });
  assert.match(text, /Do not rebuild/i);
  assert.match(text, /department reviews/i);
});

test("salvageBrief includes verify output when fix mode", () => {
  const text = salvageBrief({ verifies: false, verifyOutput: "FAIL: quota.test.ts" });
  assert.match(text, /Do not delete it and start over/i);
  assert.match(text, /FAIL: quota\.test\.ts/);
});

test("an untracked file parked with its directories comes back to the same path", async () => {
  const { restoreSalvage, UNTRACKED_DIR } = await import("../lib/salvage.mjs");
  const root = mkdtempSync(join(tmpdir(), "salvage-"));
  const dir = parkDirForSlug(root, "billing__01-api");
  mkdirSync(join(dir, UNTRACKED_DIR, "src", "__tests__"), { recursive: true });
  writeFileSync(join(dir, UNTRACKED_DIR, "src", "__tests__", "a.test.ts"), "x");
  const res = restoreSalvage(root, "billing__01-api");
  assert.equal(res.untracked, 1);
  assert.equal(readFileSync(join(root, "src", "__tests__", "a.test.ts"), "utf8"), "x");
  assert.equal(existsSync(dir), false, "a clean restore marks the park applied");
});

test("a patch that does not apply leaves the park where the next run finds it", async () => {
  const { restoreSalvage } = await import("../lib/salvage.mjs");
  const root = mkdtempSync(join(tmpdir(), "salvage-"));
  const { spawnSync } = await import("node:child_process");
  spawnSync("git", ["init", "-q"], { cwd: root });
  const dir = parkDirForSlug(root, "x");
  mkdirSync(dir, { recursive: true });
  writeFileSync(join(dir, "tracked.patch"), "diff --git a/missing.txt b/missing.txt\n--- a/missing.txt\n+++ b/missing.txt\n@@ -1 +1 @@\n-old\n+new\n");
  const res = restoreSalvage(root, "x");
  assert.ok(res.error);
  assert.equal(res.applied, null);
  assert.equal(hasSalvage(dir), true);
});
