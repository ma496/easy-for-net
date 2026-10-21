/**
 * The pure parts of scripts/dev-proxy.mjs: which upstream serves a path, the headers sent to it,
 * and the server itself (not yet listening, so a test can put it on a free port).
 */
import http from "node:http";
import net from "node:net";
import { pipeline } from "node:stream";

/** The paths the API serves; everything else is the web app's. Mirrors `@api` in docker/Caddyfile. */
const API_PATH = /^\/(api\/|hubs\/|hangfire(\/|$)|health$)/;

/** Which upstream serves a request target (`/api/users?x=1`): "api" or "web". */
export function upstreamFor(target) {
  const path = String(target ?? "/").split("?")[0];
  return API_PATH.test(path) ? "api" : "web";
}

/**
 * The headers sent upstream: the visitor's own, `Host` included, with the forwarding headers a
 * reverse proxy adds. An `X-Forwarded-For` already present is appended to — the API reads only
 * the rightmost entry, the peer added here. `X-Forwarded-Proto` and `-Host` are overwritten, never
 * taken from the client, as Caddy does for a client it does not trust: the API honours them from a
 * loopback proxy, so a visitor must not be able to claim https or another host through this one.
 */
export function forwardHeaders(headers, remoteAddress) {
  const out = { ...headers };
  const prior = headers["x-forwarded-for"];
  if (remoteAddress) out["x-forwarded-for"] = prior ? `${prior}, ${remoteAddress}` : remoteAddress;
  out["x-forwarded-proto"] = "http";
  if (headers.host) out["x-forwarded-host"] = headers.host;
  else delete out["x-forwarded-host"];
  return out;
}

/**
 * The proxy server, not yet listening. `ports` names the two upstreams on `host` (loopback by
 * default); `log` receives one line per upstream failure.
 */
export function createDevProxy({ apiPort, webPort, host = "localhost", log = () => {} }) {
  const ports = { api: Number(apiPort), web: Number(webPort) };
  const name = { api: `the API (:${ports.api})`, web: `the web app (:${ports.web})` };

  const server = http.createServer((req, res) => {
    const upstream = upstreamFor(req.url);
    const outgoing = http.request(
      {
        host,
        port: ports[upstream],
        method: req.method,
        path: req.url,
        headers: forwardHeaders(req.headers, req.socket.remoteAddress),
      },
      (upstreamRes) => {
        res.writeHead(upstreamRes.statusCode ?? 502, upstreamRes.statusMessage, upstreamRes.rawHeaders);
        // An upstream that dies part-way through (next dev restarting, the API rebuilding) aborts its
        // response rather than erroring the request, so the visitor's response is cut with it —
        // otherwise the browser would wait on a body that never ends.
        pipeline(upstreamRes, res, (err) => {
          if (err) res.destroy();
        });
      },
    );
    outgoing.on("error", (err) => {
      log(`dev-proxy: ${req.method} ${req.url} → ${name[upstream]} failed: ${err.code ?? err.message}`);
      if (res.headersSent) res.destroy();
      else {
        res.writeHead(502, { "content-type": "text/plain; charset=utf-8" });
        res.end(`502 Bad Gateway: ${name[upstream]} is not answering (${err.code ?? err.message}).\n`);
      }
    });
    // A visitor who goes away mid-request takes the upstream request with them.
    res.on("close", () => {
      if (!res.writableFinished) outgoing.destroy();
    });
    req.pipe(outgoing);
  });

  // WebSockets: replay the upgrade request on a raw connection to the upstream, then splice the two
  // sockets. Whatever the upstream answers — 101 or a refusal such as the hub's 401/403 — reaches
  // the browser byte for byte.
  server.on("upgrade", (req, socket, head) => {
    const upstream = upstreamFor(req.url);
    const target = net.connect({ host, port: ports[upstream] });
    let connected = false;
    target.on("connect", () => {
      connected = true;
      const headers = forwardHeaders(req.headers, req.socket.remoteAddress);
      const lines = [`${req.method} ${req.url} HTTP/${req.httpVersion}`];
      for (const [key, value] of Object.entries(headers)) {
        for (const v of Array.isArray(value) ? value : [value]) lines.push(`${key}: ${v}`);
      }
      target.write(`${lines.join("\r\n")}\r\n\r\n`);
      if (head?.length) target.write(head);
      target.pipe(socket);
      socket.pipe(target);
    });
    target.on("error", (err) => {
      log(`dev-proxy: upgrade ${req.url} → ${name[upstream]} failed: ${err.code ?? err.message}`);
      // A 502 only while nothing has been relayed; once connected, the socket may already be a live
      // WebSocket, and status text written into it would corrupt its frames.
      if (!connected && socket.writable) {
        socket.end("HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");
      } else socket.destroy();
    });
    socket.on("error", () => target.destroy());
    socket.on("close", () => target.destroy());
    target.on("close", () => socket.destroy());
  });

  // A malformed request from a client is that client's problem, never the proxy's.
  server.on("clientError", (_err, socket) => {
    if (socket.writable) socket.end("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
  });

  return server;
}
