import { strict as assert } from "node:assert";
import { test } from "node:test";
import { selectGateSteps } from "../lib/gate-scope.mjs";

const steps = [
  { name: "api build", watches: [/^src\/backend\//] },
  { name: "web lint", watches: [/^src\/frontend\/web\//] },
  { name: "web tests", watches: [/^src\/frontend\/web\//, /^src\/backend\/Source\/Resources\//] },
  { name: "engine tests", watches: [/^scripts\//] },
];
const inert = [/\.md$/, /^\.agent-queue\//];
const names = (r) => r.steps.map((s) => s.name);

test("a backend-only change runs only the steps that watch the backend", () => {
  const r = selectGateSteps(steps, ["src/backend/Source/Thing.cs"], { inert });
  assert.equal(r.full, false);
  assert.deepEqual(names(r), ["api build"]);
});

test("a file two areas read runs both of their steps", () => {
  const r = selectGateSteps(steps, ["src/backend/Source/Resources/en.json"], { inert });
  assert.deepEqual(names(r), ["api build", "web tests"]);
});

test("a documentation-only change runs no step at all", () => {
  const r = selectGateSteps(steps, ["docs/guide.md", ".agent-queue/done/01-x.md"], { inert });
  assert.deepEqual(names(r), []);
  assert.equal(r.full, false);
});

test("a path no step watches and nothing declares inert runs every step", () => {
  const r = selectGateSteps(steps, ["src/backend/Source/Thing.cs", "global.json"], { inert });
  assert.equal(r.full, true);
  assert.deepEqual(r.unknown, ["global.json"]);
  assert.deepEqual(names(r), names({ steps }));
});

test("no changed paths runs every step rather than none", () => {
  assert.equal(selectGateSteps(steps, [], { inert }).full, true);
  assert.equal(selectGateSteps(steps, undefined, { inert }).full, true);
});

test("a step without watches runs whenever anything runs, and Windows separators still match", () => {
  const withAlways = [...steps, { name: "always" }];
  const r = selectGateSteps(withAlways, ["scripts\\lib\\x.mjs"], { inert });
  assert.deepEqual(names(r), ["engine tests", "always"]);
});
