import test from "node:test";
import assert from "node:assert/strict";
import net from "node:net";
import { isPortFree, knownHolderHint, nextFreePort, parseDevPortLines, readDevPorts, webOriginOverrides } from "../lib/dev-ports.mjs";

// --- reading the ports ---------------------------------------------------------------------

test("the defaults apply when neither the environment nor .env names a port", () => {
  const ports = readDevPorts({ env: {}, envFile: null });
  assert.deepEqual([ports.proxy, ports.web, ports.api], [3000, 3001, 5000]);
  assert.deepEqual(ports.sources, { proxy: "default", web: "default", api: "default" });
});

test("the environment wins over .env, and .env over the default", () => {
  const ports = readDevPorts({ env: { DEV_PROXY_PORT: "3100" }, envFile: "DEV_PROXY_PORT=3200\nDEV_API_PORT=5100\n" });
  assert.deepEqual([ports.proxy, ports.web, ports.api], [3100, 3001, 5100]);
  assert.deepEqual(ports.sources, { proxy: "environment", web: "default", api: ".env" });
});

test("an empty value counts as unset", () => {
  const ports = readDevPorts({ env: { DEV_API_PORT: "" }, envFile: "DEV_WEB_PORT=\n" });
  assert.deepEqual([ports.web, ports.api], [3001, 5000]);
});

test("only the dev-port variables are read from .env, quoted or commented", () => {
  const text = "JWT_KEY=secret\nDEV_PROXY_PORT='3100'\nDEV_WEB_PORT=3101 # next dev\n# DEV_API_PORT=1\r\nPOSTGRES_PASSWORD=x";
  assert.deepEqual(parseDevPortLines(text), { DEV_PROXY_PORT: "3100", DEV_WEB_PORT: "3101" });
});

test("a value that is not a port is refused, naming the variable", () => {
  for (const value of ["abc", "0", "70000", "30.5"]) {
    assert.throws(() => readDevPorts({ env: { DEV_WEB_PORT: value }, envFile: null }), /DEV_WEB_PORT/);
  }
});

test("two ports set to the same number are refused", () => {
  assert.throws(() => readDevPorts({ env: { DEV_API_PORT: "3000" }, envFile: null }), /DEV_PROXY_PORT and DEV_API_PORT/);
});

// --- the API's Web origins -----------------------------------------------------------------

test("localhost origins are moved to the proxy's port, every Domains entry passed", () => {
  const web = {
    DefaultDomain: "http://app.localhost:3000",
    Domains: ["http://localhost:3000", "https://example.com"],
    TenantOriginTemplate: "http://{tenant}.localhost:3000",
  };
  assert.deepEqual(webOriginOverrides(web, 3100), {
    Web__DefaultDomain: "http://app.localhost:3100",
    Web__TenantOriginTemplate: "http://{tenant}.localhost:3100",
    Web__Domains__0: "http://localhost:3100",
    Web__Domains__1: "https://example.com",
  });
});

test("nothing is overridden when the origins already name the proxy's port", () => {
  assert.deepEqual(webOriginOverrides({ DefaultDomain: "http://localhost:3000", Domains: [] }, 3000), {});
  assert.deepEqual(webOriginOverrides(undefined, 3000), {});
});

test("an origin that is not localhost is left as configured", () => {
  const web = { DefaultDomain: "https://app.example.com", TenantOriginTemplate: "http://{tenant}.localhost.example.com:3000" };
  assert.deepEqual(webOriginOverrides(web, 3100), {});
});

test("a localhost origin with no port gets the proxy's", () => {
  assert.deepEqual(webOriginOverrides({ DefaultDomain: "http://localhost/" }, 3100), { Web__DefaultDomain: "http://localhost:3100/" });
});

// --- whether a port is free ----------------------------------------------------------------

/** Listen on `host` with an OS-chosen port and resolve with the server and its port. */
function listen(host) {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", reject);
    server.listen(0, host, () => resolve({ server, port: server.address().port }));
  });
}

test("a port another process holds on the wildcard address is not free", async () => {
  // The case that hid another app: a loopback bind would succeed beside this on Windows and macOS.
  const { server, port } = await listen("0.0.0.0");
  try {
    assert.equal(await isPortFree(port), false);
  } finally {
    server.close();
  }
});

test("a port held on loopback alone is not free", async () => {
  const { server, port } = await listen("127.0.0.1");
  try {
    assert.equal(await isPortFree(port), false);
  } finally {
    server.close();
  }
});

test("a port nothing holds is free, and the next free port skips the taken ones", async () => {
  const { server, port } = await listen("127.0.0.1");
  await new Promise((resolve) => server.close(resolve));
  assert.equal(await isPortFree(port), true);
  const next = await nextFreePort(port, { taken: [port + 1] });
  assert.ok(next !== null && next !== port + 1 && next > port);
});

test("the AirPlay Receiver is named for 5000 on macOS only", () => {
  assert.match(knownHolderHint(5000, "darwin"), /AirPlay/);
  assert.equal(knownHolderHint(5000, "win32"), "");
  assert.equal(knownHolderHint(3000, "darwin"), "");
});
