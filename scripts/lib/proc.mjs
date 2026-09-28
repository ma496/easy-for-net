/**
 * Process helpers that behave the same on Windows, macOS and Linux.
 *
 * The engine shells out constantly — the gate, the live service, the dependency probes —
 * and the POSIX idioms it would otherwise reach for (`which`, `sleep`, `kill -- -pid`,
 * `mkdir -p`) either do not exist on Windows or resolve to something else entirely. Every
 * script goes through here instead, so a platform difference is fixed in one place.
 */
import { spawn, spawnSync } from "node:child_process";

export const IS_WINDOWS = process.platform === "win32";

/**
 * Split a configured command string into an executable and its arguments. Double-quoted
 * segments stay together, so `node "scripts/a b.mjs"` is two parts, not three.
 */
export function commandParts(command) {
  const parts = [];
  const re = /"([^"]*)"|(\S+)/g;
  let m;
  while ((m = re.exec(String(command ?? "").trim())) !== null) parts.push(m[1] ?? m[2]);
  return { cmd: parts[0], args: parts.slice(1) };
}

/**
 * Options that make a configured command resolve the way a terminal would.
 *
 * On Windows `npm`, `npx` and most tool shims are `.cmd` files, which `spawn` cannot start
 * without a shell — the call fails with ENOENT and a gate reads as failed when it never ran.
 * A shell is used there and only there; elsewhere the argv is passed straight through.
 */
export function shellOptions(options = {}) {
  return IS_WINDOWS ? { windowsHide: true, ...options, shell: true } : options;
}

/** Quote one argument for the Windows shell when it needs it. */
function quoteForShell(arg) {
  const s = String(arg);
  return IS_WINDOWS && /[\s"&|<>^]/.test(s) ? `"${s.replace(/"/g, '\\"')}"` : s;
}

/**
 * The command line a shell is handed on Windows. Node refuses to join an argv for a shell
 * itself (DEP0190), so it is joined here, quoting each argument that needs it.
 */
export function windowsCommandLine(cmd, args) {
  return [cmd, ...args].map(quoteForShell).join(" ");
}

/** `spawnSync` for an executable and argv, resolving `.cmd` shims on Windows. */
export function runSync(cmd, args = [], options = {}) {
  return IS_WINDOWS
    ? spawnSync(windowsCommandLine(cmd, args), shellOptions(options))
    : spawnSync(cmd, args, options);
}

/** Asynchronous counterpart of `runSync`. */
export function spawnPortable(cmd, args = [], options = {}) {
  return IS_WINDOWS ? spawn(windowsCommandLine(cmd, args), shellOptions(options)) : spawn(cmd, args, options);
}

/** `spawnSync` for a configured command string, resolving `.cmd` shims on Windows. */
export function runCommandSync(command, options = {}) {
  const { cmd, args } = commandParts(command);
  if (!cmd) return { status: 1, stdout: "", stderr: "empty command", error: new Error("empty command") };
  return runSync(cmd, args, options);
}

/** Asynchronous `spawn` for a configured command string, resolving `.cmd` shims on Windows. */
export function spawnCommand(command, options = {}) {
  const { cmd, args } = commandParts(command);
  return spawnPortable(cmd, args, options);
}

/** Whether an executable is on PATH — `where` on Windows, `command -v` elsewhere. */
export function hasExecutable(name) {
  const probe = IS_WINDOWS
    ? spawnSync("where", [name], { stdio: "ignore", windowsHide: true })
    : spawnSync("sh", ["-c", `command -v "${name}"`], { stdio: "ignore" });
  return probe.status === 0;
}

/** Block the thread for `ms` milliseconds without spawning `sleep`, which Windows lacks. */
export function sleepSync(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, Math.max(0, ms));
}

/**
 * Stop a process and everything it started.
 *
 * A detached service on POSIX leads its own process group, so the negative pid reaches the
 * whole tree. Windows has no process groups in that sense, and `dotnet run` or `npm run dev`
 * leave their real server as a grandchild — `taskkill /T` is what reaches it.
 */
export function killTree(pid) {
  if (!pid) return false;
  if (IS_WINDOWS) {
    return spawnSync("taskkill", ["/PID", String(pid), "/T", "/F"], { stdio: "ignore", windowsHide: true }).status === 0;
  }
  try {
    process.kill(-pid, "SIGTERM");
    return true;
  } catch {
    try {
      process.kill(pid, "SIGTERM");
      return true;
    } catch {
      return false;
    }
  }
}

/** Whether a process with this pid is alive. */
export function isAlive(pid) {
  if (!pid) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (err) {
    // EPERM means it exists but belongs to someone else — alive, as far as a lock cares.
    return err.code === "EPERM";
  }
}
