#!/usr/bin/env node
/**
 * The development front door: one origin for the web app and the API, as docker/Caddyfile is in
 * production.
 *
 *   node scripts/dev-proxy.mjs                 # :3000 → the API on :5000 and `next dev` on :3001
 *   node scripts/dev-proxy.mjs --port 3000 --api 5000 --web 3001
 *   node scripts/dev-proxy.mjs --host 0.0.0.0  # reachable from other machines — the whole dev API with it
 *
 * `npm run dev` starts it; run it by hand only beside an API and a `next dev` started some other way.
 *
 * `/api/*`, `/hubs/*`, `/hangfire`, `/hangfire/*` and `/health` go to the API — the same matcher
 * as the Caddyfile — and everything else to Next. The browser therefore calls the API as
 * `<host>/api` on whatever host it opened (`localhost`, `default.localhost`, …), so each host keeps
 * its own cookies: the API's cookies carry no `Domain`.
 *
 * - The `Host` header is passed through untouched, never rewritten to the upstream's, so the API
 *   and Next see the host the visitor typed. `X-Forwarded-For`, `-Proto` and `-Host` are set the
 *   way Caddy sets them; the API trusts them because this proxy is on loopback.
 * - WebSocket upgrades are piped through as raw sockets, for the notification hub and for Next's
 *   hot reload alike.
 * - An upstream that is down (the API still building, `next dev` restarting) answers 502 with a
 *   line saying which one, and the proxy keeps running.
 *
 * It listens on loopback only (127.0.0.1 and ::1, so `localhost` and every `*.localhost` name reach
 * it): it fronts the development API, seeded accounts and the Hangfire dashboard included, and that
 * is not something to offer the rest of a café's network. `--host` opts in to another address.
 *
 * Dependency-free on purpose: only `node:http` and `node:net`, so it runs before any `npm install`.
 * The routing and the server live in lib/dev-proxy.mjs.
 */
import { createDevProxy } from "./lib/dev-proxy.mjs";

const argv = process.argv.slice(2);
if (argv.includes("--help")) {
  console.log("usage: node scripts/dev-proxy.mjs [--port 3000] [--api 5000] [--web 3001] [--host <address>]");
  process.exit(0);
}
const argOf = (flag, fallback) => {
  const i = argv.indexOf(`--${flag}`);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};
const port = Number(argOf("port", process.env.DEV_PROXY_PORT ?? 3000));
const apiPort = argOf("api", process.env.PORT ?? 5000);
const webPort = argOf("web", process.env.WEB_PORT ?? 3001);
const explicitHost = argOf("host", null);
// One server per loopback address: a browser may resolve localhost to either. IPv6 switched off on
// the machine (::1 unavailable) is not a failure; IPv4 loopback failing to listen is.
const addresses = explicitHost ? [explicitHost] : ["127.0.0.1", "::1"];
for (const address of addresses) {
  const server = createDevProxy({ apiPort, webPort, log: (line) => console.error(line) });
  server.on("error", (err) => {
    if (address === "::1" && ["EADDRNOTAVAIL", "EAFNOSUPPORT"].includes(err.code)) return;
    console.error(`dev-proxy: cannot listen on ${address}:${port}: ${err.code ?? err.message}`);
    process.exit(1);
  });
  server.listen(port, address, () => {
    console.log(`dev-proxy: ${address.includes(":") ? `[${address}]` : address}:${port} → API :${apiPort}, web :${webPort}`);
  });
}
