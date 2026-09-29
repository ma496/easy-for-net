#!/usr/bin/env node
/**
 * PreToolUse guard: refuse edits to secrets and build output.
 *
 * Claude Code pipes the tool call as JSON on stdin. Exit 2 blocks the call and sends stderr
 * back to the model as feedback; exit 0 allows it.
 *
 * Paths are normalised to forward slashes before any rule sees them: on Windows the tool
 * hands over `D:\repo\.env`, and a rule written against `/.env` would otherwise wave it
 * straight through.
 */
import { compileRules, config } from "../../scripts/lib/project-config.mjs";
import { secretRuleFor } from "./secret-paths.mjs";

/** Generated output: editing it is lost on the next build, and hides the real fix. */
const BUILD_OUTPUT = /(^|\/)(dist|build|\.output|\.next|node_modules|coverage|bin|obj)\//;

/** The rule a path breaks, as the message to send back, or null when it may be written. */
export function refusalFor(rawPath, extraPatterns = compileRules(config.hooks.protectedPaths, "protected path")) {
  const path = String(rawPath ?? "").replace(/\\/g, "/");
  if (!path) return null;

  // Files holding live credentials; their shared templates stay editable.
  const secret = secretRuleFor(path);
  if (secret) {
    return `Blocked: '${rawPath}' ${secret.what}. Put new settings in the shared template, and ask the user to copy any real value across themselves.`;
  }
  if (BUILD_OUTPUT.test(path)) {
    return `Blocked: '${rawPath}' is generated build output. Edit the source it is built from and rebuild.`;
  }
  // Anything else this project protects, from `hooks.protectedPaths` in agentic.config.json.
  const hit = extraPatterns.find((re) => re.test(path));
  if (hit) {
    return (
      `Blocked: '${rawPath}' matches a protected path in agentic.config.json (${hit.source}). ` +
      "If this file really must change, say why and let the user decide."
    );
  }
  return null;
}

let raw = "";
process.stdin.on("data", (chunk) => (raw += chunk));
process.stdin.on("end", () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0);
  }
  const target = input?.tool_input?.file_path ?? input?.tool_input?.notebook_path ?? "";
  const refusal = refusalFor(target);
  if (refusal) {
    process.stderr.write(`${refusal}\n`);
    process.exit(2);
  }
  process.exit(0);
});
