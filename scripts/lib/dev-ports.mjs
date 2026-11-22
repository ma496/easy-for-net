/**
 * The development ports `npm run dev` runs on, and whether something else already holds one.
 *
 * Three ports belong to the running app, beside the PostgreSQL and Redis ports Docker publishes:
 *
 *   DEV_PROXY_PORT  3000  the dev proxy — the one address the browser opens
 *   DEV_WEB_PORT    3001  `next dev`, behind the proxy
 *   DEV_API_PORT    5000  the API
 *
 * Each is read from the environment first, then from the root `.env` (the file Docker Compose
 * reads for DEV_POSTGRES_PORT / DEV_REDIS_PORT), then the default. Only these three keys are
 * read from `.env` (`readDotenvValues`): it holds the production secrets too, and nothing here
 * needs them. Only the developer-run scripts (dev.mjs, serve-api.mjs) read the file; the engine
 * (verify.mjs) takes the ports from the environment alone.
 *
 * Why a port is checked before anything binds it: the dev proxy listens on the loopback
 * addresses, and Windows and macOS both let a socket bind `127.0.0.1:3000` while another process
 * holds the wildcard `0.0.0.0:3000`. The bind succeeds, the browser's `localhost:3000` reaches
 * whichever listener is most specific, and another app on the machine silently disappears behind
 * this one (on Linux the same bind fails). So `isPortFree` asks two questions, and a port is free
 * only when both say so: does anything answer a connection on loopback, and can this process bind
 * the port itself on each loopback address. The first catches a wildcard listener on every OS —
 * including the AirPlay Receiver macOS runs on 5000 — and the second a port the OS refuses
 * (the ranges Hyper-V reserves on Windows answer EACCES).
 */
import { existsSync, readFileSync } from "node:fs";
import net from "node:net";
import { join } from "node:path";
import { readDotenvValues } from "./deploy-vps.mjs";
import { IS_WINDOWS, runSync } from "./proc.mjs";

/** Each port, the variable that moves it, and its default. */
export const DEV_PORTS = Object.freeze([
  { key: "proxy", variable: "DEV_PROXY_PORT", fallback: 3000, label: "the dev proxy (the address the browser opens)" },
  { key: "web", variable: "DEV_WEB_PORT", fallback: 3001, label: "next dev (behind the proxy)" },
  { key: "api", variable: "DEV_API_PORT", fallback: 5000, label: "the API" },
]);

/** The values of the dev-port variables a `.env` file sets, by variable name; nothing else in it. */
export function parseDevPortLines(text) {
  return readDotenvValues(text, DEV_PORTS.map((p) => p.variable));
}

/**
 * The three ports, with where each came from. Throws an Error naming the variable when a value is
 * not a port number, or when two of them are the same port.
 *
 * @param {{ env?: Record<string, string | undefined>, root?: string, envFile?: string | null }} [options]
 *   `envFile` is the `.env` text to read; left out, `<root>/.env` is read when it exists.
 * @returns {{ proxy: number, web: number, api: number, sources: Record<string, "environment" | ".env" | "default"> }}
 */
export function readDevPorts({ env = process.env, root, envFile } = {}) {
  let fileText = envFile;
  if (fileText === undefined && root) {
    const path = join(root, ".env");
    try {
      fileText = existsSync(path) ? readFileSync(path, "utf8") : null;
    } catch {
      fileText = null; // unreadable: the defaults still let the app start
    }
  }
  const fromFile = fileText ? parseDevPortLines(fileText) : {};
  const ports = { sources: {} };
  for (const { key, variable, fallback } of DEV_PORTS) {
    const fromEnv = env[variable];
    const raw = fromEnv !== undefined && fromEnv !== "" ? fromEnv : fromFile[variable];
    const source = fromEnv !== undefined && fromEnv !== "" ? "environment" : raw !== undefined && raw !== "" ? ".env" : "default";
    const value = source === "default" ? fallback : Number(raw);
    if (!Number.isInteger(value) || value < 1 || value > 65535) {
      throw new Error(`${variable}=${raw} (from ${source}) is not a port number between 1 and 65535.`);
    }
    ports[key] = value;
    ports.sources[key] = source;
  }
  for (const [a, b] of [["proxy", "web"], ["proxy", "api"], ["web", "api"]]) {
    if (ports[a] === ports[b]) {
      const name = (k) => DEV_PORTS.find((p) => p.key === k).variable;
      throw new Error(`${name(a)} and ${name(b)} are both ${ports[a]}; each needs a port of its own.`);
    }
  }
  return ports;
}

/** Whether something accepts a TCP connection on `host:port` within `timeoutMs`. */
function answers(host, port, timeoutMs) {
  return new Promise((resolve) => {
    const socket = net.connect({ host, port });
    const done = (result) => {
      socket.destroy();
      resolve(result);
    };
    socket.setTimeout(timeoutMs, () => done(false));
    socket.once("connect", () => done(true));
    socket.once("error", () => done(false));
  });
}

/**
 * Whether this process can bind `host:port`. "unavailable" means the address itself does not exist
 * here (IPv6 switched off), which says nothing about the port.
 */
function bindable(host, port) {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once("error", (err) => resolve(["EADDRNOTAVAIL", "EAFNOSUPPORT"].includes(err.code) ? "unavailable" : false));
    server.listen({ host, port, exclusive: true }, () => server.close(() => resolve(true)));
  });
}

/**
 * Whether nothing on this machine holds `port` for a loopback listener: nothing answers on
 * 127.0.0.1 or ::1, and both addresses can be bound. See the header for why both are asked.
 */
export async function isPortFree(port, { timeoutMs = 500 } = {}) {
  const [v4, v6] = await Promise.all([answers("127.0.0.1", port, timeoutMs), answers("::1", port, timeoutMs)]);
  if (v4 || v6) return false;
  if ((await bindable("127.0.0.1", port)) !== true) return false;
  return (await bindable("::1", port)) !== false;
}

/** The first free port above `port` (within `range`), skipping `taken`; null when there is none. */
export async function nextFreePort(port, { taken = [], range = 100 } = {}) {
  for (let candidate = port + 1; candidate <= Math.min(port + range, 65535); candidate++) {
    if (!taken.includes(candidate) && (await isPortFree(candidate))) return candidate;
  }
  return null;
}

/**
 * Who listens on `port`, as "name (PID n)", or "" when that cannot be told — best effort, for the
 * message only. netstat + tasklist on Windows, lsof on macOS and Linux.
 */
export function describePortHolder(port) {
  try {
    if (IS_WINDOWS) {
      const out = String(runSync("netstat", ["-ano"],{ encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).stdout ?? "");
      const pid = out
        .split(/\r?\n/)
        .map((line) => line.trim().split(/\s+/))
        .find((cols) => cols[3] === "LISTENING" && new RegExp(`:${port}$`).test(cols[1] ?? ""))?.[4];
      if (!pid) return "";
      const task = String(
        runSync("tasklist", ["/FI", `PID eq ${pid}`, "/FO", "CSV", "/NH"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).stdout ?? "",
      );
      const name = /^"([^"]+)"/.exec(task.trim())?.[1];
      return name ? `${name} (PID ${pid})` : `PID ${pid}`;
    }
    const out = String(
      runSync("lsof", ["-nP", `-iTCP:${port}`, "-sTCP:LISTEN"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).stdout ?? "",
    );
    const cols = out.split(/\r?\n/)[1]?.trim().split(/\s+/);
    return cols?.length > 1 ? `${cols[0]} (PID ${cols[1]})` : "";
  } catch {
    return "";
  }
}

/** A hint for a port a well-known service holds, or "". */
export function knownHolderHint(port, platform = process.platform) {
  if (platform === "darwin" && (port === 5000 || port === 7000)) {
    return "on macOS this is usually the AirPlay Receiver (System Settings → General → AirDrop & Handoff)";
  }
  return "";
}

/**
 * The `Web` origins the API must be told, as environment variables, so that the links it builds
 * and the origins it trusts name the proxy's port. The browser reaches the app through the proxy
 * alone, so a `localhost` origin (or a `*.localhost` one, or `{tenant}.localhost`) in `Web` whose
 * port is not the proxy's is rewritten to it; any other origin is left as configured.
 *
 * @param {{ DefaultDomain?: string, Domains?: string[], TenantOriginTemplate?: string } | undefined} web
 *   The `Web` section the API would read.
 * @param {number} proxyPort
 * @returns {Record<string, string>} `Web__…` variables, empty when every origin already agrees.
 */
export function webOriginOverrides(web, proxyPort) {
  if (!web || typeof web !== "object") return {};
  const rewrite = (origin) =>
    typeof origin === "string"
      ? origin.replace(/^(https?:\/\/(?:[^/@:]+\.)?localhost)(?::\d+)?(?=\/|$)/i, `$1:${proxyPort}`)
      : origin;
  const overrides = {};
  for (const key of ["DefaultDomain", "TenantOriginTemplate"]) {
    if (typeof web[key] === "string" && web[key] && rewrite(web[key]) !== web[key]) overrides[`Web__${key}`] = rewrite(web[key]);
  }
  if (Array.isArray(web.Domains) && web.Domains.some((d) => rewrite(d) !== d)) {
    // An array is overridden index by index, so every entry is passed, changed or not.
    web.Domains.forEach((d, i) => (overrides[`Web__Domains__${i}`] = rewrite(d)));
  }
  return overrides;
}

/**
 * The `Web` section the API runs with in `environment`: `appsettings.<environment>.json`'s, else
 * `appsettings.json`'s, read from `<root>/<backend>/Source`. Undefined when neither has one.
 */
export function readWebSettings({ root, backend, environment = "Development" }) {
  const dir = join(root, backend, "Source");
  for (const name of [`appsettings.${environment}.json`, "appsettings.json"]) {
    const path = join(dir, name);
    if (!existsSync(path)) continue;
    try {
      const web = JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""))?.Web;
      if (web) return web;
    } catch {
      /* unreadable settings — try the next file */
    }
  }
  return undefined;
}
