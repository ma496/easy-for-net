#!/usr/bin/env node
/**
 * Stop this checkout's running API, so a backend build can overwrite its output.
 *
 *   npm run stop:api              # stop it, if one is running
 *   npm run stop:api -- --list    # say what would be stopped, stop nothing
 *
 * A running API holds `src/backend/Source/bin/` open, and on Windows `dotnet build`,
 * `dotnet test` and `dotnet ef migrations add` then fail with MSB3027/MSB3021. The gate calls
 * this before its build step, and the backend agents before every build they run. Only
 * processes running from this checkout's bin directory are touched (see lib/api-process.mjs).
 *
 * Exits 0 when nothing is left running — including when nothing was — and 1 when a process
 * survived being stopped, since the build that follows would fail on the same lock.
 */
import { existsSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { join } from "node:path";
import { apiBinDir, listProcesses, matchApiProcesses } from "./lib/api-process.mjs";
import { isAlive, killTree, sleepSync } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";
import { normalizeRoot } from "./lib/api-identity.mjs";

const LIST_ONLY = process.argv.includes("--list") || process.argv.includes("--help");

let found;
try {
  found = matchApiProcesses(listProcesses(), apiBinDir(REPO_ROOT));
} catch (err) {
  // Not being able to look is not a reason to fail a build that may well not be locked.
  console.log(`stop-api: ${err.message}; carrying on without stopping anything.`);
  process.exit(0);
}

if (!found.length) {
  console.log("stop-api: no running API from this checkout.");
  process.exit(0);
}

for (const p of found) {
  console.log(`stop-api: ${LIST_ONLY ? "would stop" : "stopping"} pid ${p.pid} — ${(p.commandLine || p.exe).slice(0, 200)}`);
}
if (LIST_ONLY) process.exit(0);

for (const p of found) killTree(p.pid);

// taskkill and SIGTERM return before the process is gone, and the file handles go with it.
let survivors = found;
for (let i = 0; i < 20 && survivors.length; i++) {
  sleepSync(250);
  survivors = survivors.filter((p) => isAlive(p.pid));
}

dropDeadServiceRecords();

if (survivors.length) {
  console.log(`stop-api: still running: ${survivors.map((p) => p.pid).join(", ")}. Stop it by hand before building.`);
  process.exit(1);
}
console.log(`stop-api: stopped ${found.length} process(es).`);

/**
 * verify.mjs records each service it starts in `.agent-runs/dev-service-<port>.json`. A
 * record for this checkout whose process is now gone would only mislead the next run.
 */
function dropDeadServiceRecords() {
  const dir = join(REPO_ROOT, ".agent-runs");
  if (!existsSync(dir)) return;
  for (const file of readdirSync(dir).filter((f) => /^dev-service-\d+\.json$/.test(f))) {
    try {
      const record = JSON.parse(readFileSync(join(dir, file), "utf8"));
      if (normalizeRoot(record.repoRoot ?? "") === normalizeRoot(REPO_ROOT) && !isAlive(record.pid)) {
        rmSync(join(dir, file), { force: true });
      }
    } catch {
      // A record that does not parse is not ours to judge.
    }
  }
}
