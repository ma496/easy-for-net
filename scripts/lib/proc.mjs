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

/**
 * Every pid below `root` in a `[pid, parentPid]` table, nearest first. Pure, so the walk is
 * tested without a process table; a cycle (a reused pid naming its own descendant as parent)
 * is walked once rather than for ever.
 */
export function descendantsOf(pairs, root) {
  const children = new Map();
  for (const [pid, parent] of pairs) {
    if (!children.has(parent)) children.set(parent, []);
    children.get(parent).push(pid);
  }
  const found = [];
  const seen = new Set([root]);
  const queue = [root];
  while (queue.length > 0) {
    for (const child of children.get(queue.shift()) ?? []) {
      if (seen.has(child)) continue;
      seen.add(child);
      found.push(child);
      queue.push(child);
    }
  }
  return found;
}

/** `[pid, parentPid]` for every process, or `[]` when the table cannot be read. */
function processTable() {
  const res = IS_WINDOWS
    ? spawnSync(
        "powershell",
        [
          "-NoProfile",
          "-NonInteractive",
          "-Command",
          "Get-CimInstance Win32_Process | ForEach-Object { \"$($_.ProcessId) $($_.ParentProcessId)\" }",
        ],
        { encoding: "utf8", windowsHide: true, timeout: 30_000 },
      )
    : spawnSync("ps", ["-A", "-o", "pid=,ppid="], { encoding: "utf8", timeout: 30_000 });
  return String(res.stdout ?? "")
    .split("\n")
    .map((line) => line.trim().split(/\s+/).map(Number))
    .filter(([pid, parent]) => Number.isInteger(pid) && Number.isInteger(parent) && pid > 0);
}

/** Force one pid down, ignoring a process that is already gone. */
function killOne(pid) {
  if (IS_WINDOWS) {
    spawnSync("taskkill", ["/PID", String(pid), "/F"], { stdio: "ignore", windowsHide: true });
    return;
  }
  try {
    process.kill(pid, "SIGKILL");
  } catch {
    /* already gone */
  }
}

/**
 * `killTree`, and then every process that was below `pid` when it was called, one by one.
 *
 * `taskkill /T` follows the tree as it stands while it walks it, and stops where a link is
 * already broken or a process refuses — and a session's background shells, test hosts and
 * servers are exactly the processes that keep a stopped attempt's pipes open, so the runner
 * waited on them for hours. The table is read first, so what was below the session is known
 * before anything in the middle of the tree dies.
 */
export function killTreeHard(pid) {
  if (!pid) return false;
  const below = descendantsOf(processTable(), pid);
  const stopped = killTree(pid);
  for (const child of below.reverse()) {
    if (isAlive(child)) killOne(child);
  }
  return stopped || below.length > 0;
}

/**
 * Run a command to completion with a hard deadline, asynchronously.
 *
 * `spawnSync`'s own timeout kills only the process it started — the shell or `node`, not the
 * `dotnet test` beneath it — and then waits for pipes the survivors still hold. This kills the
 * whole tree at the deadline, and stops waiting on the pipes a short grace after the process
 * itself has exited, so a hung check costs its timeout and nothing more.
 *
 * @returns {Promise<{status: number, stdout: string, stderr: string, timedOut: boolean, error?: Error}>}
 */
export function runWithDeadline(cmd, args = [], { timeoutMs, maxBytes = 64 * 1024 * 1024, graceMs = 15_000, ...options } = {}) {
  return new Promise((resolve) => {
    let child;
    try {
      child = spawnPortable(cmd, args, { ...options, stdio: ["ignore", "pipe", "pipe"], detached: !IS_WINDOWS });
    } catch (error) {
      resolve({ status: 1, stdout: "", stderr: "", timedOut: false, error });
      return;
    }
    const out = [];
    const err = [];
    let bytes = 0;
    const collect = (sink) => (chunk) => {
      if (bytes >= maxBytes) return;
      bytes += chunk.length;
      sink.push(chunk);
    };
    child.stdout.on("data", collect(out));
    child.stderr.on("data", collect(err));

    let timedOut = false;
    let settled = false;
    let exitCode = null;
    let grace = null;
    const finish = (status, error) => {
      if (settled) return;
      settled = true;
      clearTimeout(deadline);
      clearTimeout(grace);
      resolve({
        status,
        stdout: Buffer.concat(out).toString("utf8"),
        stderr: Buffer.concat(err).toString("utf8"),
        timedOut,
        ...(error ? { error } : {}),
      });
    };
    const deadline = Number.isFinite(timeoutMs)
      ? setTimeout(() => {
          timedOut = true;
          killTreeHard(child.pid);
          // Whatever still holds the pipes is not worth waiting for once the deadline has passed.
          grace = setTimeout(() => {
            child.stdout.destroy();
            child.stderr.destroy();
            finish(exitCode ?? 1);
          }, graceMs);
        }, Math.max(0, timeoutMs))
      : null;
    child.on("error", (error) => finish(1, error));
    child.on("exit", (code) => {
      exitCode = code ?? 1;
      // An orphan that inherited the pipes keeps "close" from ever firing; give the output a
      // moment to drain, then stop waiting for it.
      if (!grace) grace = setTimeout(() => finish(exitCode), graceMs);
    });
    child.on("close", (code) => finish(timedOut ? (exitCode ?? code ?? 1) : (code ?? exitCode ?? 1)));
  });
}
