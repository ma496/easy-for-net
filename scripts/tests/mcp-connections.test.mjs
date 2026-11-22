import test from "node:test";
import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { ipv4Loopback, npgsqlToUri, readDevConnectionStrings, stackExchangeToRedisEnv } from "../lib/mcp-connections.mjs";
import { REPO_ROOT } from "../lib/project-config.mjs";

// --- npgsqlToUri -----------------------------------------------------------------------

test("npgsqlToUri converts the shape the templates ship", () => {
  assert.equal(
    npgsqlToUri("Host=localhost;Port=5433;Database=my_app;Username=postgres;Password=postgres"),
    "postgresql://postgres:postgres@127.0.0.1:5433/my_app",
  );
});

test("ipv4Loopback names localhost by its IPv4 address and leaves every other host alone", () => {
  assert.equal(ipv4Loopback("localhost"), "127.0.0.1");
  assert.equal(ipv4Loopback("LocalHost"), "127.0.0.1");
  assert.equal(ipv4Loopback("db.internal"), "db.internal");
  assert.equal(ipv4Loopback("::1"), "::1");
});

test("npgsqlToUri accepts the Server, User Id and Pwd aliases in any case and spacing", () => {
  assert.equal(npgsqlToUri("server=db; user id=app ; pwd=x; database=d"), "postgresql://app:x@db:5432/d");
});

test("npgsqlToUri percent-encodes a password that would break the URI", () => {
  assert.equal(npgsqlToUri("Host=h;Database=d;Username=u;Password=p@ss/w:rd#1"), "postgresql://u:p%40ss%2Fw%3Ard%231@h:5432/d");
});

test("npgsqlToUri leaves out what the string does not name", () => {
  assert.equal(npgsqlToUri("Host=h;Database=d;Username=u"), "postgresql://u@h:5432/d");
  assert.equal(npgsqlToUri("Database=d"), "postgresql://127.0.0.1:5432/d");
});

test("npgsqlToUri carries an SSL mode, but not Disable", () => {
  assert.equal(npgsqlToUri("Host=h;Database=d;SSL Mode=Require"), "postgresql://h:5432/d?sslmode=require");
  assert.equal(npgsqlToUri("Host=h;Database=d;SslMode=Disable"), "postgresql://h:5432/d");
});

// --- stackExchangeToRedisEnv -----------------------------------------------------------

test("stackExchangeToRedisEnv converts the shape the templates ship", () => {
  assert.deepEqual(stackExchangeToRedisEnv("localhost:6380,password=redis"), {
    REDIS_HOST: "127.0.0.1",
    REDIS_PORT: "6380",
    REDIS_PWD: "redis",
  });
});

test("stackExchangeToRedisEnv reads user, ssl and the default database, and ignores other options", () => {
  assert.deepEqual(stackExchangeToRedisEnv("cache.example.com:6379, user=app, password=a=b, ssl=True, defaultDatabase=2, abortConnect=false"), {
    REDIS_HOST: "cache.example.com",
    REDIS_PORT: "6379",
    REDIS_USERNAME: "app",
    REDIS_PWD: "a=b",
    REDIS_SSL: "True",
    REDIS_DB: "2",
  });
});

test("stackExchangeToRedisEnv defaults the port", () => {
  assert.deepEqual(stackExchangeToRedisEnv("redis"), { REDIS_HOST: "redis", REDIS_PORT: "6379" });
});

// --- readDevConnectionStrings ----------------------------------------------------------

test("readDevConnectionStrings prefers the environment's file and falls back to the base one", () => {
  const root = mkdtempSync(join(tmpdir(), "efn-mcp-"));
  try {
    const source = join(root, "app", "backend", "Source");
    mkdirSync(source, { recursive: true });
    writeFileSync(join(source, "appsettings.json"), JSON.stringify({ ConnectionStrings: { DefaultConnection: "base", Redis: "base-redis" } }));
    writeFileSync(join(source, "appsettings.Development.json"), "﻿" + JSON.stringify({ ConnectionStrings: { DefaultConnection: "dev" } }));

    const { postgres, redis } = readDevConnectionStrings({ app: { backend: "app/backend" }, root });

    assert.equal(postgres.value, "dev");
    assert.equal(redis.value, "base-redis");
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("readDevConnectionStrings answers undefined when no file names a connection", () => {
  const root = mkdtempSync(join(tmpdir(), "efn-mcp-"));
  try {
    assert.deepEqual(readDevConnectionStrings({ app: { backend: "missing" }, root }), { postgres: undefined, redis: undefined });
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

// --- .mcp.json -------------------------------------------------------------------------

test(".mcp.json starts every server through a script that exists, and names no credential", () => {
  const text = readFileSync(join(REPO_ROOT, ".mcp.json"), "utf8");
  const { mcpServers } = JSON.parse(text);
  assert.deepEqual(Object.keys(mcpServers).sort(), ["playwright", "postgres", "redis"]);
  for (const [name, server] of Object.entries(mcpServers)) {
    assert.equal(server.command, "node", `${name} must start through node`);
    assert.ok(existsSync(join(REPO_ROOT, server.args[0])), `${name}: ${server.args[0]} does not exist`);
    assert.equal(server.env, undefined, `${name} must take its connection from the settings, not .mcp.json`);
  }
  assert.doesNotMatch(text, /password|pwd|postgres(ql)?:\/\/|redis:\/\//i);
});

test("the Playwright MCP server runs headless with an isolated profile", () => {
  const text = readFileSync(join(REPO_ROOT, "scripts", "mcp", "playwright.mjs"), "utf8");
  const launch = text.match(/launchMcpServer\("playwright",[^\n]*\);/);
  assert.ok(launch, "scripts/mcp/playwright.mjs no longer calls launchMcpServer the way this test reads it");
  assert.match(launch[0], /"--headless"/);
  assert.match(launch[0], /"--isolated"/);
});

test("every Redis MCP tool that writes is denied in .claude/settings.json", () => {
  const { permissions } = JSON.parse(readFileSync(join(REPO_ROOT, ".claude", "settings.json"), "utf8"));
  const writers = ["set", "delete", "expire", "rename", "hset", "hdel", "json_set", "json_del", "lpush", "rpush", "lpop", "rpop", "lrem", "sadd", "srem", "zadd", "zrem", "xadd", "xdel", "xack", "xgroup_create", "xgroup_destroy", "xreadgroup", "publish", "set_vector_in_hash", "create_vector_index_hash"];
  for (const tool of writers) assert.ok(permissions.deny.includes(`mcp__redis__${tool}`), `mcp__redis__${tool} is not denied`);
});
