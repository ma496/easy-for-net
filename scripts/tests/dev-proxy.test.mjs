import test from "node:test";
import assert from "node:assert/strict";
import http from "node:http";
import net from "node:net";
import { createDevProxy, forwardHeaders, upstreamFor } from "../lib/dev-proxy.mjs";

// --- routing -------------------------------------------------------------------------------

test("the API's paths go to the API, as the Caddyfile routes them", () => {
  for (const path of ["/api/users", "/api/users?page=2", "/hubs/notifications", "/hubs/notifications?id=x", "/hangfire", "/hangfire/jobs", "/health"]) {
    assert.equal(upstreamFor(path), "api", path);
  }
});

test("everything else, including look-alikes of the API's paths, goes to the web app", () => {
  for (const path of ["/", "/en/signin", "/api", "/apix/y", "/hubs", "/hangfire-docs", "/healthz", "/health/x", "/_next/webpack-hmr", "/en/api/x"]) {
    assert.equal(upstreamFor(path), "web", path);
  }
});

test("the forwarding headers keep Host, append to X-Forwarded-For and never take Proto or Host from the client", () => {
  const out = forwardHeaders(
    { host: "default.localhost:3000", "x-forwarded-for": "10.0.0.1", "x-forwarded-proto": "https", "x-forwarded-host": "evil.example" },
    "127.0.0.1",
  );
  assert.equal(out.host, "default.localhost:3000");
  assert.equal(out["x-forwarded-host"], "default.localhost:3000");
  assert.equal(out["x-forwarded-for"], "10.0.0.1, 127.0.0.1");
  assert.equal(out["x-forwarded-proto"], "http");
});

// --- the running proxy ---------------------------------------------------------------------

/** Listen on a free loopback port and resolve with it. */
function listen(server) {
  return new Promise((resolve) => server.listen(0, "127.0.0.1", () => resolve(server.address().port)));
}

/** A port nothing listens on: take a free one and give it back. */
async function deadPort() {
  const server = net.createServer();
  const port = await listen(server);
  await new Promise((resolve) => server.close(resolve));
  return port;
}

/** An upstream that answers every request with its own name and the headers it received. */
function stubUpstream(name) {
  const server = http.createServer((req, res) => {
    if (req.url === "/dies-midway") {
      // Headers and part of a body, then the upstream goes away.
      res.writeHead(200, { "content-type": "text/plain" });
      res.write("partial");
      setTimeout(() => res.socket.destroy(), 20);
      return;
    }
    res.writeHead(200, { "content-type": "application/json", "set-cookie": `from=${name}` });
    res.end(JSON.stringify({ name, url: req.url, headers: req.headers }));
  });
  // Answer an upgrade with 101, then echo whatever arrives, prefixed with the upstream's name.
  server.on("upgrade", (req, socket) => {
    socket.write("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n");
    socket.on("data", (chunk) => socket.write(`${name}:${chunk}`));
    // An http.Server socket is half-open after an upgrade, so close it once the other side has.
    socket.on("end", () => socket.end());
    socket.on("error", () => socket.destroy());
  });
  return server;
}

async function withProxy({ api = true, web = true }, run) {
  const upstreams = [];
  const apiPort = api ? await listen(upstreams[upstreams.push(stubUpstream("api")) - 1]) : await deadPort();
  const webPort = web ? await listen(upstreams[upstreams.push(stubUpstream("web")) - 1]) : await deadPort();
  const logged = [];
  const proxy = createDevProxy({ apiPort, webPort, host: "127.0.0.1", log: (line) => logged.push(line) });
  const port = await listen(proxy);
  try {
    await run({ port, logged });
  } finally {
    proxy.closeAllConnections?.();
    await new Promise((resolve) => proxy.close(resolve));
    for (const server of upstreams) {
      server.closeAllConnections?.();
      await new Promise((resolve) => server.close(resolve));
    }
  }
}

/** One request through the proxy with an explicit Host header. */
function request(port, path, host) {
  return new Promise((resolve, reject) => {
    const req = http.request({ host: "127.0.0.1", port, path, headers: { host } }, (res) => {
      let body = "";
      res.setEncoding("utf8");
      res.on("data", (chunk) => (body += chunk));
      res.on("end", () => resolve({ status: res.statusCode, headers: res.headers, body }));
    });
    req.on("error", reject);
    req.end();
  });
}

test("a request reaches its upstream with the visitor's Host and the forwarding headers", async () => {
  await withProxy({}, async ({ port }) => {
    const api = await request(port, "/api/users?page=2", "default.localhost:3000");
    assert.equal(api.status, 200);
    const seen = JSON.parse(api.body);
    assert.equal(seen.name, "api");
    assert.equal(seen.url, "/api/users?page=2");
    assert.equal(seen.headers.host, "default.localhost:3000");
    assert.equal(seen.headers["x-forwarded-host"], "default.localhost:3000");
    assert.equal(seen.headers["x-forwarded-proto"], "http");
    assert.deepEqual(api.headers["set-cookie"], ["from=api"]);

    const web = JSON.parse((await request(port, "/en/signin", "app.localhost:3000")).body);
    assert.equal(web.name, "web");
    assert.equal(web.headers.host, "app.localhost:3000");
  });
});

test("an upstream that is down answers 502 and the proxy keeps serving the other", async () => {
  await withProxy({ api: false }, async ({ port, logged }) => {
    const down = await request(port, "/api/users", "localhost:3000");
    assert.equal(down.status, 502);
    assert.match(down.body, /the API/);
    assert.equal(logged.length, 1);
    const web = await request(port, "/", "localhost:3000");
    assert.equal(web.status, 200);
  });
});

test("an upstream that dies part-way through a response cuts the visitor's response too", async () => {
  await withProxy({}, async ({ port }) => {
    const outcome = await new Promise((resolve) => {
      const req = http.request({ host: "127.0.0.1", port, path: "/dies-midway" }, (res) => {
        res.on("data", () => {});
        res.on("end", () => resolve("ended"));
        res.on("aborted", () => resolve("aborted"));
        res.on("error", () => resolve("aborted"));
        res.on("close", () => resolve(res.complete ? "ended" : "aborted"));
      });
      req.on("error", () => resolve("aborted"));
      req.end();
      setTimeout(() => resolve("hung"), 3000);
    });
    assert.equal(outcome, "aborted");
  });
});

/** Send a raw upgrade request through the proxy and resolve with everything read back. */
function upgrade(port, path, payload) {
  return new Promise((resolve, reject) => {
    const socket = net.connect({ host: "127.0.0.1", port }, () => {
      socket.write(
        `GET ${path} HTTP/1.1\r\nHost: default.localhost:3000\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n`,
      );
    });
    let received = "";
    socket.setEncoding("utf8");
    socket.on("data", (chunk) => {
      received += chunk;
      if (received.includes("101") && !received.includes(payload)) socket.write(payload);
      if (received.includes(`:${payload}`) || received.includes("502")) {
        socket.destroy();
        resolve(received);
      }
    });
    socket.on("error", reject);
  });
}

test("WebSocket upgrades are piped to the hub and to Next's hot reload", async () => {
  await withProxy({}, async ({ port }) => {
    const hub = await upgrade(port, "/hubs/notifications", "ping");
    assert.match(hub, /^HTTP\/1\.1 101/);
    assert.match(hub, /api:ping/);
    const hmr = await upgrade(port, "/_next/webpack-hmr", "ping");
    assert.match(hmr, /web:ping/);
  });
});

test("an upgrade to an upstream that is down answers 502", async () => {
  await withProxy({ api: false }, async ({ port }) => {
    assert.match(await upgrade(port, "/hubs/notifications", "ping"), /^HTTP\/1\.1 502/);
  });
});
