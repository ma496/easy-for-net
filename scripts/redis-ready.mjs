#!/usr/bin/env node
/**
 * Is the Redis the API keeps its sessions in accepting connections?
 *
 *   node scripts/redis-ready.mjs              # probes the Development settings
 *   node scripts/redis-ready.mjs Testing      # probes the ones appsettings.Testing.json names
 *
 * Prints "ready host:port" and exits 0 when it is, prints nothing and exits 1 when it is not.
 * The printed line matters: verify.mjs and loop.mjs read a dependency as healthy when its
 * probe writes to stdout. Only a TCP connection is attempted — no credentials are read and no
 * command is sent, so the probe is safe to run on a timer and never touches what Redis holds.
 */
import { existsSync, readFileSync } from "node:fs";
import { connect } from "node:net";
import { join } from "node:path";
import { REPO_ROOT } from "./lib/project-config.mjs";

const environment = process.argv[2] || "Development";
const settingsDir = join(REPO_ROOT, "src", "backend", "Source");

/** Host and port from the connection string (`host:port[,options]`), falling back to the base file, then the default. */
function target() {
  for (const file of [`appsettings.${environment}.json`, "appsettings.json"]) {
    const path = join(settingsDir, file);
    if (!existsSync(path)) continue;
    try {
      const cs = JSON.parse(readFileSync(path, "utf8").replace(/^﻿/, ""))?.ConnectionStrings?.Redis;
      if (!cs) continue;
      const endpoint = cs.split(",")[0].trim();
      const separator = endpoint.lastIndexOf(":");
      const host = separator > 0 ? endpoint.slice(0, separator) : endpoint;
      const port = separator > 0 ? Number(endpoint.slice(separator + 1)) : 6379;
      return { host: host || "localhost", port: Number.isInteger(port) && port > 0 ? port : 6379 };
    } catch {
      /* unreadable settings — try the next file */
    }
  }
  return { host: "localhost", port: 6379 };
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
