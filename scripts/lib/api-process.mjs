/**
 * Find the running processes that hold this checkout's API build output.
 *
 * A running API — a developer's `dotnet run`, a `dotnet watch`, a service a verify run left
 * with `--keep-stack` — keeps its assemblies under `src/backend/Source/bin/` open, and on
 * Windows that is a lock: `dotnet build`, `dotnet test` and `dotnet ef migrations add` all
 * fail with MSB3027/MSB3021 until it stops. Whatever launched it, the process holding the
 * lock is the one whose executable (the apphost) or whose argument (`dotnet <Name>.dll`) is
 * a file inside that directory, so that is the only thing matched — which also keeps the
 * match to this checkout: another checkout's API, a `dotnet test` host and the build
 * servers all live elsewhere.
 */
import { spawnSync } from "node:child_process";
import { join } from "node:path";
import { IS_WINDOWS } from "./proc.mjs";

/** The directory whose files a running API holds open. */
export function apiBinDir(repoRoot) {
  return join(repoRoot, "src", "backend", "Source", "bin");
}

/** Forward slashes, and lower case where the file system ignores case. */
function normalize(path, windows) {
  const p = String(path ?? "").replace(/\\/g, "/");
  return windows ? p.toLowerCase() : p;
}

/**
 * Pick the processes running from `binDir` out of a process listing.
 *
 * @param {{ pid: number, exe?: string, commandLine?: string }[]} processes
 * @param {string} binDir
 * @param {{ windows?: boolean, selfPid?: number }} [options]
 */
export function matchApiProcesses(processes, binDir, { windows = IS_WINDOWS, selfPid = process.pid } = {}) {
  const prefix = `${normalize(binDir, windows).replace(/\/+$/, "")}/`;
  return processes.filter((p) => {
    if (!p.pid || p.pid === selfPid) return false;
    if (normalize(p.exe, windows).startsWith(prefix)) return true;
    // Quoted or not, a path inside the bin directory anywhere on the command line: the
    // apphost as argv[0] on POSIX, or `dotnet <Name>.dll` on either.
    return normalize(p.commandLine, windows).includes(prefix);
  });
}

/** Every process on the machine, as `{ pid, exe, commandLine }`. */
export function listProcesses() {
  if (IS_WINDOWS) {
    const res = spawnSync(
      "powershell",
      [
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "Get-CimInstance Win32_Process | Select-Object ProcessId,ExecutablePath,CommandLine | ConvertTo-Json -Compress",
      ],
      { encoding: "utf8", windowsHide: true, maxBuffer: 64 * 1024 * 1024 },
    );
    if (res.status !== 0) throw new Error(`could not list processes: ${res.stderr || res.error?.message}`);
    const parsed = JSON.parse(res.stdout || "[]");
    return (Array.isArray(parsed) ? parsed : [parsed]).map((p) => ({
      pid: p.ProcessId,
      exe: p.ExecutablePath ?? "",
      commandLine: p.CommandLine ?? "",
    }));
  }
  const res = spawnSync("ps", ["-eo", "pid=,args="], { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
  if (res.status !== 0) throw new Error(`could not list processes: ${res.stderr || res.error?.message}`);
  return res.stdout
    .split("\n")
    .map((line) => line.trim().match(/^(\d+)\s+(.*)$/))
    .filter(Boolean)
    .map(([, pid, args]) => ({ pid: Number(pid), exe: "", commandLine: args }));
}
