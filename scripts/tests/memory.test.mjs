import { strict as assert } from "node:assert";
import { mkdtempSync, mkdirSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";

import {
  formatForBrief,
  lessonSlug,
  parseLesson,
  readLessons,
  selectLessons,
  scopesOf,
  scopeReach,
  writeLesson,
} from "../lib/memory.mjs";

const lesson = (over = {}) => ({
  file: "x.md",
  scope: "always",
  learned: "2026-01-01",
  task: "",
  title: "T",
  body: "# T\nbody",
  ...over,
});

function tempMemory() {
  const dir = mkdtempSync(join(tmpdir(), "memory-"));
  mkdirSync(join(dir, "lessons"), { recursive: true });
  return dir;
}

test("parses frontmatter and takes the title from the first heading", () => {
  const l = parseLesson("---\nscope: queue\nlearned: 2026-08-27\n---\n\n# Do not X\n\nbecause Y", "a.md");
  assert.equal(l.scope, "queue");
  assert.equal(l.learned, "2026-08-27");
  assert.equal(l.title, "Do not X");
});

test("scope defaults to always when the frontmatter omits it", () => {
  assert.equal(parseLesson("---\nlearned: 2026-01-01\n---\n\n# T\n\nb", "a.md").scope, "always");
});

test("a file with no frontmatter, or no body, is not a lesson", () => {
  assert.equal(parseLesson("# just a heading\n\ntext", "a.md"), null);
  assert.equal(parseLesson("---\nscope: queue\n---\n\n", "a.md"), null);
});

test("a malformed lesson on disk is skipped, never thrown", () => {
  const dir = tempMemory();
  writeFileSync(join(dir, "lessons", "good.md"), "---\nscope: always\n---\n\n# Good\n\nbody");
  writeFileSync(join(dir, "lessons", "bad.md"), "not a lesson at all");
  const found = readLessons(dir);
  assert.equal(found.length, 1);
  assert.equal(found[0].title, "Good");
});

test("readLessons on a directory with no lessons/ returns nothing rather than failing", () => {
  assert.deepEqual(readLessons(mkdtempSync(join(tmpdir(), "empty-"))), []);
});

test("always-scoped lessons match every task; keyword scopes match by mention", () => {
  const all = [
    lesson({ scope: "always", title: "Everywhere" }),
    lesson({ scope: "crawler", title: "Crawl" }),
    lesson({ scope: "queue", title: "Queue" }),
  ];
  const picked = selectLessons(all, "This task changes the QUEUE lanes.").map((l) => l.title);
  assert.deepEqual(picked.sort(), ["Everywhere", "Queue"]);
});

test("selection is case-insensitive and tolerates an empty task text", () => {
  const all = [lesson({ scope: "crawler", title: "Crawl" })];
  assert.equal(selectLessons(all, "touches the Crawler").length, 1);
  assert.equal(selectLessons(all, "").length, 0);
  assert.equal(selectLessons(all, undefined).length, 0);
});

test("no lessons produces no brief text at all", () => {
  assert.equal(formatForBrief([]), "");
});

test("newest lesson is rendered first", () => {
  const out = formatForBrief([
    lesson({ title: "Older", learned: "2026-01-01", body: "# Older\nold" }),
    lesson({ title: "Newer", learned: "2026-09-09", body: "# Newer\nnew" }),
  ]);
  assert.ok(out.indexOf("Newer") < out.indexOf("Older"));
});

test("the character cap truncates and says how many were dropped", () => {
  const many = Array.from({ length: 6 }, (_, i) =>
    lesson({ title: `L${i}`, learned: `2026-0${i + 1}-01`, body: `# L${i}\n${"x".repeat(400)}` }),
  );
  const out = formatForBrief(many, { maxChars: 900 });
  assert.ok(out.includes("older lesson(s) not shown"), "truncation must be disclosed");
  assert.ok(out.length < 2000);
});

test("the count cap also discloses what it dropped", () => {
  const many = Array.from({ length: 5 }, (_, i) => lesson({ title: `L${i}`, learned: "2026-01-01" }));
  const out = formatForBrief(many, { maxLessons: 2 });
  assert.ok(out.includes("3 older lesson(s) not shown"));
});

test("a cap that fits everything discloses nothing", () => {
  const out = formatForBrief([lesson({ title: "Only" })]);
  assert.ok(!out.includes("not shown"));
});

test("slugs are filename-safe and bounded", () => {
  assert.equal(lessonSlug("Do NOT use `git add -A`!"), "do-not-use-git-add-a");
  assert.ok(lessonSlug("x".repeat(200)).length <= 60);
});

test("re-recording the same title overwrites rather than duplicating", () => {
  const dir = tempMemory();
  writeLesson(dir, { title: "Same Thing", body: "first", scope: "queue" });
  writeLesson(dir, { title: "Same Thing", body: "second", scope: "queue" });
  const found = readLessons(dir);
  assert.equal(found.length, 1);
  assert.ok(found[0].body.includes("second"));
});

test("a written lesson reads back with its scope intact", () => {
  const dir = tempMemory();
  writeLesson(dir, { title: "Round Trip", body: "b", scope: "crawler", learned: "2026-08-27" });
  const [back] = readLessons(dir);
  assert.equal(back.scope, "crawler");
  assert.equal(back.learned, "2026-08-27");
  assert.equal(back.title, "Round Trip");
});

test("a lesson scoped to a list reaches every keyword in it", () => {
  const lessons = [
    { title: "multi", scope: "auth, hubspot, queue", body: "b" },
    { title: "single", scope: "queue", body: "b" },
  ];
  assert.deepEqual(
    selectLessons(lessons, "Gate the HubSpot push").map((l) => l.title),
    ["multi"],
  );
  assert.deepEqual(
    selectLessons(lessons, "sign-in and AUTH cookies").map((l) => l.title),
    ["multi"],
  );
  assert.equal(selectLessons(lessons, "the queue lanes").length, 2);
  // The literal comma string must not be what is matched.
  assert.equal(selectLessons(lessons, "auth, hubspot, queue").length, 2);
});

test("scopesOf keeps a hyphenated keyword whole", () => {
  assert.deepEqual(scopesOf({ scope: "product-query" }), ["product-query"]);
  assert.deepEqual(scopesOf({ scope: "portal, design" }), ["portal", "design"]);
  assert.deepEqual(scopesOf({}), ["always"]);
});

test("scopeReach counts the briefs a scope would actually reach", () => {
  const briefs = ["Rebuild the nav bar", "Fix the leads rail", "Tune the crawler"];
  assert.equal(scopeReach("nav", briefs), 1);
  assert.equal(scopeReach("leads", briefs), 1);
  // The case this exists to catch: a plausible word no brief uses.
  assert.equal(scopeReach("frontend", briefs), 0);
  assert.equal(scopeReach("always", briefs), 3);
});
