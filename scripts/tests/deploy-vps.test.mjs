import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  appNameFor,
  composeShape,
  isValidDomain,
  parseDeployArgs,
  parseResults,
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
  // Coolify names the stack itself, and its proxy serves whatever domain Traefik routes to it.
  const prodOnly = new Set(["PROD_PROJECT_NAME", "DOMAIN"]);
  assert.deepEqual(coolify.variables, prod.variables.filter((v) => !prodOnly.has(v)));
});

test("the compose shape skips escaped dollars and nested keys", () => {
  const shape = composeShape("services:\n  db:\n    environment:\n      A: ${A:?x}\n    test: $${B}\nvolumes:\n  data:\n");
  assert.deepEqual(shape, { services: ["db"], variables: ["A"] });
});
