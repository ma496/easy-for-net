import { strict as assert } from "node:assert";
import { test } from "node:test";
import { appArg, config, modelArgs, resolveModel, selectApp, validateConfig } from "../lib/project-config.mjs";

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

test("the settle period is a positive whole number of seconds", () => {
  assert.equal(config.budget.settleSeconds, 60);
  for (const bad of [0, -5, 1.5, "60", null]) {
    const cfg = valid();
    cfg.budget.settleSeconds = bad;
    assert.match(validateConfig(cfg).join("\n"), /budget\.settleSeconds must be a positive integer/, String(bad));
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

// --- apps ----------------------------------------------------------------------------
const TWO_APPS = [
  { name: "multi-tenant", backend: "src/multi-tenant/backend", web: "src/multi-tenant/frontend/web" },
  { name: "user-facing", backend: "src/user-facing/backend", web: "src/user-facing/frontend/web" },
];

test("selectApp picks the named app, else the first", () => {
  assert.equal(selectApp("user-facing", TWO_APPS).name, "user-facing");
  assert.equal(selectApp(undefined, TWO_APPS).name, "multi-tenant");
  assert.equal(selectApp("", TWO_APPS).name, "multi-tenant");
});

test("selectApp refuses an unknown app rather than acting on another", () => {
  assert.throws(() => selectApp("single", TWO_APPS), /Unknown app "single"/);
});

test("appArg reads --app's value", () => {
  assert.equal(appArg(["--no-docker", "--app", "user-facing"]), "user-facing");
  assert.equal(appArg(["--no-docker"]), undefined);
});

test("the repository's own config lists valid apps", () => {
  assert.ok(config.apps.length >= 1);
  assert.deepEqual(validateConfig(config).filter((p) => p.startsWith("apps")), []);
});

test("validateConfig refuses empty, incomplete and duplicate apps", () => {
  const problems = (apps) => validateConfig({ ...config, apps }).filter((p) => p.startsWith("apps"));
  assert.equal(problems([]).length, 1);
  assert.equal(problems([{ name: "x", backend: "src/backend" }]).length, 1);
  assert.equal(problems([TWO_APPS[0], { ...TWO_APPS[1], name: "multi-tenant" }]).length, 1);
  assert.deepEqual(problems(TWO_APPS), []);
});
