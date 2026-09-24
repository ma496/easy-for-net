import { strict as assert } from "node:assert";
import { spawnSync } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

/**
 * Every entry script must at least load.
 *
 * `node --check` parses a file; it does not run it, so a module-level `ReferenceError` —
 * a constant used above its definition, an import that was never added — passes the check
 * and passes the gate, then throws on the first real invocation. That happened: a
 * MEMORY_DIR line referencing a ROOT that did not exist in that file crashed
 * `agent-run.mjs` instantly, and because nothing in the gate executes these scripts, an
 * unattended drain failed every task it picked up before anyone noticed.
 *
 * These tests are cheap and shallow on purpose. They run each script in a mode that does
 * no work — no argument, or an explicit help/list — and assert only that the module
 * evaluated. A usage message on stderr is a pass; a stack trace is not.
 *
 * `verify.mjs` is deliberately absent: it has no help flag and treats an unknown one as a
 * normal run, so including it would run the whole gate inside the gate. It is also the
 * script hardest to break unnoticed, since every commit and every task invokes it.
 */
const SCRIPTS = resolve(join(dirname(fileURLToPath(import.meta.url)), ".."));

/** Each entry script, and an invocation that must not do any work. */
const ENTRIES = [
  { file: "agent-run.mjs", args: [] },
  { file: "agent-queue.mjs", args: ["list"] },
  { file: "record-lesson.mjs", args: ["--list"] },
  { file: "record-build.mjs", args: [] },
  { file: "run-journal.mjs", args: [] },
  { file: "schedule-drain.mjs", args: ["status"] },
  { file: "open-pr.mjs", args: [] },
  { file: "auto-ship.mjs", args: [] },
  { file: "loop.mjs", args: ["--dry-run"] },
];

for (const { file, args } of ENTRIES) {
  test(`${file} evaluates without throwing`, () => {
    const res = spawnSync("node", [join(SCRIPTS, file), ...args], {
      encoding: "utf8",
      timeout: 60_000,
      cwd: resolve(join(SCRIPTS, "..")),
    });
    const output = `${res.stdout ?? ""}${res.stderr ?? ""}`;

    // A module that failed to evaluate reports the throw and the ESM loader frames. A
    // script that merely declined to do anything useful (no arguments, nothing queued,
    // no database) is exactly what we asked for and is a pass.
    assert.ok(
      !/ReferenceError|TypeError: .* is not a function|Cannot find module|SyntaxError/.test(output),
      `${file} failed to load:\n${output.slice(0, 900)}`,
    );
    assert.ok(
      !/at ModuleJob\.run|asyncRunEntryPointWithESMLoader/.test(output),
      `${file} threw while the module was being evaluated:\n${output.slice(0, 900)}`,
    );
  });
}
