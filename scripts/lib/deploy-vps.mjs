/**
 * The pure parts of `npm run deploy:vps`: reading its arguments, naming the Coolify application,
 * turning a git remote into the URLs Coolify clones from, and building the command line each
 * server phase runs under. Nothing here touches the network or the file system.
 */
import { parseRemote } from "./remote.mjs";

/** Options that take a value, and the flags that do not. */
const VALUE_OPTIONS = ["host", "user", "port", "identity", "domain", "repo", "branch", "name", "email"];
const FLAGS = ["check", "yes", "help"];

/**
 * `--key value`, `--key=value` and bare flags into `{ options, flags, errors }`. An unknown
 * option is an error rather than ignored: a typo in `--domian` must not deploy to a default.
 */
export function parseDeployArgs(argv) {
  const options = {};
  const flags = {};
  const errors = [];
  for (let i = 0; i < argv.length; i++) {
    const raw = argv[i];
    const match = /^--([a-z-]+)(?:=(.*))?$/.exec(raw);
    if (!match) {
      errors.push(`unexpected argument: ${raw}`);
      continue;
    }
    const [, name, inline] = match;
    if (FLAGS.includes(name)) flags[name] = true;
    else if (VALUE_OPTIONS.includes(name)) {
      // `--host --check` is a missing value, not a host called "--check".
      const next = argv[i + 1];
      const value = inline ?? (next !== undefined && !next.startsWith("--") ? argv[++i] : undefined);
      if (value === undefined || value === "") errors.push(`--${name} needs a value`);
      else options[name] = value;
    } else errors.push(`unknown option: --${name}`);
  }
  if (argv.includes("-h")) flags.help = true;
  return { options, flags, errors };
}

/** Whether a string is a host name a certificate can be issued for. */
export function isValidDomain(domain) {
  const value = String(domain ?? "");
  return (
    value.length <= 253 &&
    /^(?=.{1,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$/i.test(value)
  );
}

/**
 * The Coolify project and application name for a domain: `demo.easyfornet.com` becomes
 * `demo-easyfornet-com`. One name per domain is what lets several apps share one Coolify
 * server without a deploy for one ever finding, or changing, another.
 */
export function appNameFor(domain) {
  return String(domain).toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
}

/**
 * The two URLs Coolify can clone a repository from: HTTPS for a public one, SSH for a private
 * one read with a deploy key. `resolveHost` turns an SSH alias (`git@github-work:…`) into the
 * real host; it defaults to leaving the host as written.
 */
export function repoUrls(remote, resolveHost = (host) => host) {
  const parsed = parseRemote(remote);
  if (!parsed) return null;
  const host = parsed.host.includes(".") ? parsed.host : resolveHost(parsed.host);
  const slug = parsed.slug.replace(/\.git$/, "");
  return { host, slug, https: `https://${host}/${slug}`, ssh: `git@${host}:${slug}.git` };
}

/** One argument quoted for a POSIX shell: single quotes, with any single quote closed and escaped. */
export function shellQuote(value) {
  return `'${String(value).replace(/'/g, `'\\''`)}'`;
}

/** The uploaded server script, in the SSH user's home directory. */
export const REMOTE_SCRIPT = ".efn-deploy-vps.sh";

/**
 * The command a server phase runs: the script as root, through sudo when the SSH user is not
 * root. `values` become KEY=value arguments; empty ones are left out.
 */
export function remoteCommand(phase, values = {}) {
  const args = [phase, ...Object.entries(values)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => shellQuote(`${k}=${v}`))].join(" ");
  return `if [ "$(id -u)" -eq 0 ]; then sh ${REMOTE_SCRIPT} ${args}; else sudo sh ${REMOTE_SCRIPT} ${args}; fi`;
}

/** The JSON objects the server script reports on its `EFN_RESULT` lines, in order. */
export function parseResults(output) {
  const results = [];
  for (const line of String(output).split(/\r?\n/)) {
    const at = line.indexOf("EFN_RESULT ");
    if (at < 0) continue;
    try {
      results.push(JSON.parse(line.slice(at + "EFN_RESULT ".length).trim()));
    } catch {
      // A line that merely mentions the marker is not a result.
    }
  }
  return results;
}

/** The services a compose file declares, and every `${VAR}` it reads, for comparing two files. */
export function composeShape(text) {
  const services = [];
  let inServices = false;
  for (const line of String(text).split(/\r?\n/)) {
    if (/^\S/.test(line)) inServices = /^services:\s*$/.test(line);
    else if (inServices) {
      const service = /^ {2}([A-Za-z0-9_-]+):\s*$/.exec(line);
      if (service) services.push(service[1]);
    }
  }
  const variables = new Set();
  // `$${VAR}` is an escaped dollar for the container's own shell, not a Compose variable.
  for (const m of String(text).matchAll(/(?<!\$)\$\{([A-Za-z_][A-Za-z0-9_]*)/g)) variables.add(m[1]);
  return { services: services.sort(), variables: [...variables].sort() };
}
