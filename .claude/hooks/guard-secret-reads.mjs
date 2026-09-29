#!/usr/bin/env node
/**
 * PreToolUse guard: refuse reading a file that holds live credentials, through Read or Grep.
 *
 * The shell guard stops `cat .env`; `settings.json` denies the Read tool on `./.env` alone.
 * That left the Read tool free to open `.env.production` or a per-environment
 * `appsettings.*.json` — the very files the shell guard calls live credentials — and an
 * unattended run with permissions bypassed would do it without asking. Permission `deny`
 * rules cannot carve out the shared `.env.example`, so the rule lives here, where it runs in
 * every permission mode.
 *
 * Grep is refused when its path names a secret file. A search over a directory is left
 * alone, whatever its glob: ripgrep skips git-ignored files — which is where secrets live —
 * and a glob filters that set rather than overriding it; only a file named outright is read
 * regardless. Glob only lists names, which reveals nothing, and is not guarded.
 *
 * Exit 2 blocks the call and returns stderr to the model. Exit 0 allows it.
 */
import { secretRuleFor } from "./secret-paths.mjs";

let raw = "";
process.stdin.on("data", (chunk) => (raw += chunk));
process.stdin.on("end", () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0);
  }
  const tool = input?.tool_name ?? "";
  const args = input?.tool_input ?? {};
  const targets = [args.file_path, args.path].filter((t) => typeof t === "string" && t);
  for (const target of targets) {
    const rule = secretRuleFor(target);
    if (rule) {
      process.stderr.write(
        `Blocked: ${tool || "this tool"} would read '${target}', which ${rule.what}. ${rule.instead}\n`,
      );
      process.exit(2);
    }
  }
  process.exit(0);
});
