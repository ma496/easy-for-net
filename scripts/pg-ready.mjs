#!/usr/bin/env node
/**
 * Is the PostgreSQL the API and its tests use accepting connections?
 *
 *   node scripts/pg-ready.mjs              # probes the Development database
 *   node scripts/pg-ready.mjs Testing      # probes the one appsettings.Testing.json names
 *
 * Prints "ready host:port" and exits 0 when it is, prints nothing and exits 1 when it is not.
 * The printed line matters: verify.mjs and loop.mjs read a dependency as healthy when its
 * probe writes to stdout. Only a TCP connection is attempted — no credentials are read, so
 * the probe is safe to run on a timer and never touches the database's contents.
 */
import { existsSync, readFileSync } from "node:fs";
import { connect } from "node:net";
import { join } from "node:path";
import { REPO_ROOT } from "./lib/project-config.mjs";

const environment = process.argv[2] || "Development";
const settingsDir = join(REPO_ROOT, "src", "backend", "Source");

/** Host and port from the connection string, falling back to the base file, then the default. */
function target() {
  for (const file of [`appsettings.${environment}.json`, "appsettings.json"]) {
    const path = join(settingsDir, file);
    if (!existsSync(path)) continue;
    try {
      const cs = JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""))?.ConnectionStrings?.DefaultConnection;
      if (!cs) continue;
      const part = (key) => cs.match(new RegExp(`(?:^|;)\\s*${key}\\s*=\\s*([^;]+)`, "i"))?.[1]?.trim();
      return { host: part("Host") || part("Server") || "localhost", port: Number(part("Port") || 5432) };
    } catch {
      /* unreadable settings — try the next file */
    }
  }
  return { host: "localhost", port: 5432 };
}

const { host, port } = target();
const socket = connect({ host, port, timeout: 2000 });
socket.once("connect", () => {
  console.log(`ready ${host}:${port}`);
  socket.destroy();
  process.exit(0);
});
const fail = () => {
  socket.destroy();
  process.exit(1);
};
socket.once("timeout", fail);
socket.once("error", fail);
