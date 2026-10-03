#!/usr/bin/env node
/**
 * Deploy this application to a Linux server through Coolify, from Windows, macOS or Linux.
 *
 *   npm run deploy:vps -- --host 203.0.113.10 --domain app.example.com --email you@example.com
 *   npm run deploy:vps -- --domain app.example.com        # again: the other answers are remembered
 *   npm run deploy:vps -- --host 203.0.113.10 --check     # only report the server's prerequisites
 *
 * Options: --host (server IP or name), --user (SSH user, default root; any other needs sudo),
 * --port (SSH port, default 22), --identity (SSH private key file), --domain (where the app is
 * served), --email (Coolify's administrator account), --repo (default: the origin remote),
 * --branch (default: the base branch), --name (Coolify project/application, default: from the
 * domain), --yes (no confirmations).
 *
 * What it needs here: Node and the OpenSSH client (`ssh`), which Windows 10+, macOS and Linux
 * all ship. On the server: SSH access as root or a sudo user, and the domain's DNS A record
 * pointing at it. Everything else is checked and installed by scripts/deploy/vps/remote.sh, which
 * this uploads and runs in phases:
 *
 *   check    the prerequisites, read-only (what --check runs)
 *   prepare  the missing tools, swap, firewall ports, Coolify itself, an API token, and a
 *            deploy key when the repository is private
 *   deploy   the Coolify project and application, the domain, the environment variables
 *            (secrets generated once, on the server, never overwritten), and the deployment
 *
 * Coolify builds from the Git repository, so it deploys what is pushed to --branch, using
 * docker-compose.coolify.yml. Each domain is its own Coolify project and application, so other
 * apps on the same server are never touched.
 *
 * The answers (never a secret) are saved in .deploy/vps/<domain>.json, which git ignores. Exits 0
 * when the app is deployed (or the check passed), 1 otherwise.
 */
import { spawn, spawnSync } from "node:child_process";
import { lookup, resolveMx } from "node:dns/promises";
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { isIP } from "node:net";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createInterface } from "node:readline/promises";
import {
  appNameFor,
  isValidDomain,
  parseDeployArgs,
  parseResults,
  remoteCommand,
  repoUrls,
  REMOTE_SCRIPT,
} from "./lib/deploy-vps.mjs";
import { hasExecutable, IS_WINDOWS } from "./lib/proc.mjs";
import { BASE_BRANCH, REPO_ROOT } from "./lib/project-config.mjs";
import { sshHostName } from "./lib/remote.mjs";

const COMPOSE_FILE = "docker-compose.coolify.yml";
const SAVED_DIR = join(REPO_ROOT, ".deploy", "vps");
const SAVED_KEYS = ["host", "user", "port", "identity", "domain", "repo", "branch", "name", "email"];

const log = (message = "") => console.log(message);
const fail = (message) => {
  console.error(`deploy: ${message}`);
  process.exit(1);
};

const { options: given, flags, errors } = parseDeployArgs(process.argv.slice(2));
if (flags.help) {
  // The header comment is the help text.
  const source = readFileSync(new URL(import.meta.url), "utf8");
  log(source.slice(source.indexOf("/**") + 3, source.indexOf("*/")).replace(/^ \* ?/gm, "").trim());
  process.exit(0);
}
if (errors.length) fail(`${errors.join("; ")} (npm run deploy:vps -- --help)`);

const rl = process.stdin.isTTY ? createInterface({ input: process.stdin, output: process.stdout }) : null;
async function ask(question, fallback = "") {
  if (!rl || flags.yes) return fallback;
  const answer = (await rl.question(fallback ? `${question} [${fallback}]: ` : `${question}: `)).trim();
  return answer || fallback;
}
async function confirm(question) {
  if (flags.yes) return true;
  if (!rl) return false;
  return /^y(es)?$/i.test((await rl.question(`${question} [y/N]: `)).trim());
}

const git = (...args) => {
  const res = spawnSync("git", args, { cwd: REPO_ROOT, encoding: "utf8", windowsHide: true });
  return res.status === 0 ? res.stdout.trim() : "";
};

/** The answers saved for a domain, or for the only domain saved when none is given. */
function loadSaved(domain) {
  if (!existsSync(SAVED_DIR)) return {};
  const file = domain
    ? join(SAVED_DIR, `${domain}.json`)
    : (() => {
        const files = readdirSync(SAVED_DIR).filter((f) => f.endsWith(".json"));
        return files.length === 1 ? join(SAVED_DIR, files[0]) : null;
      })();
  if (!file || !existsSync(file)) return {};
  try {
    return JSON.parse(readFileSync(file, "utf8"));
  } catch {
    return {};
  }
}

// --- the answers ---------------------------------------------------------------------------

const saved = loadSaved(given.domain);
const opts = { ...saved, ...given };

opts.host ||= await ask("Server IP or host name");
if (!opts.host) fail("--host is required");
// An alias from ~/.ssh/config (Host easyfornet-vps) is resolved the way ssh resolves it, so the DNS
// check and the dashboard URL use the real address.
const serverAddress = sshHostName(opts.host);
opts.user ||= "root";
opts.port ||= "22";
if (!/^\d+$/.test(String(opts.port))) fail(`--port must be a number, not '${opts.port}'`);

if (!flags.check) {
  opts.domain ||= await ask("Domain to serve the app on (e.g. app.example.com)");
  if (!isValidDomain(opts.domain)) fail(`'${opts.domain ?? ""}' is not a domain name (--domain app.example.com)`);
  opts.domain = opts.domain.toLowerCase();
  opts.email ||= await ask("Email for Coolify's administrator account", git("config", "user.email"));
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(opts.email ?? "")) fail("--email is required (Coolify's administrator account)");
  opts.name ||= appNameFor(opts.domain);
  opts.branch ||= BASE_BRANCH;
  opts.repo ||= git("remote", "get-url", "origin");
  if (!opts.repo) fail("no origin remote: push the project to a Git host and pass --repo <url>");
}

const repo = flags.check ? null : repoUrls(opts.repo, sshHostName);
if (!flags.check && !repo) fail(`cannot read the repository URL '${opts.repo}'`);

if (!hasExecutable("ssh")) {
  fail(
    IS_WINDOWS
      ? "ssh not found: install the OpenSSH client (Settings > System > Optional features > OpenSSH Client)"
      : "ssh not found: install the OpenSSH client",
  );
}

if (opts.domain) {
  mkdirSync(SAVED_DIR, { recursive: true });
  const record = Object.fromEntries(SAVED_KEYS.filter((k) => opts[k]).map((k) => [k, opts[k]]));
  writeFileSync(join(SAVED_DIR, `${opts.domain}.json`), `${JSON.stringify(record, null, 2)}\n`);
}

// --- checks on this side -------------------------------------------------------------------

if (!flags.check) {
  log(`Deploying ${repo.https} (${opts.branch}) to https://${opts.domain} on ${opts.user}@${opts.host}`);

  // Coolify clones the branch from the remote, so the compose file has to be pushed there.
  if (git("rev-parse", "--verify", "--quiet", `refs/remotes/origin/${opts.branch}`)
    && !git("rev-parse", "--verify", "--quiet", `origin/${opts.branch}:${COMPOSE_FILE}`)) {
    log(`warning: ${COMPOSE_FILE} is not on origin/${opts.branch} (as of the last fetch): commit and push it, or the build will fail`);
  }

  // Coolify refuses an administrator email whose domain has no mail or address record.
  const emailDomain = opts.email.split("@")[1];
  // The system resolver first: Node's own (c-ares) may be pointed at a resolver that refuses it.
  // Only a domain that is definitely missing counts; a resolver failure is not evidence.
  const exists = await lookup(emailDomain).then(
    () => true,
    (err) => (["ENOTFOUND", "ENODATA"].includes(err.code) ? resolveMx(emailDomain).then((r) => r.length > 0, () => false) : true),
  );
  if (!exists && !(await confirm(`${emailDomain} does not resolve, and Coolify checks it before creating its account. Continue anyway?`))) {
    fail("stopped: use a real email address (--email)");
  }

  // Let's Encrypt only issues the certificate once the domain resolves to this server.
  const addresses = async (name) => (isIP(name) ? [name] : (await lookup(name, { all: true })).map((a) => a.address));
  try {
    const [domainIps, hostIps] = await Promise.all([addresses(opts.domain), addresses(serverAddress)]);
    if (!domainIps.some((ip) => hostIps.includes(ip))) {
      log(`warning: ${opts.domain} resolves to ${domainIps.join(", ")}, not to ${hostIps.join(", ")}.`);
      log("  Point its DNS A record at the server, or the HTTPS certificate cannot be issued.");
      if (!(await confirm("Continue anyway?"))) fail("stopped: fix the DNS record and run again");
    }
  } catch {
    log(`warning: ${opts.domain} does not resolve yet. Add a DNS A record pointing at ${serverAddress}.`);
    if (!(await confirm("Continue anyway?"))) fail("stopped: add the DNS record and run again");
  }
}

// --- the server ----------------------------------------------------------------------------

// ssh, gh and git are spawned directly rather than through lib/proc.mjs: they are native
// executables on every platform (no .cmd shim), and routing the remote command line through
// cmd.exe would rewrite the quoting it has to deliver to the server's shell intact.

const target = `${opts.user}@${opts.host}`;
const sshArgs = [
  "-p", String(opts.port),
  ...(opts.identity ? ["-i", opts.identity] : []),
  // A fresh server's key is trusted on first sight; a changed one is still refused.
  "-o", "StrictHostKeyChecking=accept-new",
  "-o", "ServerAliveInterval=30",
  // One connection for every phase, so a password is asked for once. Windows' OpenSSH has no
  // connection sharing, so there each phase connects on its own.
  ...(IS_WINDOWS ? [] : ["-o", "ControlMaster=auto", "-o", "ControlPath=~/.ssh/efn-deploy-%C", "-o", "ControlPersist=120"]),
];

function upload() {
  // LF only: a Windows checkout may have turned the script's line endings into CRLF.
  const script = readFileSync(join(REPO_ROOT, "scripts", "deploy", "vps", "remote.sh"), "utf8").replace(/\r\n/g, "\n");
  const res = spawnSync("ssh", [...sshArgs, target, `umask 077 && cat > ${REMOTE_SCRIPT}`], {
    input: script,
    stdio: ["pipe", "inherit", "inherit"],
    windowsHide: true,
  });
  if (res.status !== 0) fail(`could not connect to ${target} (ssh exited ${res.status ?? res.error?.message})`);
}

/** Run one server phase in a terminal, so sudo and the token fallback can prompt. */
function runPhase(phase, values) {
  return new Promise((resolve) => {
    const child = spawn("ssh", [...sshArgs, "-tt", target, remoteCommand(phase, values)], {
      stdio: ["inherit", "pipe", "inherit"],
    });
    let output = "";
    child.stdout.on("data", (chunk) => {
      const text = chunk.toString();
      output += text;
      process.stdout.write(text.replace(/^EFN_RESULT .*$/gm, ""));
    });
    child.on("close", (code) => resolve({ code, results: parseResults(output) }));
    child.on("error", (err) => resolve({ code: 1, results: [], error: err }));
  });
}

/** Give the deploy key read access to the repository: with gh when it can, else by hand. */
async function addDeployKey({ publicKey, keyName }) {
  const isGithub = repo.host === "github.com";
  if (isGithub && hasExecutable("gh")) {
    const file = join(tmpdir(), `${keyName}.pub`);
    writeFileSync(file, `${publicKey}\n`);
    const res = spawnSync("gh", ["repo", "deploy-key", "add", file, "--repo", repo.slug, "--title", keyName], {
      encoding: "utf8",
      windowsHide: true,
    });
    const said = `${res.stdout}${res.stderr}`;
    if (res.status === 0 || /already in use/i.test(said)) {
      log(`Deploy key ${keyName} is on ${repo.slug}.`);
      return;
    }
    log(`gh could not add the deploy key (${said.trim()}); add it by hand.`);
  }
  log("");
  log(`The repository is private. Add this read-only deploy key to it${isGithub ? ` (https://github.com/${repo.slug}/settings/keys/new)` : ""}:`);
  log("");
  log(`  ${publicKey}`);
  log("");
  if (!rl) fail("add the deploy key, then run again");
  await rl.question("Press Enter once it is added (or if it was added before)...");
}

upload();

if (flags.check) {
  const { code } = await runPhase("check", { CLEANUP: 1 });
  rl?.close();
  process.exit(code === 0 ? 0 : 1);
}

const prepared = await runPhase("prepare", {
  SSH_PORT: opts.port,
  EMAIL: opts.email,
  HOST: serverAddress,
  REPO_HTTPS: repo.https,
  APP_NAME: opts.name,
});
const source = prepared.results.at(-1);
if (prepared.code !== 0 || !source) fail("preparing the server failed (see above)");

if (source.visibility === "private") await addDeployKey(source);

const deployed = await runPhase("deploy", {
  APP_NAME: opts.name,
  DOMAIN: opts.domain,
  REPO: source.visibility === "private" ? repo.ssh : repo.https,
  BRANCH: opts.branch,
  KEY_UUID: source.keyUuid,
  COMPOSE: `/${COMPOSE_FILE}`,
  CLEANUP: 1,
});
rl?.close();
if (deployed.code !== 0) fail("the deployment failed (see above)");

// The same check from out here proves DNS and the certificate as a visitor sees them.
const url = `https://${opts.domain}`;
try {
  const res = await fetch(`${url}/health`, { signal: AbortSignal.timeout(15_000) });
  log(res.ok ? `\n${url} is live.` : `\nwarning: ${url}/health answered ${res.status} from here.`);
} catch (err) {
  log(`\nwarning: ${url} is not reachable from this machine yet (${err.cause?.code ?? err.message}); DNS may still be spreading.`);
}
log(`Coolify dashboard: http://${serverAddress}:8000`);
