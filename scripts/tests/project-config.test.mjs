import { strict as assert } from "node:assert";
import { test } from "node:test";
import { config, modelArgs, resolveModel, validateConfig } from "../lib/project-config.mjs";

const valid = () => structuredClone(config);

test("the committed agentic.config.json is valid", () => {
  assert.deepEqual(validateConfig(config), []);
});

test("a non-positive or non-integer budget is refused, naming the key", () => {
  for (const bad of [0, -1, 2.5, "three", null]) {
    const cfg = valid();
    cfg.budget.attempts = bad;
    assert.match(validateConfig(cfg).join("\n"), /budget\.attempts must be a positive integer/, String(bad));
  }
});

test("an invalid department regex is refused rather than silently dropped", () => {
  const cfg = valid();
  cfg.departments[0].match = ["(unclosed"];
  assert.match(validateConfig(cfg).join("\n"), /departments\[0\].*invalid regular expression/);
});

test("an agent may appear once per phase, not twice in one", () => {
  const cfg = valid();
  cfg.departments.push({ ...cfg.departments.at(-1) });
  assert.match(validateConfig(cfg).join("\n"), /listed twice/);
});

test("a department with neither match nor always is refused", () => {
  const cfg = valid();
  cfg.departments.push({ agent: "orphan", phase: "review" });
  assert.match(validateConfig(cfg).join("\n"), /orphan.*needs `match` paths or `always: true`/);
});

test("the model resolves env, then flag, then config, and blank means unset", () => {
  assert.equal(resolveModel({ env: "sonnet", arg: "haiku", configured: "opus" }), "sonnet");
  assert.equal(resolveModel({ env: "  ", arg: "haiku", configured: "opus" }), "haiku");
  assert.equal(resolveModel({ configured: "opus" }), "opus");
  assert.equal(resolveModel({ configured: "" }), "opus");
});

test("inherit passes no --model at all", () => {
  assert.deepEqual(modelArgs("inherit"), []);
  assert.deepEqual(modelArgs("opus"), ["--model", "opus"]);
});
