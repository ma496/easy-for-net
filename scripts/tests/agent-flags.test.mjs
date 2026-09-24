import test from "node:test";
import assert from "node:assert/strict";
import { parseAgentFlag, wantsAutoPush, wantsRefuseDirtyStart } from "../lib/agent-flags.mjs";

test("unset falls back to each flag's default", () => {
  assert.equal(parseAgentFlag(undefined, true), true);
  assert.equal(parseAgentFlag("", true), true);
  assert.equal(wantsRefuseDirtyStart({}), true);
});

test("pushing is off unless the operator asks for it", () => {
  assert.equal(wantsAutoPush({}), false);
  assert.equal(wantsAutoPush({ AGENT_AUTO_PUSH: "" }), false);
  assert.equal(wantsAutoPush({ AGENT_AUTO_PUSH: "1" }), true);
});

test("explicit off spellings disable", () => {
  for (const v of ["0", "false", "off", "no", "NONE", "False"]) {
    assert.equal(parseAgentFlag(v, true), false, v);
  }
  assert.equal(wantsAutoPush({ AGENT_AUTO_PUSH: "0" }), false);
  assert.equal(wantsRefuseDirtyStart({ AGENT_REFUSE_DIRTY_START: "off" }), false);
});

test("explicit on spellings enable even when default is off", () => {
  for (const v of ["1", "true", "yes", "on", "YES"]) {
    assert.equal(parseAgentFlag(v, false), true, v);
  }
});
