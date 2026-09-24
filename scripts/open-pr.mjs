#!/usr/bin/env node
/**
 * Print (and optionally open) the pull-request page for the current branch.
 *
 *   npm run pr           # print the URL
 *   npm run pr -- --open # print and open it in the browser
 *
 * It opens nothing on the forge by itself and never merges: creating the pull request is
 * a click, or `auto-ship.mjs` with the GitHub CLI installed.
 */
import { spawnSync } from "node:child_process";
import { BASE_BRANCH } from "./lib/project-config.mjs";
import { describeRemote, pullRequestUrl } from "./lib/remote.mjs";

const git = (...args) => (spawnSync("git", args, { encoding: "utf8" }).stdout ?? "").trim();

const branch = git("rev-parse", "--abbrev-ref", "HEAD");
const remote = git("remote", "get-url", "origin");

if (!branch || branch === "HEAD") {
  console.error("Not on a branch.");
  process.exit(1);
}
if (!remote) {
  console.error("No 'origin' remote configured.");
  process.exit(1);
}

const described = describeRemote(remote);
if (!described) {
  console.error(`Could not parse the origin remote: ${remote}`);
  process.exit(1);
}

const url = pullRequestUrl(described.forge, described.slug, branch, BASE_BRANCH);
if (!url) {
  console.error(`Unrecognised host '${described.host}'. Open a PR for branch '${branch}' manually.`);
  process.exit(1);
}

const unpushed = spawnSync("git", ["rev-parse", "--verify", `origin/${branch}`], { encoding: "utf8" }).status !== 0;
if (unpushed) {
  console.log(`Branch '${branch}' is not on origin yet. Push it first:\n`);
  console.log(`  git push -u origin ${branch}\n`);
}

console.log(`Open a pull request for '${branch}' into '${BASE_BRANCH}':\n`);
console.log(`  ${url}\n`);

if (process.argv.includes("--open")) {
  // `start` treats its first quoted argument as a window title, hence the empty one.
  // Verbatim, so the quotes reach cmd as written and an `&` in the URL is not a separator.
  if (process.platform === "win32") {
    spawnSync("cmd", ["/c", "start", '""', `"${url}"`], { stdio: "ignore", windowsHide: true, windowsVerbatimArguments: true });
  } else {
    spawnSync(process.platform === "darwin" ? "open" : "xdg-open", [url], { stdio: "ignore" });
  }
}
