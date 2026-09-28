import { strict as assert } from "node:assert";
import { mkdtempSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { test } from "node:test";

/**
 * The router reads its map from `agentic.config.json`, so these tests point it at a fixture
 * rather than at whatever the host project happens to declare. A test that asserted the
 * host's own paths would pass here and fail in the next repository the framework is
 * installed into — which is exactly the coupling this package exists to remove.
 *
 * The config is read when the module is first imported, so the environment variable is set
 * before the dynamic import below. Node runs each test file in its own process, so this
 * does not leak into the others.
 */
const fixture = join(mkdtempSync(join(tmpdir(), "departments-")), "agentic.config.json");
writeFileSync(
  fixture,
  JSON.stringify({
    departments: [
      {
        agent: "ui-ux-reviewer",
        label: "Design",
        phase: "design",
        match: ["^public/"],
        why: "Decides what the screen is before it is written.",
      },
      {
        agent: "data-engineer",
        label: "Data",
        phase: "build",
        match: ["^src/db/"],
        why: "The schema and the queries it invalidates.",
      },
      {
        agent: "backend-engineer",
        label: "Backend",
        phase: "build",
        match: ["^src/routes/"],
        why: "Everything behind the HTTP surface.",
      },
      {
        agent: "frontend-engineer",
        label: "Front end",
        phase: "build",
        match: ["^public/"],
        why: "Everything a person looks at.",
      },
      {
        agent: "qa-engineer",
        label: "QA",
        phase: "review",
        order: 1,
        always: true,
        why: "Was the brief satisfied?",
      },
      {
        agent: "ui-ux-reviewer",
        label: "UI",
        phase: "review",
        order: 1,
        match: ["^public/"],
        why: "Judged by eye.",
      },
      {
        agent: "code-reviewer",
        label: "Code review",
        phase: "review",
        order: 2,
        always: true,
        why: "What a green gate cannot see.",
      },
    ],
    skills: [
      { skill: "api-endpoint", match: ["^src/routes/"], why: "Route wiring has an order." },
      { skill: "debug-answer", brief: "\\bwrong answer\\b", why: "The early exits come first." },
    ],
    unownedPaths: ["^\\.agent-queue/", "^docs/builds/"],
  }),
);
process.env.AGENTIC_CONFIG = fixture;

const {
  departmentsFor,
  explainMissing,
  explainMissingSkills,
  expectedSequence,
  missingAgents,
  requiredAgents,
  requiredSkills,
  sequenceProblems,
} = await import("../lib/departments.mjs");

const UNIVERSAL = ["qa-engineer", "code-reviewer"];

test("every change requires review and acceptance, whatever it touched", () => {
  for (const paths of [[], ["README.md"], ["src/db/schema.ts"]]) {
    for (const agent of UNIVERSAL) {
      assert.ok(requiredAgents(paths).includes(agent), `${agent} missing for ${paths}`);
    }
  }
});

test("a specialist is owed only by the work it owns", () => {
  assert.ok(requiredAgents(["src/db/schema.ts"]).includes("data-engineer"));
  assert.ok(!requiredAgents(["README.md"]).includes("data-engineer"));
});

test("queue bookkeeping alone owes nobody but the universal pair", () => {
  assert.deepEqual(requiredAgents([".agent-queue/done/01-x.md", "docs/builds/01-x.md"]).sort(), [
    "code-reviewer",
    "qa-engineer",
  ]);
});

test("a department is reported missing with the paths that required it", () => {
  const missing = missingAgents(["src/routes/thing.ts"], ["qa-engineer", "code-reviewer"]);
  assert.deepEqual(missing, ["backend-engineer"]);

  const [explained] = explainMissing(["src/routes/thing.ts"], ["qa-engineer", "code-reviewer"]);
  assert.equal(explained.agent, "backend-engineer");
  assert.deepEqual(explained.triggeredBy, ["src/routes/thing.ts"]);
});

test("design runs before the writers, and reviewers after them", () => {
  const sequence = expectedSequence(["public/index.html", "src/routes/thing.ts"]).map((d) => d.agent);
  assert.equal(sequence[0], "ui-ux-reviewer", "the designer goes first");
  assert.ok(sequence.indexOf("backend-engineer") < sequence.lastIndexOf("code-reviewer"));
  assert.equal(sequence[sequence.length - 1], "code-reviewer", "the last word is code review");
});

test("writing the page before designing it is a sequence problem", () => {
  const problems = sequenceProblems(
    ["public/index.html"],
    ["frontend-engineer", "ui-ux-reviewer", "qa-engineer", "code-reviewer"],
  );
  assert.ok(problems.length > 0, "a design that arrived after the build must be reported");
});

test("the declared order raises no problem", () => {
  const problems = sequenceProblems(
    ["src/routes/thing.ts"],
    ["backend-engineer", "qa-engineer", "code-reviewer"],
  );
  assert.deepEqual(problems, []);
});

test("a repeated review is expected, not a violation", () => {
  const problems = sequenceProblems(
    ["src/routes/thing.ts"],
    ["backend-engineer", "qa-engineer", "code-reviewer", "qa-engineer", "code-reviewer"],
  );
  assert.deepEqual(problems, []);
});

test("skills are owed by the diff and by what the brief describes", () => {
  assert.deepEqual(requiredSkills(["src/routes/thing.ts"], ""), ["api-endpoint"]);
  assert.deepEqual(requiredSkills([], "the assistant gives a wrong answer on returns"), [
    "debug-answer",
  ]);
  assert.deepEqual(requiredSkills(["README.md"], "tidy the readme"), []);
});

test("a skill it owed and never loaded is explained", () => {
  const [owed] = explainMissingSkills(["src/routes/thing.ts"], "", []);
  assert.equal(owed.skill, "api-endpoint");
  assert.ok(owed.why.length > 0);
});

test("departmentsFor returns the entries themselves, for explaining a refusal", () => {
  const labels = departmentsFor(["src/db/schema.ts"]).map((d) => d.label);
  assert.ok(labels.includes("Data"));
  assert.ok(labels.includes("Code review"));
});
