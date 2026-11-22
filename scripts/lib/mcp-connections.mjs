/**
 * The connections the project's MCP servers (`.mcp.json`, `scripts/mcp/`) open, read from the
 * same settings the API runs with.
 *
 * `.mcp.json` is checked in, so it names no credential. Each wrapper in `scripts/mcp/` calls
 * `readDevConnectionStrings` when Claude Code starts it, converts the .NET connection string to
 * what its server expects, and hands it over in the child's environment — never in argv, where
 * any process listing would show it. The settings file is git-ignored and written per project by
 * the generator, so a generated project connects to its own database and ports with nothing to
 * configure.
 */
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { hasExecutable, killTreeHard, spawnPortable } from "./proc.mjs";
import { REPO_ROOT, selectApp } from "./project-config.mjs";

/**
 * Start an MCP server over the wrapper's own stdio and exit with its code. stdout is the MCP
 * protocol, so everything the wrapper itself says goes to stderr.
 */
export function launchMcpServer(name, cmd, args, env = {}) {
  if (!hasExecutable(cmd)) {
    const hint = cmd === "uvx" ? " Install uv: https://docs.astral.sh/uv/getting-started/installation/" : "";
    failMcp(name, `${cmd} is not on PATH.${hint}`);
  }
  // stdin is relayed rather than inherited so the wrapper sees the client close it, which is how
  // an MCP client ends a stdio server. Not every server exits on that (Postgres MCP keeps its
  // pool's workers alive on Windows), so the whole tree is ended after a short grace.
  const child = spawnPortable(cmd, args, { stdio: ["pipe", "inherit", "inherit"], env: { ...process.env, ...env } });
  process.stdin.pipe(child.stdin);
  child.stdin.on("error", () => {});
  // On Windows the child is a shell, which can exit while the server beneath it runs on; the
  // tree is found through the parent pids the survivors still carry.
  let closing = false;
  const stop = (code = 0) => {
    killTreeHard(child.pid);
    process.exit(code);
  };
  process.stdin.on("end", () => {
    closing = true;
    child.stdin.end();
    setTimeout(stop, SHUTDOWN_GRACE_MS);
  });
  child.on("error", (error) => failMcp(name, `could not start ${cmd}: ${error.message}`));
  child.on("exit", (code, signal) => {
    if (!closing) stop(code ?? (signal ? 1 : 0));
  });
  for (const signal of ["SIGINT", "SIGTERM", "SIGHUP"]) process.on(signal, () => stop(0));
}

/** How long a server gets to exit by itself once its client has closed stdin. */
const SHUTDOWN_GRACE_MS = 2000;

/** Report why an MCP server could not start, on stderr, and exit 1. */
export function failMcp(name, message) {
  process.stderr.write(`[mcp:${name}] ${message}\n`);
  process.exit(1);
}

/**
 * `ConnectionStrings.DefaultConnection` and `ConnectionStrings.Redis` for one environment, each
 * from `appsettings.<environment>.json`, falling back to `appsettings.json`; undefined when
 * neither file names it.
 */
export function readDevConnectionStrings({ app = selectApp(), environment = "Development", root = REPO_ROOT } = {}) {
  const dir = join(root, app.backend, "Source");
  const files = [`appsettings.${environment}.json`, "appsettings.json"].map((f) => join(dir, f)).filter((p) => existsSync(p));
  const pick = (key) => {
    for (const path of files) {
      try {
        const value = JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""))?.ConnectionStrings?.[key];
        if (value) return { value, path };
      } catch {
        /* unreadable settings — try the next file */
      }
    }
    return undefined;
  };
  return { postgres: pick("DefaultConnection"), redis: pick("Redis") };
}

/**
 * `localhost` as the IPv4 loopback. docker-compose.yml publishes PostgreSQL and Redis on
 * 127.0.0.1 alone, and `localhost` resolves to `::1` first on Windows: a plain psycopg connect
 * falls back to IPv4, but the connection pool Postgres MCP opens waits on `::1` until it times out.
 */
export function ipv4Loopback(host) {
  return host.toLowerCase() === "localhost" ? "127.0.0.1" : host;
}

/** The `key=value;` pairs of an Npgsql connection string, keys lowercased with spaces removed. */
function npgsqlParts(cs) {
  const parts = {};
  for (const segment of cs.split(";")) {
    const at = segment.indexOf("=");
    if (at <= 0) continue;
    parts[segment.slice(0, at).trim().toLowerCase().replace(/\s+/g, "")] = segment.slice(at + 1).trim();
  }
  return parts;
}

/**
 * An Npgsql connection string (`Host=…;Port=…;Database=…;Username=…;Password=…`, with the
 * `Server`, `User Id`, `User`, `Pwd` aliases) as a `postgresql://` URI, `localhost` as 127.0.0.1.
 */
export function npgsqlToUri(cs) {
  const p = npgsqlParts(cs);
  const host = ipv4Loopback(p.host || p.server || "localhost");
  const port = Number(p.port) || 5432;
  const user = p.username || p.userid || p.user || p.uid || "";
  const password = p.password || p.pwd || "";
  const database = p.database || p.db || "";
  const auth = user ? `${encodeURIComponent(user)}${password ? `:${encodeURIComponent(password)}` : ""}@` : "";
  const ssl = p.sslmode && p.sslmode.toLowerCase() !== "disable" ? `?sslmode=${p.sslmode.toLowerCase()}` : "";
  return `postgresql://${auth}${host}:${port}/${encodeURIComponent(database)}${ssl}`;
}

/**
 * A StackExchange.Redis configuration string (`host:port[,password=…][,user=…][,ssl=true]`) as
 * the `REDIS_*` environment variables the Redis MCP server reads.
 */
export function stackExchangeToRedisEnv(cs) {
  const [endpoint, ...options] = cs.split(",").map((s) => s.trim());
  const separator = endpoint.lastIndexOf(":");
  const host = ipv4Loopback((separator > 0 ? endpoint.slice(0, separator) : endpoint) || "localhost");
  const port = separator > 0 ? Number(endpoint.slice(separator + 1)) || 6379 : 6379;
  const env = { REDIS_HOST: host, REDIS_PORT: String(port) };
  for (const option of options) {
    const at = option.indexOf("=");
    if (at <= 0) continue;
    const key = option.slice(0, at).trim().toLowerCase();
    const value = option.slice(at + 1).trim();
    if (key === "password") env.REDIS_PWD = value;
    else if (key === "user") env.REDIS_USERNAME = value;
    else if (key === "ssl") env.REDIS_SSL = value.toLowerCase() === "true" ? "True" : "False";
    else if (key === "defaultdatabase") env.REDIS_DB = value;
  }
  return env;
}

/**
 * Where the Playwright MCP server writes screenshots and snapshots: one folder per branch, and
 * under it one per task, so everything a task's browser checks produced is in one place —
 * `.playwright-mcp/<branch>/<spec>/<task>/`. The runner names the task (`AGENT_TASK_ID`, the
 * task's `<spec>/<name>` stem); a session outside a run is `manual`. A branch's `/` becomes `-`,
 * so `feat/users` is one folder rather than two, and anything a folder name cannot hold is
 * replaced too. Pure, so the layout is tested without git.
 */
export function screenshotDir({ branch, taskId, root = ".playwright-mcp" } = {}) {
  const clean = (part) => String(part ?? "").trim().replace(/[\/]+/g, "-").replace(/[^\w.-]+/g, "-").replace(/^[-.]+|[-.]+$/g, "");
  const parts = String(taskId ?? "").split("/").map(clean).filter(Boolean);
  return [root, clean(branch) || "detached", ...(parts.length ? parts : ["manual"])].join("/");
}
