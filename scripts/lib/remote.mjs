/**
 * Where the origin remote lives, and the page that opens a pull request there.
 *
 * Pure apart from `sshHostName`, which asks ssh how it would resolve a host alias. SSH
 * aliases are common — `git@github-work:owner/repo.git` with a `Host github-work` block in
 * `~/.ssh/config` — and without resolving them a GitHub remote reads as an unknown host.
 */
import { spawnSync } from "node:child_process";

/** Normalise git@host:owner/repo.git, ssh:// and https:// remotes to { host, slug }. */
export function parseRemote(url) {
  const value = String(url ?? "").trim();
  const scp = value.match(/^(?:[^@/]+@)?([^:/]+):(?!\/)(.+?)(?:\.git)?\/?$/);
  if (scp && !/^[a-z]+:\/\//i.test(value)) return { host: scp[1], slug: scp[2] };
  const full = value.match(/^(?:ssh|https?|git):\/\/(?:[^@/]+@)?([^/:]+)(?::\d+)?\/(.+?)(?:\.git)?\/?$/i);
  if (full) return { host: full[1], slug: full[2] };
  return null;
}

/** The real host behind an SSH alias, via `ssh -G`. Falls back to the alias itself. */
export function sshHostName(alias) {
  const res = spawnSync("ssh", ["-G", alias], { encoding: "utf8", windowsHide: true, timeout: 5000 });
  const line = (res.stdout ?? "").split(/\r?\n/).find((l) => l.toLowerCase().startsWith("hostname "));
  return line ? line.slice("hostname ".length).trim() : alias;
}

/** Which forge a host is, from the resolved host name. */
export function forgeOf(host) {
  const h = String(host ?? "").toLowerCase();
  if (h === "github.com" || h.endsWith(".github.com")) return "github";
  if (h === "gitlab.com") return "gitlab";
  if (h === "bitbucket.org") return "bitbucket";
  return null;
}

/** The page that opens a pull request from `branch` into `base`, or null for an unknown forge. */
export function pullRequestUrl(forge, slug, branch, base) {
  const b = encodeURIComponent(branch);
  switch (forge) {
    case "github":
      return `https://github.com/${slug}/compare/${encodeURIComponent(base)}...${b}?expand=1`;
    case "gitlab":
      return `https://gitlab.com/${slug}/-/merge_requests/new?merge_request%5Bsource_branch%5D=${b}&merge_request%5Btarget_branch%5D=${encodeURIComponent(base)}`;
    case "bitbucket":
      return `https://bitbucket.org/${slug}/pull-requests/new?source=${b}&dest=${encodeURIComponent(base)}`;
    default:
      return null;
  }
}

/** Parse origin and resolve its host: `{ host, slug, forge }`, or null when unparseable. */
export function describeRemote(url, resolveAlias = sshHostName) {
  const parsed = parseRemote(url);
  if (!parsed) return null;
  let forge = forgeOf(parsed.host);
  let host = parsed.host;
  if (!forge) {
    host = resolveAlias(parsed.host);
    forge = forgeOf(host);
  }
  return { host, slug: parsed.slug, forge };
}

/**
 * The ref "not pushed yet" is measured from: the branch's own remote copy, else the base
 * branch's (remote, then local) — what a pull request would be cut against. Null when none
 * exists, as in a project with no remote yet. Pure: `refExists` is supplied.
 *
 * Every report used `origin/<branch>..HEAD` alone, which fails on a branch never pushed and
 * in a repository with no remote, and the failure read as "nothing is waiting for you".
 */
export function unpushedBase(branch, base, refExists) {
  const candidates = [`origin/${branch}`, ...(base && base !== branch ? [`origin/${base}`, base] : [])];
  return candidates.find((ref) => refExists(ref)) ?? null;
}

/** `unpushedBase` against this repository's refs. */
export function unpushedBaseHere(branch, base, cwd = process.cwd()) {
  return unpushedBase(branch, base, (ref) =>
    spawnSync("git", ["rev-parse", "--verify", "--quiet", `${ref}^{commit}`], { cwd, stdio: "ignore" }).status === 0,
  );
}

/**
 * What is waiting to be pushed: `{ since, count, localOnly }`. `since` is the ref counted
 * from, or null. With nothing to count from — no remote yet, as in every freshly generated
 * project, or a remote nothing has been pushed to — none of the history is known to exist
 * anywhere else, so the count is all of it and `localOnly` says so. Reporting "nothing is
 * waiting" there was the one answer that was certainly wrong.
 */
export function unpushedHere(branch, base, cwd = process.cwd()) {
  const since = unpushedBaseHere(branch, base, cwd);
  const range = since ? `${since}..HEAD` : "HEAD";
  const count = Number((spawnSync("git", ["rev-list", "--count", range], { cwd, encoding: "utf8" }).stdout ?? "").trim() || 0);
  return { since, count, localOnly: !since };
}
