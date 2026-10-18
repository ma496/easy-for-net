/**
 * The pure parts of `npm run deploy:vps`: reading its arguments, naming the Coolify application,
 * turning a git remote into the URLs Coolify clones from, and building the command line each
 * server phase runs under. Nothing here touches the network or the file system.
 */
import { parseRemote } from "./remote.mjs";

/** Options that take a value, and the flags that do not. */
const VALUE_OPTIONS = ["host", "user", "port", "identity", "domain", "repo", "branch", "name", "email", "platform-subdomain", "dns-provider", "dns-token"];
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

/** The platform's subdomain when none is given: the platform at app.<domain>. */
export const DEFAULT_PLATFORM_SUBDOMAIN = "app";

/** Whether a string is one DNS label, which is all the platform's subdomain may be. */
export function isValidLabel(label) {
  return /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/i.test(String(label ?? ""));
}

/**
 * The host the platform is served on, `<subdomain>.<domain>` (app.<domain> by default); every other
 * subdomain of the domain is a tenant's.
 */
export function platformHost(domain, subdomain = DEFAULT_PLATFORM_SUBDOMAIN) {
  return `${String(subdomain).toLowerCase()}.${String(domain).toLowerCase()}`;
}

/**
 * The environment variable each common DNS provider's API token goes in, as Traefik's ACME client
 * (lego) names it. Any other lego provider works too, with the token given as `NAME=value`.
 */
export const DNS_PROVIDERS = {
  cloudflare: "CF_DNS_API_TOKEN",
  digitalocean: "DO_AUTH_TOKEN",
  duckdns: "DUCKDNS_TOKEN",
  gandiv5: "GANDIV5_PERSONAL_ACCESS_TOKEN",
  linode: "LINODE_TOKEN",
  vultr: "VULTR_API_KEY",
};

/**
 * `--dns-provider` and `--dns-token` into what Traefik's DNS-01 challenge needs: the provider and
 * the one environment variable that holds the token. `{}` when neither is given (HTTP-01 only),
 * `{ error }` when only one is, or when the provider's variable is not known and the token does not
 * name it. An error never quotes the token.
 */
export function dnsChallenge(provider, token) {
  if (!provider && !token) return {};
  if (!provider) return { error: "--dns-token needs --dns-provider" };
  if (!token) return { error: "--dns-provider needs a token: EFN_DNS_TOKEN=<token> (or --dns-token)" };
  const name = String(provider).toLowerCase();
  if (!/^[a-z0-9]+$/.test(name)) return { error: `--dns-provider '${provider}' is not a lego provider name (e.g. cloudflare)` };
  const named = /^([A-Z][A-Z0-9_]*)=(.+)$/s.exec(token);
  const envName = named ? named[1] : DNS_PROVIDERS[name];
  const value = named ? named[2] : token;
  if (!envName) {
    return {
      error: `the token variable of DNS provider '${name}' is not known: give the token as VARIABLE=token, naming ` +
        `the variable lego's ${name} provider reads (known: ${Object.keys(DNS_PROVIDERS).join(", ")})`,
    };
  }
  // The token is written into an env file on the server: nothing a real token holds needs quoting.
  if (/[\s'"`\\$]/.test(value)) return { error: "--dns-token holds a space, a quote, a backslash or a $, which no DNS provider token does" };
  return { provider: name, envName, value };
}

/** A label nobody creates a record for, so that it resolves only through a wildcard record. */
export function randomLabel(random = Math.random) {
  return `efn-check-${Math.floor(random() * 36 ** 8).toString(36).padStart(8, "0")}`;
}

/**
 * What DNS says about a deployment's two records: the platform's host (`app.<domain>` unless
 * `subdomain` names another), and `*.<domain>` probed through a random label. `resolve(name)` gives a name's addresses (or throws when it has none). Returns
 * `{ missing, elsewhere }`, each a list of `{ record, name, addresses? }` in that order: `missing`
 * stops a deploy, `elsewhere` (resolving, but not to the server) is a warning.
 */
export async function checkDomainDns({ domain, subdomain, serverAddresses, resolve, label = randomLabel() }) {
  const platform = platformHost(domain, subdomain);
  const records = [
    { record: platform, name: platform },
    { record: `*.${domain}`, name: `${label}.${domain}` },
  ];
  const answers = await Promise.all(records.map((r) => Promise.resolve().then(() => resolve(r.name)).catch(() => [])));
  const missing = [];
  const elsewhere = [];
  records.forEach((r, i) => {
    const addresses = answers[i] ?? [];
    if (!addresses.length) missing.push(r);
    else if (!addresses.some((ip) => serverAddresses.includes(ip))) elsewhere.push({ ...r, addresses });
  });
  return { missing, elsewhere };
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

/** The DNS provider's token on its way to the server, beside the script; the deploy phase deletes it. */
export const REMOTE_DNS_TOKEN = ".efn-dns-token";

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
