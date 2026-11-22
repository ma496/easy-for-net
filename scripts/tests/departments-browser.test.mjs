import { strict as assert } from "node:assert";
import { mkdtempSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { test } from "node:test";

/**
 * The browser check a `"browser": "required"` reviewer owes, against a fixture config so the
 * rule is tested on its own rather than on whatever the host project declares. The config is
 * read on first import, so the variable is set before the dynamic import below.
 */
const fixture = join(mkdtempSync(join(tmpdir(), "departments-browser-")), "agentic.config.json");
writeFileSync(
  fixture,
  JSON.stringify({
    departments: [
      { agent: "frontend-engineer", label: "Front end", phase: "build", match: ["^web/"], why: "Builds pages." },
      { agent: "qa-engineer", label: "QA", phase: "review", order: 1, always: true, why: "Acceptance." },
      {
        agent: "ui-ux-reviewer",
        label: "UI / UX",
        phase: "review",
        order: 1,
        browser: "required",
        match: ["^web/app/.*\\.tsx$"],
        why: "Judged by eye.",
      },
    ],
  }),
);
process.env.AGENTIC_CONFIG = fixture;
const { browserOverlaps, browserProblems, browserWaiver, owesBrowserCheck } = await import("../lib/departments.mjs");

const PAGE = ["web/app/users/page.tsx"];
const looked = { agent: "ui-ux-reviewer", browserTools: ["browser_navigate", "browser_take_screenshot", "browser_close"], finalText: "DESIGN: PASS" };

test("a UI review that opened, screenshotted and closed the page passes", () => {
  const { problems, notes } = browserProblems(PAGE, [{ agent: "frontend-engineer", browserTools: [], finalText: "" }, looked]);
  assert.deepEqual(problems, []);
  assert.match(notes[0], /rendered the page \(1 screenshot/);
});

test("a UI review that never opened a browser is refused", () => {
  const { problems } = browserProblems(PAGE, [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "DESIGN: PASS" }]);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /never rendered the page/);
  assert.match(problems[0], /pnpm stack up/);
});

test("opening the page without a screenshot is refused", () => {
  const { problems } = browserProblems(PAGE, [{ agent: "ui-ux-reviewer", browserTools: ["browser_navigate", "browser_close"], finalText: "" }]);
  assert.match(problems[0], /made no screenshot/);
});

test("a review that left its browser open is refused", () => {
  const { problems } = browserProblems(PAGE, [
    { agent: "ui-ux-reviewer", browserTools: ["browser_navigate", "browser_take_screenshot"], finalText: "DESIGN: PASS" },
  ]);
  assert.match(problems[0], /left its browser open/);
});

test("a close before the last screenshot still leaves the browser open", () => {
  const { problems } = browserProblems(PAGE, [
    { agent: "ui-ux-reviewer", browserTools: ["browser_navigate", "browser_close", "browser_take_screenshot"], finalText: "" },
  ]);
  assert.match(problems[0], /left its browser open/);
});

test("only the last review counts: an earlier look does not cover a later blind pass", () => {
  const { problems } = browserProblems(PAGE, [looked, { agent: "ui-ux-reviewer", browserTools: [], finalText: "DESIGN: PASS" }]);
  assert.match(problems[0], /never rendered the page/);
});

test("a later look covers an earlier blind pass", () => {
  const { problems } = browserProblems(PAGE, [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "" }, looked]);
  assert.deepEqual(problems, []);
});

test("a waiver with a reason is accepted and recorded", () => {
  const { problems, notes } = browserProblems(PAGE, [
    { agent: "ui-ux-reviewer", browserTools: [], finalText: "Types only.\n\nBROWSER: NOT NEEDED — a prop type was renamed; nothing renders differently\nDESIGN: PASS" },
  ]);
  assert.deepEqual(problems, []);
  assert.match(notes[0], /browser not needed — a prop type was renamed/);
});

test("a waiver without a reason is refused", () => {
  const { problems } = browserProblems(PAGE, [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "BROWSER: NOT NEEDED\nDESIGN: PASS" }]);
  assert.match(problems[0], /without a reason/);
});

test("a stack that failed to start excuses a review read from code", () => {
  const { problems, notes } = browserProblems(PAGE, [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "DESIGN: PASS" }], {
    stackFailure: "Redis is not answering",
  });
  assert.deepEqual(problems, []);
  assert.match(notes[0], /reviewed from code — the app did not start \(Redis is not answering\)/);
});

test("a change with no screen owes no browser check", () => {
  assert.equal(owesBrowserCheck(["web/lib/utils.ts"]), false);
  assert.deepEqual(browserProblems(["web/lib/utils.ts"], [{ agent: "qa-engineer", browserTools: [], finalText: "" }]), { problems: [], notes: [] });
  assert.equal(owesBrowserCheck(PAGE), true);
});

test("a reviewer that was never delegated is left to the attendance check", () => {
  assert.deepEqual(browserProblems(PAGE, []).problems, []);
});

test("QA's browser use is optional", () => {
  const { problems } = browserProblems(["web/lib/utils.ts", ...PAGE], [{ agent: "qa-engineer", browserTools: [], finalText: "QA: PASS" }, looked]);
  assert.deepEqual(problems, []);
});

test("browserWaiver reads the line however it is dashed or formatted", () => {
  assert.equal(browserWaiver("BROWSER: NOT NEEDED - only a type changed").reason, "only a type changed");
  assert.equal(browserWaiver("**BROWSER: NOT NEEDED** — only a type changed").reason, "only a type changed");
  assert.equal(browserWaiver("browser: not needed: nothing renders").reason, "nothing renders");
  assert.equal(browserWaiver("BROWSER: NOT NEEDED").reason, "");
  assert.equal(browserWaiver("I opened the browser; it was not needed to log in."), null);
  assert.equal(browserWaiver(""), null);
});

test("a waiver the lead suggested is refused", () => {
  const { problems } = browserProblems(
    PAGE,
    [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "BROWSER: NOT NEEDED — nothing renders differently" }],
    { leadSuggestedWaiver: true },
  );
  assert.match(problems[0], /suggested to it/);
});

test("a lead that never named the waiver leaves the reviewer's own waiver standing", () => {
  const { problems } = browserProblems(
    PAGE,
    [{ agent: "ui-ux-reviewer", browserTools: [], finalText: "BROWSER: NOT NEEDED — nothing renders differently" }],
    { leadSuggestedWaiver: false },
  );
  assert.deepEqual(problems, []);
});

test("two agents driving the shared browser at once are refused", () => {
  const { problems } = browserProblems(PAGE, [
    { ...looked, browserSpan: [1, 5] },
    { agent: "qa-engineer", browserTools: ["browser_navigate", "browser_close"], browserSpan: [3, 7], finalText: "QA: PASS" },
  ]);
  assert.equal(problems.length, 1);
  assert.match(problems[0], /ui-ux-reviewer and qa-engineer drove the browser at the same time/);
});

test("agents taking turns with the browser are fine", () => {
  assert.deepEqual(
    browserOverlaps([
      { agent: "ui-ux-reviewer", browserSpan: [1, 4] },
      { agent: "qa-engineer", browserSpan: [5, 8] },
      { agent: "code-reviewer", browserSpan: null },
    ]),
    [],
  );
});
