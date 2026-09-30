import { strict as assert } from "node:assert";
import { mkdtempSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { test } from "node:test";

/**
 * A universal reviewer excused from documentation-only diffs, and delegations carried from
 * one attempt of a run to the next. A fixture of its own, set before the import, for the
 * same reason departments.test.mjs gives.
 */
const fixture = join(mkdtempSync(join(tmpdir(), "departments-attempts-")), "agentic.config.json");
writeFileSync(
  fixture,
  JSON.stringify({
    departments: [
      { agent: "ui-ux-reviewer", label: "Design", phase: "design", match: ["^public/"], why: "Designs first." },
      { agent: "data-engineer", label: "Data", phase: "build", match: ["^src/db/"], why: "The schema." },
      { agent: "backend-engineer", label: "Backend", phase: "build", match: ["^src/"], why: "The API." },
      { agent: "qa-engineer", label: "QA", phase: "review", order: 1, always: true, why: "The brief." },
      { agent: "ui-ux-reviewer", label: "UI", phase: "review", order: 1, match: ["^public/"], why: "By eye." },
      {
        agent: "code-reviewer",
        label: "Code review",
        phase: "review",
        order: 1,
        always: true,
        exceptWhenOnly: ["\\.md$"],
        why: "What a green gate cannot see.",
      },
    ],
    unownedPaths: ["^\\.agent-queue/", "^docs/builds/"],
  }),
);
process.env.AGENTIC_CONFIG = fixture;

const { carriedDelegations, explainMissing, requiredAgents, sequenceProblems } = await import("../lib/departments.mjs");
const { validateConfig, loadConfig } = await import("../lib/project-config.mjs").then((m) => ({
  validateConfig: m.validateConfig,
  loadConfig: () => m.config,
}));

test("a universal reviewer excused from markdown is not owed by a markdown-only diff", () => {
  const owed = requiredAgents(["docs/guide.md", ".claude/skills/x/SKILL.md", "docs/builds/01.md"]);
  assert.deepEqual(owed, ["qa-engineer"]);
});

test("the same reviewer is owed again as soon as one path is not markdown", () => {
  assert.ok(requiredAgents(["docs/guide.md", "src/api.ts"]).includes("code-reviewer"));
});

test("a diff owning nothing excuses no one", () => {
  assert.ok(requiredAgents(["docs/builds/01.md"]).includes("code-reviewer"));
});

test("only writers' delegations carry into the next attempt", () => {
  assert.deepEqual(
    carriedDelegations(["data-engineer", "backend-engineer", "qa-engineer", "code-reviewer", "ui-ux-reviewer"]),
    ["data-engineer", "backend-engineer", "ui-ux-reviewer"],
  );
  assert.deepEqual(carriedDelegations(undefined), []);
});

test("a writer from an earlier attempt satisfies a retry that did not call it again", () => {
  const paths = ["src/db/schema.ts", "src/routes/a.ts"];
  const current = ["backend-engineer", "qa-engineer", "code-reviewer"];
  const delegated = [...carriedDelegations(["data-engineer", "backend-engineer"]), ...current];
  assert.deepEqual(explainMissing(paths, delegated, current), []);
  assert.deepEqual(sequenceProblems(paths, delegated), []);
});

test("a reviewer from an earlier attempt does not satisfy a retry: only a review of this diff counts", () => {
  const paths = ["src/routes/a.ts"];
  const current = ["qa-engineer"];
  const delegated = [...carriedDelegations(["backend-engineer", "code-reviewer"]), ...current];
  assert.deepEqual(
    explainMissing(paths, [...delegated, "code-reviewer"], current).map((d) => d.agent),
    ["code-reviewer"],
  );
});

test("a design pass carried from an earlier attempt still comes before the build", () => {
  const paths = ["public/page.tsx"];
  const current = ["qa-engineer", "ui-ux-reviewer", "code-reviewer"];
  const delegated = [...carriedDelegations(["ui-ux-reviewer", "backend-engineer"]), ...current];
  assert.deepEqual(sequenceProblems(paths, delegated), []);
  assert.deepEqual(explainMissing(paths, delegated, current), []);
});

test("the fixture is a valid config, and exceptWhenOnly on a non-universal department is refused", () => {
  assert.deepEqual(validateConfig(loadConfig()), []);
  const bad = structuredClone(loadConfig());
  bad.departments[1].exceptWhenOnly = ["\\.md$"];
  assert.ok(validateConfig(bad).some((p) => p.includes("exceptWhenOnly")));
});
