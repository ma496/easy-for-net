#!/usr/bin/env node
/**
 * The `playwright` MCP server of `.mcp.json`: Microsoft's Playwright MCP.
 *
 * Headless always (`--headless`), so no window opens on the desktop or under an unattended run, and
 * a clean browser profile per session (`--isolated`), so nothing signed in carries between runs.
 * Screenshots and traces go under the git-ignored `.playwright-mcp/`. The browser lives until an
 * agent calls `browser_close`, which every check ends with; the next browser call starts a fresh
 * one. Extra arguments are passed through (`--browser chrome`). Started through `lib/proc.mjs`, so
 * `pnpm` resolves on Windows as elsewhere.
 */
import { launchMcpServer } from "../lib/mcp-connections.mjs";

const VERSION = "0.0.85";

launchMcpServer("playwright", "pnpm", ["dlx", `@playwright/mcp@${VERSION}`, "--headless", "--isolated", "--output-dir", ".playwright-mcp", ...process.argv.slice(2)]);
