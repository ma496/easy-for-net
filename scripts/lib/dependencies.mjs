/**
 * The services a task's verification needs running, and whether they are.
 *
 * A task spends its whole attempt — a quarter of an hour and several dollars — before verify
 * first asks for the database or the cache. If one is down then, the attempt fails for a
 * reason the change did not cause, and the retry re-verifies everything once it is back. So
 * the runner asks first, before anything is spent, and refuses to start when a dependency
 * is down and cannot be started.
 *
 * The list is what `agentic.config.json` already declares: the cycle's `preflight` and the
 * verify service's `dependsOn`, merged by name. Nothing new to configure.
 */
import { runCommandSync, sleepSync } from "./proc.mjs";

/**
 * Every dependency the config names, once each. Names compare case-insensitively (the loop
 * says "Postgres", the service "postgres"); the first entry to name one wins, and a later
 * one only lends it a start command it lacked.
 */
export function taskDependencies(cfg) {
  const byName = new Map();
  for (const dep of [...(cfg?.cycle?.preflight ?? []), ...(cfg?.verify?.service?.dependsOn ?? [])]) {
    if (!dep?.name || !dep?.healthyWhen) continue;
    const key = String(dep.name).toLowerCase();
    const known = byName.get(key);
    if (!known) byName.set(key, { ...dep });
    else if (!known.start && dep.start) byName.set(key, { ...known, start: dep.start, waitSeconds: dep.waitSeconds });
  }
  return [...byName.values()];
}

/**
 * Whether a probe says the dependency is up: it exited 0. A `--filter` listing (docker ps and
 * friends) exits 0 whether or not anything matched, so there the output is the answer — the
 * same reading `loop.mjs` gives its preflight.
 */
export function probeHealthy(command, cwd) {
  const out = runCommandSync(command, { cwd, encoding: "utf8", stdio: "pipe" });
  if (out.status !== 0) return false;
  return command.includes("--filter") ? String(out.stdout ?? "").trim() !== "" : true;
}

/**
 * Bring up what is down and can be started; report what is still down.
 *
 * @returns {Array<{ name: string, whenMissing?: string }>} the dependencies still down
 */
export function ensureDependencies(deps, { cwd, log = () => {} } = {}) {
  const down = [];
  for (const dep of deps) {
    if (probeHealthy(dep.healthyWhen, cwd)) continue;
    if (dep.start) {
      log(`${dep.name} is down — starting it.`);
      runCommandSync(dep.start, { cwd, stdio: "ignore" });
      let up = false;
      for (let i = 0; i < Number(dep.waitSeconds ?? 60) / 2 && !up; i++) {
        up = probeHealthy(dep.healthyWhen, cwd);
        if (!up) sleepSync(2000);
      }
      if (up) continue;
    }
    down.push(dep);
  }
  return down;
}
