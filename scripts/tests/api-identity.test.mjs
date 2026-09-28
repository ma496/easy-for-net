import test from "node:test";
import assert from "node:assert/strict";

import { apiAdoptionVerdict, normalizeRoot } from "../lib/api-identity.mjs";

const WORKTREE = "/repo/.agent-worktrees/some-task";

test("adopts an API serving the checkout under test", () => {
  const verdict = apiAdoptionVerdict({ ok: true, repoRoot: WORKTREE }, WORKTREE);
  assert.equal(verdict.adopt, true);
  assert.match(verdict.reason, /this checkout/);
});

test("refuses an API serving a different checkout", () => {
  // The case that made this necessary: the compose container answers on 8787 from /app,
  // and a worktree's answer gate passed 8/8 against main.
  const verdict = apiAdoptionVerdict({ ok: true, repoRoot: "/app" }, WORKTREE);
  assert.equal(verdict.adopt, false);
  assert.match(verdict.reason, /\/app/);
});

test("refuses a dev API left behind by another worktree", () => {
  const verdict = apiAdoptionVerdict(
    { ok: true, repoRoot: "/repo/.agent-worktrees/other-task" },
    WORKTREE,
  );
  assert.equal(verdict.adopt, false);
});

test("refuses an older API that does not report a root", () => {
  // Absence of proof is not proof: an API predating this field cannot be tied to the diff.
  for (const health of [{ ok: true }, { ok: true, repoRoot: "" }, { ok: true, repoRoot: "   " }, { ok: true, repoRoot: 8787 }]) {
    const verdict = apiAdoptionVerdict(health, WORKTREE);
    assert.equal(verdict.adopt, false, `should refuse ${JSON.stringify(health)}`);
    assert.match(verdict.reason, /repoRoot/);
  }
});

test("refuses anything that is not a readable health body", () => {
  for (const health of [null, undefined, "ok", 42]) {
    assert.equal(apiAdoptionVerdict(health, WORKTREE).adopt, false);
  }
});

test("refuses when the directory under test is unknown", () => {
  assert.equal(apiAdoptionVerdict({ repoRoot: WORKTREE }, "").adopt, false);
  assert.equal(apiAdoptionVerdict({ repoRoot: WORKTREE }, undefined).adopt, false);
});

test("a trailing slash is not a different checkout", () => {
  assert.equal(apiAdoptionVerdict({ repoRoot: `${WORKTREE}/` }, WORKTREE).adopt, true);
  assert.equal(apiAdoptionVerdict({ repoRoot: WORKTREE }, `${WORKTREE}//`).adopt, true);
});

test("a prefix of the checkout path is not the checkout", () => {
  // /repo and /repo/.agent-worktrees/some-task share a prefix and share nothing else.
  assert.equal(apiAdoptionVerdict({ repoRoot: "/repo" }, WORKTREE).adopt, false);
  assert.equal(apiAdoptionVerdict({ repoRoot: `${WORKTREE}-2` }, WORKTREE).adopt, false);
});

test("normalizeRoot keeps root itself intact", () => {
  assert.equal(normalizeRoot("/"), "/");
  assert.equal(normalizeRoot("  /repo/  "), "/repo");
  assert.equal(normalizeRoot(null), "");
});
