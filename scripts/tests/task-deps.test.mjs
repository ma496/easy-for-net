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
