import { strict as assert } from "node:assert";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { gateBuilt, readGateReport, stepTimings } from "../lib/gate-report.mjs";

const report = {
  steps: [
    { name: "dotnet build", ok: true, seconds: "12.5" },
    { name: "backend tests", ok: false, seconds: "61.0" },
  ],
  skipped: ["vitest"],
};

test("a build step that ran and passed means the service is already built", () => {
  assert.equal(gateBuilt(report, "dotnet build"), true);
});

test("a failed, skipped or unnamed step never counts as built", () => {
  assert.equal(gateBuilt(report, "backend tests"), false);
  assert.equal(gateBuilt(report, "vitest"), false);
  assert.equal(gateBuilt(report, undefined), false);
  assert.equal(gateBuilt(null, "dotnet build"), false);
});

test("timings are numbers, one per step that ran", () => {
  assert.deepEqual(stepTimings(report), [
    { name: "dotnet build", ok: true, seconds: 12.5 },
    { name: "backend tests", ok: false, seconds: 61 },
  ]);
  assert.deepEqual(stepTimings(null), []);
});

test("a missing, unreadable or shapeless report reads as none", () => {
  const dir = mkdtempSync(join(tmpdir(), "gate-report-"));
  try {
    assert.equal(readGateReport(join(dir, "absent.json")), null);
    writeFileSync(join(dir, "bad.json"), "{not json");
    assert.equal(readGateReport(join(dir, "bad.json")), null);
    writeFileSync(join(dir, "shape.json"), JSON.stringify({ steps: "nope" }));
    assert.equal(readGateReport(join(dir, "shape.json")), null);
    writeFileSync(join(dir, "good.json"), JSON.stringify(report));
    assert.deepEqual(readGateReport(join(dir, "good.json")), report);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});
