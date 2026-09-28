/**
 * Which checkout is that service serving?
 *
 * A live check is only evidence if it exercised the code under test. A health endpoint
 * answering on the expected port does not establish that: a container publishes the same
 * port, and a dev server left behind by an earlier run sits on whatever port it was given.
 * Either one will happily answer this checkout's live check with another checkout's
 * behaviour, and the run prints "Verified." — the exact lie the gate exists to prevent.
 *
 * So the service reports the directory it was started from, and a run adopts it only when
 * that is the directory being verified. Everything else — a different checkout, a server
 * too old to say, a body that will not parse — is refused, and the caller starts its own.
 *
 * Which field carries that directory is a project's own business: name it as
 * `verify.service.identityField` in `agentic.config.json` (`repoRoot` by default) and have
 * the health route return it.
 *
 * Kept in its own module, free of I/O, so the decision is unit-testable: the caller does
 * the fetching and the realpath resolution and hands the answers in.
 */

/**
 * Trailing slashes are not identity, and neither is how Windows spells a path: `D:\repo`,
 * `d:/repo` and `D:/repo/` name one directory. Everything else about the path is identity.
 */
export function normalizeRoot(value) {
  if (typeof value !== "string") return "";
  let path = value.trim().replace(/\\/g, "/");
  path = path.replace(/^([A-Za-z]):/, (_, drive) => `${drive.toLowerCase()}:`);
  if (path.length <= 1) return path;
  return path.replace(/\/+$/, "");
}

/**
 * @param {unknown} health  parsed `/health` body, or null when it could not be read
 * @param {string} cwd      the checkout being verified, realpath-resolved by the caller
 * @param {string} field    the health field naming the directory the service was started from
 * @returns {{ adopt: boolean, reason: string }} `reason` is written for the run's log —
 *   a refusal has to say which checkout answered, or the failure is unreadable.
 */
export function apiAdoptionVerdict(health, cwd, field = "repoRoot") {
  const here = normalizeRoot(cwd);
  if (!here) {
    return { adopt: false, reason: "the directory under test could not be resolved" };
  }
  if (!health || typeof health !== "object") {
    return { adopt: false, reason: "it did not return a readable /health body" };
  }
  const served = normalizeRoot(health[field]);
  if (!served) {
    return {
      adopt: false,
      reason:
        `it does not report ${field}, so it cannot prove which checkout it serves ` +
        "(an API built before this check, or a different service on the port)",
    };
  }
  if (served !== here) {
    return { adopt: false, reason: `it is serving ${served}, not ${here}` };
  }
  return { adopt: true, reason: `it is serving this checkout (${served})` };
}
