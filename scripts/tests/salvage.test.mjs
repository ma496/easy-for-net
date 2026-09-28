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
