#!/usr/bin/env node
/**
 * What the change in this checkout owes before it should be committed: the departments that
 * must see it, in the order the runner enforces, and the skills its shape calls for.
 *
 *   npm run owes                    # the working tree's changes
 *   npm run owes -- --base master   # everything since the branch left master, plus the tree
 *
 * The runner derives the same set from a task's finished diff and refuses an attempt that
 * skipped one (lib/departments.mjs). Working by hand, nothing told you that set — so a
 * change shipped with `/ship` could get one review where the queue would have required
 * three. This prints it, and spends nothing.
 */
import { spawnSync } from "node:child_process";
import { expectedSequence, departmentsFor, requiredSkills } from "./lib/departments.mjs";
import { workingTreePaths } from "./lib/changed-paths.mjs";

const argv = process.argv.slice(2);
const baseAt = argv.indexOf("--base");
const base = baseAt >= 0 ? argv[baseAt + 1] : null;
if (baseAt >= 0 && (!base || base.startsWith("--"))) {
  console.error("Usage: npm run owes [-- --base <branch>]");
  process.exit(1);
}

const paths = new Set(workingTreePaths());
if (base) {
  const mergeBase = (spawnSync("git", ["merge-base", "HEAD", base], { encoding: "utf8" }).stdout ?? "").trim();
  if (!mergeBase) {
    console.error(`No common history with ${base}.`);
    process.exit(1);
  }
  const committed = spawnSync("git", ["diff", "--name-only", mergeBase, "HEAD"], { encoding: "utf8" }).stdout ?? "";
  for (const p of committed.split("\n")) if (p.trim()) paths.add(p.trim());
}

const changed = [...paths].sort();
if (changed.length === 0) {
  console.log("Nothing has changed, so nothing is owed.");
  process.exit(0);
}

const why = new Map(departmentsFor(changed).map((d) => [`${d.agent}:${d.phase}`, d]));
const sequence = expectedSequence(changed);
const tiers = [...new Set(sequence.filter((d) => d.phase === "review").map((d) => d.tier))];

console.log(`${changed.length} changed path(s) owe:\n`);
for (const phase of ["design", "build"]) {
  const inPhase = sequence.filter((d) => d.phase === phase);
  if (inPhase.length === 0) continue;
  console.log(phase === "design" ? "  First, before code is written:" : "  Then the writers:");
  for (const d of inPhase) console.log(`    ${d.agent.padEnd(20)} ${why.get(`${d.agent}:${d.phase}`)?.why ?? ""}`);
}
for (const tier of tiers) {
  console.log(tiers.length > 1 ? `  Then reviews, tier ${tier} (together):` : "  Then the reviews, together:");
  for (const d of sequence.filter((s) => s.phase === "review" && s.tier === tier)) {
    console.log(`    ${d.agent.padEnd(20)} ${why.get(`${d.agent}:${d.phase}`)?.why ?? ""}`);
  }
}
const skills = requiredSkills(changed, "");
if (skills.length) console.log(`\n  Skills its shape calls for: ${skills.join(", ")}`);
