import { strict as assert } from "node:assert";
import { test } from "node:test";

import {
  findCommitFor,
  isDefinite,
  stemOf,
  subjectOf,
  taskTrailerOf,
  taskTrailersIn,
} from "../lib/commit-pairing.mjs";

const commit = (sha, subject, extra = "") => ({
  sha,
  subject,
  message: `${subject}\n\n${extra}Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>\n`,
});

test("a Task: trailer names the brief it came from", () => {
  assert.equal(
    taskTrailerOf("anything at all\n\nTask: 16-tenant-scoped-lead-routes\n"),
    "16-tenant-scoped-lead-routes",
  );
  assert.equal(taskTrailerOf("Task: 16-a.md"), "16-a", "the .md suffix is dropped");
  assert.equal(taskTrailerOf("  Task:   17-b  "), "17-b", "surrounding space is ignored");
});

test("prose that merely mentions a task is not a trailer", () => {
  assert.equal(taskTrailerOf("leads code new"), null);
  assert.equal(taskTrailerOf("This is the Task: we discussed at length"), null);
  assert.equal(taskTrailerOf(""), null);
  assert.equal(taskTrailerOf(undefined), null);
});

test("the trailer wins over a subject that matches a different commit", () => {
  const commits = [
    commit("aaa1111", "leads code new", "Task: 16-tenant-scoped-lead-routes\n"),
    commit("bbb2222", "Serve employer leads under the tenant in the URL"),
  ];
  const hit = findCommitFor({
    taskFile: "16-tenant-scoped-lead-routes.md",
    brief: "Serve employer leads under the tenant in the URL\n\nbody",
    commits,
  });
  assert.equal(hit.sha, "aaa1111");
  assert.equal(hit.matchedBy, "trailer");
  assert.ok(isDefinite(hit));
});

test("without a trailer, a whole-subject match still pairs — history predates the trailer", () => {
  const commits = [commit("ccc3333", "Read each tenant's verticals, regions, and qualifier")];
  const hit = findCommitFor({
    taskFile: "17-taxonomy.md",
    brief: "Read each tenant's verticals, regions, and qualifier.\n\nbody",
    commits,
  });
  assert.equal(hit.sha, "ccc3333");
  assert.equal(hit.matchedBy, "subject-exact");
  assert.ok(isDefinite(hit), "an exact subject match is safe to act on");
});

test("a truncated subject pairs, but is never definite enough to move a lane file", () => {
  const subject = "Put lead discovery behind a source interface, with Adzuna as the first source";
  const commits = [commit("ddd4444", subject.slice(0, 60))];
  const hit = findCommitFor({ taskFile: "25-sources.md", brief: `${subject}\n\nbody`, commits });
  assert.equal(hit.matchedBy, "subject-prefix");
  assert.equal(isDefinite(hit), false, "a guess must not file a task as done");
});

test("an unbuilt task pairs with nothing", () => {
  const commits = [commit("eee5555", "something entirely unrelated")];
  assert.equal(
    findCommitFor({ taskFile: "99-never-built.md", brief: "Do a new thing\n\nbody", commits }),
    null,
  );
});

test("two briefs with a short common prefix do not pair with each other's commit", () => {
  const commits = [commit("fff6666", "Add unit tests for apps/api/src/lib/extract/content-dedup")];
  const hit = findCommitFor({
    taskFile: "add-unit-tests-plat.md",
    brief: "Add unit tests for apps/api/src/lib/platform/spreadsheet\n\nbody",
    commits,
  });
  assert.equal(hit, null, "a shared prefix is not evidence of the same build");
});

test("an empty brief pairs with nothing rather than everything", () => {
  const commits = [commit("aaa0000", "")];
  assert.equal(findCommitFor({ taskFile: "empty.md", brief: "", commits }), null);
  assert.equal(findCommitFor({ taskFile: "empty.md", brief: "   \n\n", commits }), null);
});

test("every Task: line of a squashed message is read, in order", () => {
  const squash =
    "Add billing export (#42)\n\n* Add the export endpoint\n\nVerified with `npm run verify`.\n\nTask: billing-01-endpoint\n\n" +
    "* Add the export screen\n\nTask: billing-02-screen.md\n";
  assert.deepEqual(taskTrailersIn(squash), ["billing-01-endpoint", "billing-02-screen"]);
});

test("Task: only counts at the start of a line, not mid-sentence", () => {
  assert.deepEqual(taskTrailersIn("Fixes the Task: thing\nsee Task: x in the brief"), []);
  assert.deepEqual(taskTrailersIn(""), []);
});

test("stem and subject are derived the way the runner derives them", () => {
  assert.equal(stemOf("16-tenant-scoped-lead-routes.md"), "16-tenant-scoped-lead-routes");
  assert.equal(subjectOf("\n\nFirst real line.\nsecond\n"), "First real line");
});

test("a commit that names another task is never paired by its subject", () => {
  // Two plans' briefs open with the same line; only the plan whose task the commit names owns it.
  const commits = [{ sha: "aaa0000", subject: "Add alpha", message: "Add alpha\n\nTask: plan-a/01-alpha\n" }];
  assert.equal(findCommitFor({ taskFile: "plan-b/01-alpha.md", brief: "Add alpha\n", commits }), null);
  assert.equal(findCommitFor({ taskFile: "plan-a/01-alpha.md", brief: "Add alpha\n", commits })?.matchedBy, "trailer");
});
