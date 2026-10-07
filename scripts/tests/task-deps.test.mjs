import { strict as assert } from "node:assert";
import { test } from "node:test";
import { dependencyState, names, namesOf } from "../lib/task-deps.mjs";

test("a task answers to its stem and to its stem without the intake stamp", () => {
  assert.deepEqual(namesOf("01-foo.md"), ["01-foo"]);
  assert.deepEqual(namesOf("2026-09-29T01-02-03-add-reports.md"), ["2026-09-29T01-02-03-add-reports", "add-reports"]);
});

test("a dependency matches exactly, never as a substring", () => {
  assert.equal(names("01-foo", "01-foo.md"), true);
  assert.equal(names("01-foo.md", "01-foo.md"), true);
  assert.equal(names("01-foo", "01-foobar.md"), false);
  assert.equal(names("api", "03-api-keys.md"), false);
  assert.equal(names("add-reports", "2026-09-29T01-02-03-add-reports.md"), true);
});

test("each lane gives the dependency a state, and no lane is unknown", () => {
  const lanes = { todo: ["03-c.md"], doing: ["02-b.md"], done: ["01-a.md"], failed: ["04-d.md"] };
  assert.equal(dependencyState("01-a", lanes), "landed");
  assert.equal(dependencyState("02-b", lanes), "pending");
  assert.equal(dependencyState("03-c", lanes), "pending");
  assert.equal(dependencyState("04-d", lanes), "failed");
  assert.equal(dependencyState("01-typo", lanes), "unknown");
});

test("a dependency named by a landed commit counts with no done/ file", () => {
  // done/ is local to one machine: a teammate's checkout, or a fresh clone, knows the task
  // landed only because a commit reachable from HEAD names it.
  const lanes = { todo: ["billing-02-screen.md"], doing: [], done: [], failed: [] };
  const landed = new Set(["billing-01-endpoint"]);
  assert.equal(dependencyState("billing-01-endpoint", lanes, landed), "landed");
  assert.equal(dependencyState("billing-01-endpoint.md", lanes, landed), "landed");
  assert.equal(dependencyState("billing-01", lanes, landed), "unknown");
});

test("a landed commit outranks a stale failed/ copy of the same brief", () => {
  const lanes = { todo: [], doing: [], done: [], failed: ["billing-01-endpoint.md"] };
  assert.equal(dependencyState("billing-01-endpoint", lanes, new Set(["billing-01-endpoint"])), "landed");
});

test("tasks of different plans with the same short name are told apart by scope", () => {
  const lanes = { todo: ["plan-b/02-screen.md"], doing: [], done: ["plan-a/01-endpoint.md"], failed: [] };
  assert.equal(dependencyState("plan-a/01-endpoint", lanes), "landed");
  assert.equal(dependencyState("plan-b/01-endpoint", lanes), "unknown");
  assert.equal(dependencyState("plan-b/01-endpoint", lanes, new Set(["plan-a/01-endpoint"])), "unknown");
});

test("an adhoc task answers to its name without the intake stamp, inside its scope", () => {
  assert.deepEqual(namesOf("adhoc/2026-09-29T01-02-03-add-reports.md"), [
    "adhoc/2026-09-29T01-02-03-add-reports",
    "adhoc/add-reports",
  ]);
});
