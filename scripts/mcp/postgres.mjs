#!/usr/bin/env node
/**
 * The `postgres` MCP server of `.mcp.json`: Postgres MCP Pro, read-only.
 *
 * Connects to the database `ConnectionStrings:DefaultConnection` of appsettings.Development.json
 * names (EFN_APP picks the app, as for the other stack scripts), passed as DATABASE_URI in the
 * server's environment so the password is in neither `.mcp.json` nor argv. `restricted` runs
 * every statement in a read-only transaction with a time limit: SQL sent through MCP is not seen
 * by the shell guards, so this mode is what stops a DROP or DELETE. Requires uv (`uvx`).
 */
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { failMcp, launchMcpServer, npgsqlToUri, readDevConnectionStrings } from "../lib/mcp-connections.mjs";

const VERSION = "0.3.0";
// 0.3.0 pins a pglast release PyPI no longer serves and leaves `mcp` unbounded, whose 2.x
// removed the FastMCP it imports; the overrides make it installable. Drop them on an upgrade
// that resolves without them.
const OVERRIDES = join(dirname(fileURLToPath(import.meta.url)), "postgres-overrides.txt");

const { postgres } = readDevConnectionStrings();
if (!postgres || /\{password\}/.test(postgres.value)) {
  failMcp("postgres", "no usable ConnectionStrings:DefaultConnection in appsettings.Development.json.");
}
launchMcpServer("postgres", "uvx", ["--overrides", OVERRIDES, `postgres-mcp==${VERSION}`, "--access-mode=restricted", ...process.argv.slice(2)], {
  DATABASE_URI: npgsqlToUri(postgres.value),
});
