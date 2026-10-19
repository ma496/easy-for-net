import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  appNameFor,
  checkDomainDns,
  composeShape,
  dnsChallenge,
  isValidDomain,
  isValidLabel,
  parseDeployArgs,
  parseResults,
  platformHost,
  randomLabel,
  remoteCommand,
  repoUrls,
  shellQuote,
} from "../lib/deploy-vps.mjs";
import { REPO_ROOT } from "../lib/project-config.mjs";

// --- arguments -----------------------------------------------------------------------------

test("options take a value in either form, and flags take none", () => {
  const { options, flags, errors } = parseDeployArgs(["--host", "203.0.113.10", "--domain=app.example.com", "--check"]);
  assert.deepEqual(options, { host: "203.0.113.10", domain: "app.example.com" });
  assert.deepEqual(flags, { check: true });
  assert.deepEqual(errors, []);
});

test("a misspelt option is an error, never silently ignored", () => {
  assert.deepEqual(parseDeployArgs(["--domian", "x.com"]).errors, ["unknown option: --domian", "unexpected argument: x.com"]);
});

test("an option missing its value does not swallow the next option", () => {
  const { options, flags, errors } = parseDeployArgs(["--host", "--check"]);
  assert.equal(options.host, undefined);
  assert.equal(flags.check, true);
  assert.deepEqual(errors, ["--host needs a value"]);
  assert.deepEqual(parseDeployArgs(["--domain"]).errors, ["--domain needs a value"]);
});

// --- names ---------------------------------------------------------------------------------

test("a domain is a dotted host name a certificate can be issued for", () => {
  assert.equal(isValidDomain("demo.easyfornet.com"), true);
  assert.equal(isValidDomain("easyfornet.com"), true);
  assert.equal(isValidDomain("localhost"), false);
  assert.equal(isValidDomain("203.0.113.10"), false);
  assert.equal(isValidDomain("https://app.example.com"), false);
  assert.equal(isValidDomain("-bad.example.com"), false);
  assert.equal(isValidDomain(undefined), false);
});

test("each domain gets its own application name, so apps on one server never collide", () => {
  assert.equal(appNameFor("demo.easyfornet.com"), "demo-easyfornet-com");
  assert.equal(appNameFor("easyfornet.com"), "easyfornet-com");
  assert.equal(appNameFor("Shop.Example.co.uk"), "shop-example-co-uk");
});

// --- repositories --------------------------------------------------------------------------

test("an SSH alias remote resolves to the real host's HTTPS and SSH URLs", () => {
  const urls = repoUrls("git@github-personal:ma496/easy-for-net.git", (alias) => (alias === "github-personal" ? "github.com" : alias));
  assert.deepEqual(urls, {
    host: "github.com",
    slug: "ma496/easy-for-net",
    https: "https://github.com/ma496/easy-for-net",
    ssh: "git@github.com:ma496/easy-for-net.git",
  });
});

test("HTTPS and ssh:// remotes give the same pair, and a real host is not re-resolved", () => {
  const never = () => assert.fail("a host with a dot is not an alias");
  for (const remote of ["https://github.com/acme/shop.git", "https://github.com/acme/shop", "ssh://git@github.com/acme/shop.git"]) {
    assert.equal(repoUrls(remote, never).https, "https://github.com/acme/shop");
    assert.equal(repoUrls(remote, never).ssh, "git@github.com:acme/shop.git");
  }
  assert.equal(repoUrls("not a url"), null);
});

// --- the remote command line ---------------------------------------------------------------

test("shell quoting survives spaces, quotes and shell syntax", () => {
  assert.equal(shellQuote("plain"), "'plain'");
  assert.equal(shellQuote("it's $(here)"), `'it'\\''s $(here)'`);
});

test("a phase runs as root directly, through sudo otherwise, with empty values left out", () => {
  const command = remoteCommand("deploy", { APP_NAME: "demo-easyfornet-com", KEY_UUID: undefined, BRANCH: "", CLEANUP: 1 });
  assert.equal(
    command,
    `if [ "$(id -u)" -eq 0 ]; then sh .efn-deploy-vps.sh deploy 'APP_NAME=demo-easyfornet-com' 'CLEANUP=1'; ` +
      `else sudo sh .efn-deploy-vps.sh deploy 'APP_NAME=demo-easyfornet-com' 'CLEANUP=1'; fi`,
  );
});

test("results are read from their marker lines, across terminal line endings", () => {
  const output = 'Checking...\r\nEFN_RESULT {"visibility":"public"}\r\nmentions EFN_RESULT in prose\r\nEFN_RESULT {"url":"https://x.com"}\r\n';
  assert.deepEqual(parseResults(output), [{ visibility: "public" }, { url: "https://x.com" }]);
});

// --- the compose files ---------------------------------------------------------------------

test("the Coolify stack runs the same services and reads the same values as the production one", () => {
  const read = (file) => composeShape(readFileSync(join(REPO_ROOT, file), "utf8"));
  const prod = read("docker-compose.prod.yml");
  const coolify = read("docker-compose.coolify.yml");
  assert.deepEqual(coolify.services, prod.services);
  // Coolify names the stack itself, and its proxy serves whatever host Traefik routes to it.
  const prodOnly = new Set(["PROD_PROJECT_NAME", "DOMAIN", "PLATFORM_SUBDOMAIN"]);
  // The Traefik service the tenants' router sends tenant hosts to (Caddy routes them in production).
  const coolifyOnly = new Set(["TENANTS_SERVICE"]);
  assert.deepEqual(coolify.variables.filter((v) => !coolifyOnly.has(v)), prod.variables.filter((v) => !prodOnly.has(v)));
});

test("the tenants' router reaches the proxy through the service its label defines, never a container name", () => {
  const compose = readFileSync(join(REPO_ROOT, "docker-compose.coolify.yml"), "utf8");
  assert.match(compose, /^ {6}- traefik\.http\.services\.\$\{TENANTS_SERVICE:\?set TENANTS_SERVICE\}\.loadbalancer\.server\.port=80$/m);
  const script = readFileSync(join(REPO_ROOT, "scripts", "deploy", "vps", "remote.sh"), "utf8").replace(/\r\n/g, "\n");
  assert.match(script, /tenants_service\(\) \{ printf 'efn-%s-tenants' "\$1"; \}/);
  assert.match(script, /set_env TENANTS_SERVICE "\$\(tenants_service "\$app"\)"/);
  assert.match(script, /service="\$\(tenants_service "\$1"\)@docker"/);
  // Coolify escapes $ in labels unless told not to, which would leave the variable unfilled.
  assert.match(script, /'\{"is_container_label_escape_enabled": false\}'/);
  // Coolify suffixes the container name with each deployment's timestamp.
  assert.doesNotMatch(script, /http:\/\/proxy-/);
});

test("the compose shape skips escaped dollars and nested keys", () => {
  const shape = composeShape("services:\n  db:\n    environment:\n      A: ${A:?x}\n    test: $${B}\nvolumes:\n  data:\n");
  assert.deepEqual(shape, { services: ["db"], variables: ["A"] });
});

test("both compose files hand the API the tenant template beside the platform origin", () => {
  for (const file of ["docker-compose.prod.yml", "docker-compose.coolify.yml"]) {
    const text = readFileSync(join(REPO_ROOT, file), "utf8");
    assert.match(text, /^ {6}Web__DefaultDomain: \$\{PUBLIC_URL:\?set PUBLIC_URL\}$/m, file);
    assert.match(text, /^ {6}Web__TenantOriginTemplate: \$\{TENANT_URL_TEMPLATE:\?set TENANT_URL_TEMPLATE\}$/m, file);
  }
});

test(".env.docker.example fills every value the production stack requires", () => {
  const example = readFileSync(join(REPO_ROOT, ".env.docker.example"), "utf8");
  const values = Object.fromEntries([...example.matchAll(/^([A-Z_][A-Z0-9_]*)=(.*)$/gm)].map((m) => [m[1], m[2].trim()]));
  const prod = readFileSync(join(REPO_ROOT, "docker-compose.prod.yml"), "utf8");
  for (const [, name] of prod.matchAll(/\$\{([A-Z_][A-Z0-9_]*):\?/g)) assert.ok(name in values, `${name} is missing`);
  assert.equal(values.DOMAIN, "example.com");
  assert.equal(values.PLATFORM_SUBDOMAIN, "app");
  assert.equal(values.PUBLIC_URL, `https://${platformHost(values.DOMAIN, values.PLATFORM_SUBDOMAIN)}`);
  assert.equal(values.TENANT_URL_TEMPLATE, `https://{tenant}.${values.DOMAIN}`);
});

// --- the Caddyfile -------------------------------------------------------------------------

test("Caddy serves the platform and every tenant subdomain with the same routes", () => {
  const caddyfile = readFileSync(join(REPO_ROOT, "docker", "Caddyfile"), "utf8").replace(/\r\n/g, "\n");
  // Top-level blocks: a name (none for the global options; one may open with a `{$VAR}` placeholder)
  // at column 0 and `{`, up to `}` at column 0.
  const blocks = Object.fromEntries([...caddyfile.matchAll(/^((?:\{\$|[^\s{#])[^\n]*?)?\s*\{\n([\s\S]*?)^\}/gm)].map((m) => [m[1] ?? "", m[2]]));
  // The platform's site is named by PLATFORM_SUBDOMAIN, app when the environment leaves it unset.
  assert.ok(blocks["{$PLATFORM_SUBDOMAIN:app}.{$DOMAIN}"], "the platform's site block");
  assert.match(blocks[""] ?? "", /on_demand_tls \{\s*ask http:\/\/api:8080\/api\/tenants\/tls-allowed\s*\}/);
  const routes = blocks["(app_routes)"];
  assert.match(routes, /@api path \/api\/\* \/hubs\/\* \/hangfire \/hangfire\/\* \/health\n\s*handle @api \{\s*reverse_proxy api:8080\s*\}/);
  assert.match(routes, /handle \{\s*reverse_proxy web:3000\s*\}/);
  assert.match(blocks["{$PLATFORM_SUBDOMAIN:app}.{$DOMAIN}"], /import app_routes/);
  assert.doesNotMatch(blocks["{$PLATFORM_SUBDOMAIN:app}.{$DOMAIN}"], /on_demand/);
  assert.match(blocks["*.{$DOMAIN}"], /tls \{\s*on_demand\s*\}/);
  assert.match(blocks["*.{$DOMAIN}"], /import app_routes/);
});

test("the Coolify Caddyfile answers whatever host Traefik forwards", () => {
  const caddyfile = readFileSync(join(REPO_ROOT, "docker", "Caddyfile.coolify"), "utf8");
  assert.match(caddyfile, /^:80 \{/m);
});

test("the server script attaches the platform host and the wildcard, and sets the tenant template", () => {
  const script = readFileSync(join(REPO_ROOT, "scripts", "deploy", "vps", "remote.sh"), "utf8").replace(/\r\n/g, "\n");
  assert.match(script, /platform_subdomain=\$\{platform_subdomain:-app\}/);
  assert.match(script, /platform_host="\$platform_subdomain\.\$domain"\n\s*url="https:\/\/\$platform_host"/);
  assert.match(script, /--resolve "\$platform_host:443:127\.0\.0\.1"/);
  assert.doesNotMatch(script, /app\.\$domain/);
  assert.match(script, /\$url:80,https:\/\/\*\.\$domain:80/);
  assert.match(script, /set_env TENANT_URL_TEMPLATE "\$tenant_template"/);
  assert.match(script, /tenant_template="https:\/\/\{tenant\}\.\$domain"/);
  // The token file is deleted as soon as it is read.
  assert.match(script, /dns_token=\$\(cat "\$token_file"[^\n]*\n\s*rm -f "\$token_file"/);
  // Every line that reads the token sets it, checks it, or hands it to dns_challenge: none prints it.
  for (const line of script.split("\n").filter((l) => /\$\{?dns_token\b/.test(l))) {
    assert.match(line, /^\s*(dns_token=|if \[ -n "\$dns_provider" \] && \{|if \[ -n "\$dns_provider" \]; then dns_challenge )/, line);
  }
  // Inside dns_challenge the token ($3) is only written into the root-only env file.
  const start = script.indexOf("dns_challenge() {");
  const challenge = script.slice(start, script.indexOf("\n}\n", start));
  assert.deepEqual(challenge.split("\n").filter((l) => l.includes('"$3"')), [String.raw`  printf '%s=%s\n' "$2" "$3" >"$env_file"`]);
});

// --- tenant subdomains and DNS -------------------------------------------------------------

test("the DNS flags take values and are known options", () => {
  const { options, errors } = parseDeployArgs(["--dns-provider", "cloudflare", "--dns-token=abc123"]);
  assert.deepEqual(options, { "dns-provider": "cloudflare", "dns-token": "abc123" });
  assert.deepEqual(errors, []);
  assert.deepEqual(parseDeployArgs(["--dns-token"]).errors, ["--dns-token needs a value"]);
});

test("the platform is app.<domain> unless another subdomain is named", () => {
  assert.equal(platformHost("Example.com"), "app.example.com");
  assert.equal(platformHost("easyfornet.com", "Demo"), "demo.easyfornet.com");
  assert.equal(platformHost("example.com", undefined), "app.example.com");
});

test("--platform-subdomain takes a value and must be one DNS label", () => {
  const { options, errors } = parseDeployArgs(["--domain", "easyfornet.com", "--platform-subdomain=demo"]);
  assert.deepEqual(options, { domain: "easyfornet.com", "platform-subdomain": "demo" });
  assert.deepEqual(errors, []);
  assert.deepEqual(parseDeployArgs(["--platform-subdomain"]).errors, ["--platform-subdomain needs a value"]);
  for (const label of ["app", "demo", "Demo", "my-app", "a", "x1"]) assert.ok(isValidLabel(label), label);
  for (const label of ["", "a.b", "-app", "app-", "my_app", "*", "a".repeat(64), undefined]) assert.ok(!isValidLabel(label), String(label));
});

test("no DNS flags means HTTP-01 only, and one without the other is an error", () => {
  assert.deepEqual(dnsChallenge(undefined, undefined), {});
  assert.match(dnsChallenge("cloudflare", undefined).error, /--dns-provider needs a token: EFN_DNS_TOKEN/);
  assert.match(dnsChallenge(undefined, "secret-token").error, /--dns-token needs --dns-provider/);
});

test("a known provider puts the token in its lego variable", () => {
  assert.deepEqual(dnsChallenge("Cloudflare", "abc123"), { provider: "cloudflare", envName: "CF_DNS_API_TOKEN", value: "abc123" });
  assert.deepEqual(dnsChallenge("digitalocean", "dop_v1_x"), { provider: "digitalocean", envName: "DO_AUTH_TOKEN", value: "dop_v1_x" });
});

test("any other provider names its variable in the token, and an error never quotes the token", () => {
  assert.deepEqual(dnsChallenge("ovh", "OVH_APPLICATION_KEY=k=1"), { provider: "ovh", envName: "OVH_APPLICATION_KEY", value: "k=1" });
  const unknown = dnsChallenge("route53", "super-secret-value");
  assert.match(unknown.error, /VARIABLE=token/);
  assert.doesNotMatch(unknown.error, /super-secret-value/);
  assert.match(dnsChallenge("cloud flare", "x").error, /not a lego provider name/);
  const quoted = dnsChallenge("cloudflare", "abc'; rm -rf /");
  assert.match(quoted.error, /space, a quote/);
  assert.doesNotMatch(quoted.error, /rm -rf/);
});

test("a random label is a valid DNS label that differs between runs", () => {
  assert.match(randomLabel(), /^efn-check-[a-z0-9]{8}$/);
  assert.equal(randomLabel(() => 0), "efn-check-00000000");
  assert.notEqual(randomLabel(() => 0.1), randomLabel(() => 0.2));
});

test("the DNS check probes app.<domain> and the wildcard through a random label", async () => {
  const asked = [];
  const records = { "app.example.com": ["203.0.113.10"], "efn-check-abc.example.com": ["203.0.113.10"] };
  const resolve = async (name) => {
    asked.push(name);
    if (!records[name]) throw Object.assign(new Error("not found"), { code: "ENOTFOUND" });
    return records[name];
  };
  const result = await checkDomainDns({ domain: "example.com", serverAddresses: ["203.0.113.10"], resolve, label: "efn-check-abc" });
  assert.deepEqual(result, { missing: [], elsewhere: [] });
  assert.deepEqual(asked.sort(), ["app.example.com", "efn-check-abc.example.com"]);
});

test("the DNS check probes the platform subdomain it is given", async () => {
  const asked = [];
  const resolve = async (name) => {
    asked.push(name);
    return ["203.0.113.10"];
  };
  const result = await checkDomainDns({ domain: "easyfornet.com", subdomain: "demo", serverAddresses: ["203.0.113.10"], resolve, label: "efn-check-abc" });
  assert.deepEqual(result, { missing: [], elsewhere: [] });
  assert.deepEqual(asked.sort(), ["demo.easyfornet.com", "efn-check-abc.easyfornet.com"]);
});

test("a missing wildcard record is reported as missing, so the deploy refuses", async () => {
  const resolve = async (name) => {
    if (name === "app.example.com") return ["203.0.113.10"];
    throw Object.assign(new Error("not found"), { code: "ENOTFOUND" });
  };
  const { missing, elsewhere } = await checkDomainDns({ domain: "example.com", serverAddresses: ["203.0.113.10"], resolve, label: "efn-check-x" });
  assert.deepEqual(missing, [{ record: "*.example.com", name: "efn-check-x.example.com" }]);
  assert.deepEqual(elsewhere, []);
});

test("records pointing at another address are a warning, not a refusal", async () => {
  const resolve = async () => ["198.51.100.7"];
  const { missing, elsewhere } = await checkDomainDns({ domain: "example.com", serverAddresses: ["203.0.113.10"], resolve, label: "efn-check-x" });
  assert.deepEqual(missing, []);
  assert.deepEqual(elsewhere.map((e) => e.record), ["app.example.com", "*.example.com"]);
  assert.deepEqual(elsewhere[1].addresses, ["198.51.100.7"]);
});
