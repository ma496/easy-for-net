#!/usr/bin/env node
/**
 * The `playwright` MCP server of `.mcp.json`: Microsoft's Playwright MCP.
 *
 * A clean browser profile per session (`--isolated`), so nothing signed in carries between runs,
 * and screenshots and traces under the git-ignored `.playwright-mcp/`. Extra arguments are passed
 * through (`--headless`, `--browser chrome`). Started through `lib/proc.mjs`, so `pnpm` resolves on
 * Windows as elsewhere.
 */
import { launchMcpServer } from "../lib/mcp-connections.mjs";

const VERSION = "0.0.85";

launchMcpServer("playwright", "pnpm", ["dlx", `@playwright/mcp@${VERSION}`, "--isolated", "--output-dir", ".playwright-mcp", ...process.argv.slice(2)]);
