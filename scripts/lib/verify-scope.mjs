/**
 * What an unattended task's verify judges: the task's own change, not the whole branch.
 *
 * `verify.mjs` defaults to "branch" scope — every commit since the base plus the working
 * tree — because that is what a pull request contains and what its owner reviews. Inside a
 * drain that is the wrong question. Each task commits only after a passing verify, so the
 * committed half of the branch has already been judged, and the uncommitted tree is exactly
 * what this task changed. On a long-lived branch, branch scope made a web-only task pay for
 * the backend suite and the live API check because earlier backend tasks sat on the branch.
 *
 * The branch as a whole is still judged where it is the question: `/verify` by hand,
 * `/ship`, and before a pull request — none of which pass this scope.
 */

/** The scope the runner verifies a task's tree in, and tells the lead to verify in. */
export const TASK_VERIFY_SCOPE = "working";

/** `npm run verify -- --scope working` from the configured verify command. */
export function taskVerifyCommand(verifyCommand, scope = TASK_VERIFY_SCOPE) {
  const base = String(verifyCommand ?? "").trim();
  if (!base) throw new Error("taskVerifyCommand: no verify command is configured");
  // `npm run x -- a` already forwards; a second `--` would reach the script as an argument.
  const separator = /^npm(\.cmd)?\s+run\s+\S+$/.test(base) ? " --" : "";
  return `${base}${separator} --scope ${scope}`;
}

/** The arguments the runner hands `node`, for its own verify of a task's tree. */
export function runnerVerifyArgs({ script, port, base, scope = TASK_VERIFY_SCOPE, autostart = true, extra = [] }) {
  const args = [script, "--port", String(port), "--base", base, "--scope", scope, ...extra];
  if (autostart) args.push("--autostart");
  return args;
}

/**
 * The flags a session's verify may carry and still be answered by the runner's own verify:
 * none that changes what is judged. `--full`, `--json`, `--scope branch`, a port or a base all
 * ask a different question, so a call carrying one runs locally as it always did.
 */
const FORWARDABLE = new Set(["--autostart", "--scope", TASK_VERIFY_SCOPE]);

/**
 * Whether this `verify.mjs` call is answered by the task runner instead of run here.
 *
 * Inside an unattended run the lead verifies the finished tree, and the runner then verified
 * the same bytes again because it cannot trust a pass the session reports — anything the
 * session writes it can forge. Asking the runner to run it fixes both: the runner runs it,
 * fingerprints the tree itself, and reuses that pass after the session if nothing changed.
 */
export function shouldForwardVerify(argv, env) {
  if (!env?.AGENT_VERIFY_URL) return false;
  const scopeAt = argv.indexOf("--scope");
  if (scopeAt >= 0 && argv[scopeAt + 1] !== TASK_VERIFY_SCOPE) return false;
  return argv.every((arg) => FORWARDABLE.has(arg));
}
