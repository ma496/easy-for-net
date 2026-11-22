#!/usr/bin/env node
/**
 * The `playwright` MCP server of `.mcp.json`: Microsoft's Playwright MCP.
 *
 * Headless always (`--headless`), so no window opens on the desktop or under an unattended run, and
 * a clean browser profile per session (`--isolated`), so nothing signed in carries between runs.
 * Screenshots and snapshots go under the git-ignored `.playwright-mcp/`, one folder per branch and
 * task — `.playwright-mcp/<branch>/<spec>/<task>/`, or `<branch>/manual/` outside a run
 * (`screenshotDir`) — so a person finds a task's screenshots in one place. The runner names the task
 * in `AGENT_TASK_ID`, which the CLI passes on to the servers it starts. The browser lives until an
 * agent calls `browser_close`, which every check ends with; the next browser call starts a fresh
 * one. Extra arguments are passed through (`--browser chrome`). Started through `lib/proc.mjs`, so
 * `pnpm` resolves on Windows as elsewhere.
 */
import { launchMcpServer, screenshotDir } from "../lib/mcp-connections.mjs";
import { runSync } from "../lib/proc.mjs";

const VERSION = "0.0.85";

const branch = String(runSync("git", ["rev-parse", "--abbrev-ref", "HEAD"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).stdout ?? "").trim();
const outputDir = screenshotDir({ branch, taskId: process.env.AGENT_TASK_ID });

launchMcpServer("playwright", "pnpm", ["dlx", `@playwright/mcp@${VERSION}`, "--headless", "--isolated", "--output-dir", outputDir, ...process.argv.slice(2)]);
