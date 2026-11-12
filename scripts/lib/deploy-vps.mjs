/**
 * The pure parts of `npm run deploy:vps`: reading its arguments, naming the Coolify application,
 * turning a git remote into the URLs Coolify clones from, and building the command line each
 * server phase runs under. Nothing here touches the network or the file system.
 */
import { parseRemote } from "./remote.mjs";

/** Options that take a value, and the flags that do not. */
const VALUE_OPTIONS = ["host", "user", "port", "identity", "domain", "repo", "branch", "name", "email", "platform-subdomain", "dns-provider", "dns-token", "godaddy-key", "godaddy-secret"];
const FLAGS = ["check", "yes", "help", "www"];

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
 * The two shapes of deployment. `tenants`: the platform at `<subdomain>.<domain>` and every tenant at
 * `<identifier>.<domain>` (the multi-tenant template). `single`: one site at `<domain>` itself, with
 * `www.<domain>` optionally redirecting to it (the user-facing template).
 */
export const SITE_TENANTS = "tenants";
export const SITE_SINGLE = "single";

/**
 * Which shape a project deploys, read from the compose file Coolify runs: a stack that hands the API
 * no tenant template (`${TENANT_URL_TEMPLATE}`) serves one domain.
 */
export function siteModeOf(composeText) {
  return /(?<!\$)\$\{TENANT_URL_TEMPLATE\b/.test(String(composeText ?? "")) ? SITE_TENANTS : SITE_SINGLE;
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
 * The environment variables each common DNS provider's credentials go in, as Traefik's ACME client
 * (lego) names them. A provider reading several takes their values joined by `:` in that order
 * (godaddy: `KEY:SECRET`). Any other lego provider works too, with the token given as `NAME=value`,
 * several joined by commas.
 */
export const DNS_PROVIDERS = {
  cloudflare: ["CF_DNS_API_TOKEN"],
  digitalocean: ["DO_AUTH_TOKEN"],
  duckdns: ["DUCKDNS_TOKEN"],
  gandiv5: ["GANDIV5_PERSONAL_ACCESS_TOKEN"],
  godaddy: ["GODADDY_API_KEY", "GODADDY_API_SECRET"],
  linode: ["LINODE_TOKEN"],
  vultr: ["VULTR_API_KEY"],
};

/**
 * Settings a provider needs beyond lego's defaults, written beside its credentials unless the token
 * names them. GoDaddy's nameservers publish a record slowly and tell resolvers to remember its
 * absence for ten minutes (the SOA minimum), past lego's two-minute default wait, so the first check
 * caches "no TXT record" and the certificate is never issued.
 */
export const DNS_PROVIDER_SETTINGS = {
  godaddy: { GODADDY_PROPAGATION_TIMEOUT: "1200", GODADDY_POLLING_INTERVAL: "30" },
};

/**
 * `--dns-provider` and `--dns-token` into what Traefik's DNS-01 challenge needs: the provider, the
 * names of the environment variables that hold its credentials, and `env`, the env file's lines
 * that set them. `{}` when neither is given (HTTP-01 only), `{ error }` when only one is, or when the
 * provider's variables are not known and the token does not name them. An error never quotes the
 * token.
 */
export function dnsChallenge(provider, token) {
  if (!provider && !token) return {};
  if (!provider) return { error: "--dns-token needs --dns-provider" };
  if (!token) return { error: "--dns-provider needs a token: EFN_DNS_TOKEN=<token> (or --dns-token)" };
  const name = String(provider).toLowerCase();
  if (!/^[a-z0-9]+$/.test(name)) return { error: `--dns-provider '${provider}' is not a lego provider name (e.g. cloudflare)` };
  let pairs;
  if (/^[A-Z][A-Z0-9_]*=/.test(token)) {
    // NAME=value[,NAME=value...]: a comma starts a new pair only where a variable name follows it.
    pairs = token.split(/,(?=[A-Z][A-Z0-9_]*=)/).map((pair) => [pair.slice(0, pair.indexOf("=")), pair.slice(pair.indexOf("=") + 1)]);
  } else if (DNS_PROVIDERS[name]) {
    const names = DNS_PROVIDERS[name];
    const values = names.length === 1 ? [token] : token.split(":");
    if (values.length !== names.length) {
      return { error: `DNS provider '${name}' takes ${names.length} values, ${names.join(", ")}, as EFN_DNS_TOKEN=${names.map((n) => `<${n}>`).join(":")}` };
    }
    pairs = names.map((n, i) => [n, values[i]]);
  } else {
    return {
      error: `the token variable of DNS provider '${name}' is not known: give the token as VARIABLE=token, naming ` +
        `the variable lego's ${name} provider reads, several joined by commas (known: ${Object.keys(DNS_PROVIDERS).join(", ")})`,
    };
  }
  // The values are written into an env file on the server: nothing a real token holds needs quoting.
  if (pairs.some(([, value]) => !value || /[\s'"`\\$]/.test(value))) {
    return { error: "--dns-token holds an empty value, a space, a quote, a backslash or a $, which no DNS provider token does" };
  }
  for (const [setting, value] of Object.entries(DNS_PROVIDER_SETTINGS[name] ?? {})) {
    if (!pairs.some(([n]) => n === setting)) pairs.push([setting, value]);
  }
  return { provider: name, envNames: pairs.map(([n]) => n), env: pairs.map(([n, v]) => `${n}=${v}`).join("\n") };
}

/**
 * The values of just the named variables in a dotenv file's text (`NAME=value`, `export NAME=value`,
 * optional single or double quotes, `#` comments). Every other line is ignored and never returned,
 * so the deploy reads the two GoDaddy credentials from `.env` and nothing else in it.
 */
export function readDotenvValues(text, names) {
  const wanted = new Set(names);
  const values = {};
  for (const raw of String(text).split(/\r?\n/)) {
    const match = /^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/.exec(raw);
    if (!match || !wanted.has(match[1])) continue;
    let value = match[2];
    const quote = value[0];
    if ((quote === '"' || quote === "'") && value.lastIndexOf(quote) > 0) value = value.slice(1, value.lastIndexOf(quote));
    else value = value.replace(/\s+#.*$/, "");
    values[match[1]] = value;
  }
  return values;
}

/**
 * The DNS zones a domain could be registered as, longest first: `biz.example.co.uk` gives
 * `biz.example.co.uk`, `example.co.uk`, `co.uk`. GoDaddy is asked about each in turn; the first one
 * the account owns is the zone its records are written in. A bare top-level label is never a
 * candidate.
 */
export function zoneCandidates(domain) {
  const labels = String(domain).toLowerCase().split(".");
  const zones = [];
  for (let i = 0; i < labels.length - 1; i++) zones.push(labels.slice(i).join("."));
  return zones;
}

/**
 * The name of the wildcard record inside its zone: `*` for the zone itself (`example.com` in
 * `example.com`), `*.biz` for a domain below it (`biz.example.com`). The domain's own record is
 * never written: the bare domain is left for whatever else is served there.
 */
export function wildcardRecordName(domain, zone) {
  const d = String(domain).toLowerCase();
  const z = String(zone).toLowerCase();
  if (d === z) return "*";
  if (!d.endsWith(`.${z}`)) throw new Error(`${d} is not inside the zone ${z}`);
  return `*.${d.slice(0, -(z.length + 1))}`;
}

/**
 * The GoDaddy API credentials from, in order, the flags, the environment and the values read from
 * `.env`; the key and secret are one pair, so a lone half is an `error`. `{}` when none is given.
 * When `dnsProvider` is godaddy and `dnsToken` is `KEY:SECRET`, that token is the last source.
 */
export function godaddyCredentials({ given = {}, env = {}, dotenv = {}, dnsProvider, dnsToken } = {}) {
  let key = given["godaddy-key"] || env.GODADDY_API_KEY || dotenv.GODADDY_API_KEY || "";
  let secret = given["godaddy-secret"] || env.GODADDY_API_SECRET || dotenv.GODADDY_API_SECRET || "";
  if (!key && !secret && String(dnsProvider ?? "").toLowerCase() === "godaddy" && /^[^:=,\s]+:[^:\s]+$/.test(dnsToken ?? "")) {
    [key, secret] = dnsToken.split(":");
  }
  if (Boolean(key) !== Boolean(secret)) return { error: "pass both --godaddy-key and --godaddy-secret, or neither" };
  return key ? { key, secret } : {};
}

/** A label nobody creates a record for, so that it resolves only through a wildcard record. */
export function randomLabel(random = Math.random) {
  return `efn-check-${Math.floor(random() * 36 ** 8).toString(36).padStart(8, "0")}`;
}

/**
 * What DNS says about a deployment's records. In the `tenants` shape, two: the platform's host
 * (`app.<domain>` unless `subdomain` names another), and `*.<domain>` probed through a random label.
 * In the `single` shape, `<domain>` itself, and `www.<domain>` when `www` is set.
 * `resolve(name)` gives a name's addresses (or throws when it has none). Returns
 * `{ missing, elsewhere }`, each a list of `{ record, name, addresses? }` in that order: `missing`
 * stops a deploy, `elsewhere` (resolving, but not to the server) is a warning.
 */
export async function checkDomainDns({ domain, subdomain, serverAddresses, resolve, label = randomLabel(), mode = SITE_TENANTS, www = false }) {
  const platform = platformHost(domain, subdomain);
  const site = String(domain).toLowerCase();
  const records = mode === SITE_SINGLE
    ? [{ record: site, name: site }, ...(www ? [{ record: `www.${site}`, name: `www.${site}` }] : [])]
    : [
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
