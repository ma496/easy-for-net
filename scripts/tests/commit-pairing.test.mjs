import { strict as assert } from "node:assert";
import { test } from "node:test";

import {
  findCommitFor,
  isDefinite,
  recordedShaIn,
  stemOf,
  subjectOf,
  taskTrailerOf,
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

test("a recorded sha is read back, and 'none' reads as absent", () => {
  assert.equal(recordedShaIn("| **Commit** | `67e149b` |"), "67e149b");
  assert.equal(recordedShaIn("| **Commit** | **none** — no commit carries the work |"), "");
  assert.equal(recordedShaIn(""), "");
});

test("stem and subject are derived the way the runner derives them", () => {
  assert.equal(stemOf("16-tenant-scoped-lead-routes.md"), "16-tenant-scoped-lead-routes");
  assert.equal(subjectOf("\n\nFirst real line.\nsecond\n"), "First real line");
});
