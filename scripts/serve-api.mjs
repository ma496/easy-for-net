#!/usr/bin/env node
/**
 * Start the API on the port verify.mjs asks for, as the Development environment.
 *
 *   PORT=5100 node scripts/serve-api.mjs
 *
 * The launch profile is skipped on purpose: it pins the URL to :5000, and a live check that
 * starts its own service on a free port must actually get that port. The environment is set
 * here instead of by the profile, so the API still boots with its Development settings.
 */
import { join } from "node:path";
import { spawnPortable } from "./lib/proc.mjs";
import { REPO_ROOT } from "./lib/project-config.mjs";

const port = process.env.PORT || "5000";
const child = spawnPortable(
  "dotnet",
  ["run", "--project", join(REPO_ROOT, "src", "backend", "Source"), "--no-launch-profile", "--urls", `http://localhost:${port}`],
  {
    cwd: REPO_ROOT,
    stdio: "inherit",
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT || "Development" },
  },
);
child.on("exit", (code) => process.exit(code ?? 1));
for (const sig of ["SIGINT", "SIGTERM"]) process.on(sig, () => child.kill(sig));
