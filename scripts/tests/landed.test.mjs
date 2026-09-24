import { test } from "node:test";
import assert from "node:assert/strict";
import { carriesProductCode, carriesProductCodeFromShow, productFiles } from "../lib/landed.mjs";

test("a commit touching only the queue's own lanes built nothing", () => {
  assert.equal(
    carriesProductCode([
      ".agent-queue/todo/06-lead-filters.md",
      "docs/builds/06-lead-filters.md",
    ]),
    false,
  );
});

test("one application file is enough to count as work", () => {
  assert.equal(
    carriesProductCode([
      ".agent-queue/todo/06-lead-filters.md",
      "docs/builds/06-lead-filters.md",
      "apps/api/public/leads.html",
    ]),
    true,
  );
});

test("docs outside docs/builds are the product's own documentation", () => {
  assert.equal(carriesProductCode(["docs/capabilities/lead-intelligence.md"]), true);
});

test("an empty commit is not work", () => {
  assert.equal(carriesProductCode([]), false);
  assert.equal(carriesProductCode(undefined), false);
});

test("productFiles names what actually changed", () => {
  assert.deepEqual(
    productFiles(["  apps/api/src/routes/leads.ts  ", "", ".agent-runs/x.json"]),
    ["apps/api/src/routes/leads.ts"],
  );
});

test("git show output parses the same way", () => {
  assert.equal(carriesProductCodeFromShow("\n.agent-queue/done/x.md\ndocs/builds/x.md\n"), false);
  assert.equal(carriesProductCodeFromShow("\n.agent-queue/done/x.md\napps/api/public/leads.html\n"), true);
});
