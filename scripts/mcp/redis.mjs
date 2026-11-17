#!/usr/bin/env node
/**
 * The `redis` MCP server of `.mcp.json`: the official Redis MCP server.
 *
 * Connects to the Redis `ConnectionStrings:Redis` of appsettings.Development.json names (EFN_APP
 * picks the app), passed as REDIS_* variables in the server's environment so the password is in
 * neither `.mcp.json` nor argv. The server has no read-only mode: its writing tools are refused by
 * `permissions.deny` in `.claude/settings.json`, since this Redis holds the API's sessions.
 * Requires uv (`uvx`).
 */
import { failMcp, launchMcpServer, readDevConnectionStrings, stackExchangeToRedisEnv } from "../lib/mcp-connections.mjs";

const VERSION = "0.5.1";

const { redis } = readDevConnectionStrings();
if (!redis) failMcp("redis", "no ConnectionStrings:Redis in appsettings.Development.json.");
launchMcpServer("redis", "uvx", ["--from", `redis-mcp-server==${VERSION}`, "redis-mcp-server", ...process.argv.slice(2)], stackExchangeToRedisEnv(redis.value));
