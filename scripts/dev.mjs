#!/usr/bin/env node
/**
 * Run the whole app for development with one command: services, API and web app.
 *
 *   npm run dev                      # PostgreSQL + Redis, the API on :5000, the web app on :3000
 *   npm run dev -- --no-docker       # use the PostgreSQL and Redis you already run
 *   npm run dev -- --api-only        # services + API, no web app
 *   npm run dev -- --web-only        # the web app alone, against an API started elsewhere
 *
 * 1. Probes PostgreSQL and Redis (pg-ready.mjs, redis-ready.mjs). When either is down it runs
 *    `docker compose up -d --wait` on docker-compose.yml and waits for both to answer. A port
 *    another server already holds is named, with how to move off it (DEV_POSTGRES_PORT /
 *    DEV_REDIS_PORT in .env and the connection strings); a service that answers but is not this
 *    project's container (a PostgreSQL installed on the machine) is warned about, not used silently.
 * 2. Stops an API already running from this checkout, which would hold the port and lock bin/.
 * 3. Installs the web app's packages when node_modules is missing.
 * 4. Starts the API (serve-api.mjs, Development) and `next dev`, with each line prefixed by
 *    where it came from. Ctrl+C, or either process exiting, stops both. The containers are left
 *    running; `docker compose down` stops them.
 */
import { existsSync } from "node:fs";
import { join } from "node:path";
import { hasExecutable, killTree, IS_WINDOWS, runSync, sleepSync, spawnPortable } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";

const args = process.argv.slice(2);
const NO_DOCKER = args.includes("--no-docker");
const API_ONLY = args.includes("--api-only");
const WEB_ONLY = args.includes("--web-only");
const WEB = join(REPO_ROOT, "src", "frontend", "web");
const API_PORT = process.env.PORT || "5000";
/**
 * The two services, each with the probe that reads its address from the connection string, the
 * Compose service and container port that serve it, and the .env variable that moves its host port.
 */
const SERVICES = [
  { name: "PostgreSQL", probe: "pg-ready.mjs", service: "postgres", containerPort: 5432, portVariable: "DEV_POSTGRES_PORT" },
  { name: "Redis", probe: "redis-ready.mjs", service: "redis", containerPort: 6379, portVariable: "DEV_REDIS_PORT" },
];
const PORT_ADVICE =
  "set {variable} in the root .env to a free port and the same port in the connection strings of " +
  "src/backend/Source/appsettings.Development.json and appsettings.Testing.json";

if (args.includes("--help")) {
  console.log("usage: npm run dev [-- --no-docker] [-- --api-only | --web-only]");
  process.exit(0);
}
if (API_ONLY && WEB_ONLY) fail("--api-only and --web-only cannot be used together.");

if (!WEB_ONLY) {
  ensureServices();
  runSync("node", [join(REPO_ROOT, "scripts", "stop-api.mjs")], { cwd: REPO_ROOT, stdio: "inherit" });
  if (!existsSync(join(REPO_ROOT, "src", "backend", "Source", "Migrations"))) {
    console.log(
      "dev: no migrations yet — run `dotnet tool restore` and " +
        "`dotnet ef migrations add Initial --project src/backend/Source` first, or the API cannot create its database.",
    );
  }
}
if (!API_ONLY && !existsSync(join(WEB, "node_modules"))) {
  console.log("dev: installing the web app's packages…");
  if (runSync("npm", ["install"], { cwd: WEB, stdio: "inherit" }).status !== 0) fail("npm install failed.");
}

const children = [];
if (!WEB_ONLY) {
  children.push(
    start("api", "node", [join(REPO_ROOT, "scripts", "serve-api.mjs")], { cwd: REPO_ROOT, env: { PORT: API_PORT } }),
  );
}
if (!API_ONLY) children.push(start("web", "npm", ["run", "dev"], { cwd: WEB }));

console.log(
  [
    !WEB_ONLY && `dev: API  http://localhost:${API_PORT}  (Swagger at /swagger)`,
    !API_ONLY && "dev: web  http://localhost:3000",
    "dev: Ctrl+C stops both.",
  ]
    .filter(Boolean)
    .join("\n"),
);

let stopping = false;
function stopAll(code) {
  if (stopping) return;
  stopping = true;
  for (const child of children) if (child.exitCode === null) killTree(child.pid);
  process.exit(code);
}
for (const sig of ["SIGINT", "SIGTERM"]) process.on(sig, () => stopAll(0));

/** Start one process, prefixing every line it writes, and stop everything when it exits. */
function start(name, cmd, cmdArgs, { cwd, env = {} }) {
  const child = spawnPortable(cmd, cmdArgs, {
    cwd,
    // A detached child leads its own process group on POSIX, which is what lets killTree reach
    // the server it starts. On Windows taskkill /T does that, and detaching would open a console.
    detached: !IS_WINDOWS,
    stdio: ["ignore", "pipe", "pipe"],
    env: { ...process.env, FORCE_COLOR: "1", ...env },
  });
  const label = `[${name}]`;
  for (const stream of [child.stdout, child.stderr]) prefixLines(stream, label);
  child.on("exit", (code) => {
    if (stopping) return;
    console.log(`dev: ${name} exited with code ${code ?? "?"}; stopping the rest.`);
    stopAll(code ?? 1);
  });
  return child;
}

function prefixLines(stream, label) {
  let buffered = "";
  stream.setEncoding("utf8");
  stream.on("data", (chunk) => {
    const lines = (buffered + chunk).split(/\r?\n/);
    buffered = lines.pop();
    for (const line of lines) process.stdout.write(`${label} ${line}\n`);
  });
  stream.on("end", () => buffered && process.stdout.write(`${label} ${buffered}\n`));
}

/** PostgreSQL and Redis answering, starting them with Docker Compose when they are not. */
function ensureServices() {
  if (SERVICES.every((s) => probe(s.probe))) {
    if (!NO_DOCKER) warnForeignServers();
    return;
  }
  if (NO_DOCKER) fail("PostgreSQL or Redis is not answering, and --no-docker was given. Start them and run again.");
  if (!hasExecutable("docker")) {
    fail("PostgreSQL or Redis is not answering, and docker is not on PATH. Install Docker or start them yourself.");
  }
  // A port that already answers while its container is not running belongs to some other server, and the
  // container will fail to publish on it. With Docker itself down (ps fails) that cannot be told, so nothing is held.
  const running = compose(["ps", "--status", "running", "--services"]);
  const held = running === null ? [] : SERVICES.filter((s) => probe(s.probe) && !running.split(/\r?\n/).includes(s.service));
  console.log("dev: starting PostgreSQL and Redis (docker compose up -d)…");
  const up = runSync("docker", ["compose", "-f", join(REPO_ROOT, "docker-compose.yml"), "up", "-d", "--wait"], {
    cwd: REPO_ROOT,
    stdio: "inherit",
  });
  if (up.status !== 0) {
    if (held.length === 0) fail("docker compose up failed. Is Docker running?");
    fail(
      "docker compose up failed. " +
        held
          .map((s) => `${s.name}'s port is already taken by another server (${probeOutput(s.probe)}): ${portAdvice(s)}.`)
          .join(" "),
    );
  }
  // --wait follows the containers' healthchecks; the probes confirm the ports the API uses.
  for (let i = 0; i < 30; i++) {
    if (SERVICES.every((s) => probe(s.probe))) return;
    sleepSync(1000);
  }
  fail(
    "PostgreSQL or Redis did not start answering within 30 seconds. If .env publishes them on other ports than the " +
      "connection strings name, make the two agree.",
  );
}

/**
 * When a service answers but not from this project's container — a PostgreSQL installed on the
 * machine, another project's container — the API will connect to it, with credentials that were
 * written for the container. Say so rather than leave a login failure to explain it. A Docker that
 * is not running proves nothing either way, so it says nothing.
 */
function warnForeignServers() {
  if (!hasExecutable("docker") || compose(["ps"]) === null) return;
  for (const s of SERVICES) {
    const target = probeOutput(s.probe);
    const configuredPort = target.split(":").pop();
    const published = (compose(["port", s.service, String(s.containerPort)]) ?? "").trim().split(":").pop();
    if (!published) {
      console.log(
        `dev: warning: ${s.name} answers on ${target}, but this project's container is not running — the API will use ` +
          `that other server. If that is intended, run with --no-docker; otherwise ${portAdvice(s)}, then run again.`,
      );
    } else if (published !== configuredPort) {
      console.log(
        `dev: warning: this project's ${s.name} container is published on port ${published}, but the connection string ` +
          `names ${target} — the API will use whatever answers there. Make ${s.portVariable} in .env and the ` +
          "connection strings agree.",
      );
    }
  }
}

function portAdvice(s) {
  return PORT_ADVICE.replace("{variable}", s.portVariable);
}

/** stdout of `docker compose …` on this project's file, or null when it fails (Docker not running, say). */
function compose(composeArgs) {
  const result = runSync("docker", ["compose", "-f", join(REPO_ROOT, "docker-compose.yml"), ...composeArgs], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
  });
  return result.status === 0 ? String(result.stdout ?? "") : null;
}

function probe(script) {
  return runSync("node", [join(REPO_ROOT, "scripts", script)], { cwd: REPO_ROOT, stdio: "ignore" }).status === 0;
}

/** The `host:port` a probe names when its service answers ("ready host:port"), or "" when it does not. */
function probeOutput(script) {
  const result = runSync("node", [join(REPO_ROOT, "scripts", script)], {
    cwd: REPO_ROOT,
    encoding: "utf8",
    stdio: ["ignore", "pipe", "ignore"],
  });
  return String(result.stdout ?? "").trim().replace(/^ready\s+/, "");
}

function fail(message) {
  console.error(`dev: ${message}`);
  process.exit(1);
}
