import { strict as assert } from "node:assert";
import { test } from "node:test";
import { runnerVerifyArgs, shouldForwardVerify, TASK_VERIFY_SCOPE, taskVerifyCommand } from "../lib/verify-scope.mjs";

test("a task is verified on its own change, not the whole branch", () => {
  assert.equal(TASK_VERIFY_SCOPE, "working");
});

test("an npm script gets the separator npm needs to forward the flag", () => {
  assert.equal(taskVerifyCommand("npm run verify"), "npm run verify -- --scope working");
});

test("a command that already forwards, or is not npm, gets no second separator", () => {
  assert.equal(taskVerifyCommand("npm run verify -- --autostart"), "npm run verify -- --autostart --scope working");
  assert.equal(taskVerifyCommand("node scripts/verify.mjs"), "node scripts/verify.mjs --scope working");
});

test("no configured command is refused rather than producing a bare flag", () => {
  assert.throws(() => taskVerifyCommand(""), /no verify command/);
});

test("the runner's verify names the scope, the base and the port", () => {
  const args = runnerVerifyArgs({ script: "verify.mjs", port: 5010, base: "hrm" });
  assert.deepEqual(args, ["verify.mjs", "--port", "5010", "--base", "hrm", "--scope", "working", "--autostart"]);
});

test("the runner's verify can be told not to autostart and to pass more flags", () => {
  const args = runnerVerifyArgs({ script: "verify.mjs", port: 5010, base: "main", autostart: false, extra: ["--full"] });
  assert.deepEqual(args, ["verify.mjs", "--port", "5010", "--base", "main", "--scope", "working", "--full"]);
});

test("a session's plain verify is answered by the runner", () => {
  const env = { AGENT_VERIFY_URL: "http://127.0.0.1:1/verify/x" };
  assert.equal(shouldForwardVerify([], env), true);
  assert.equal(shouldForwardVerify(["--autostart", "--scope", "working"], env), true);
});

test("outside a run, or asking a different question, verify runs locally", () => {
  const env = { AGENT_VERIFY_URL: "http://127.0.0.1:1/verify/x" };
  assert.equal(shouldForwardVerify([], {}), false);
  assert.equal(shouldForwardVerify(["--scope", "branch"], env), false);
  assert.equal(shouldForwardVerify(["--full"], env), false);
  assert.equal(shouldForwardVerify(["--json"], env), false);
  assert.equal(shouldForwardVerify(["--port", "5100"], env), false);
});
