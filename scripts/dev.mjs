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
 *    `docker compose up -d --wait` on docker-compose.yml and waits for both to answer.
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
  if (probe("pg-ready.mjs") && probe("redis-ready.mjs")) return;
  if (NO_DOCKER) fail("PostgreSQL or Redis is not answering, and --no-docker was given. Start them and run again.");
  if (!hasExecutable("docker")) {
    fail("PostgreSQL or Redis is not answering, and docker is not on PATH. Install Docker or start them yourself.");
  }
  console.log("dev: starting PostgreSQL and Redis (docker compose up -d)…");
  const up = runSync("docker", ["compose", "-f", join(REPO_ROOT, "docker-compose.yml"), "up", "-d", "--wait"], {
    cwd: REPO_ROOT,
    stdio: "inherit",
  });
  if (up.status !== 0) fail("docker compose up failed. Is Docker running?");
  // --wait follows the containers' healthchecks; the probes confirm the ports the API uses.
  for (let i = 0; i < 30; i++) {
    if (probe("pg-ready.mjs") && probe("redis-ready.mjs")) return;
    sleepSync(1000);
  }
  fail("PostgreSQL or Redis did not start answering within 30 seconds.");
}

function probe(script) {
  return runSync("node", [join(REPO_ROOT, "scripts", script)], { cwd: REPO_ROOT, stdio: "ignore" }).status === 0;
}

function fail(message) {
  console.error(`dev: ${message}`);
  process.exit(1);
}
