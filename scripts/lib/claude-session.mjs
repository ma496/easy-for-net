/**
 * One streaming `claude -p` session: spawn it, narrate its stream while it runs, and decide
 * when it is over.
 *
 * Lifted out of agent-run.mjs so it can be tested against a fake CLI. Four things end a
 * session, and each is reported so the runner judges it correctly:
 *
 *   - it exits on its own (the ordinary case);
 *   - it passes its turn backstop (`stoppedAtLimit`, from the stream renderer) or its wall-clock
 *     deadline (`timedOut`, with `timeLimit` naming whose deadline) and is killed;
 *   - it **settles**: the stream says it has finished — a final `result`, nothing pending in the
 *     background, then quiet (lib/session-settle.mjs) — but the process does not exit. It is
 *     closed and reported `settled`, which the runner treats as finished rather than as a crash
 *     or a time-out;
 *   - it exits but something it started still holds its output pipe, so "close" never fires.
 *     The runner stops waiting a grace period after the exit.
 */
import { renderStream } from "./stream-render.mjs";
import { createSettleTracker } from "./session-settle.mjs";
import { IS_WINDOWS, killTree, killTreeHard, spawnPortable } from "./proc.mjs";

/**
 * @param {string} cmd
 * @param {string[]} args
 * @param {object} options
 * @param {string} [options.input]        written to the session's stdin, then closed
 * @param {(line: string) => void} [options.onLine]  every raw line, e.g. to keep the stream on disk
 * @param {object} [options.env]          the child's environment (default: this process's)
 * @param {number} [options.maxTurns]     the stream-side turn backstop
 * @param {number} [options.attemptEndsAt] epoch ms the attempt's own wall clock runs out (Infinity: none)
 * @param {number} [options.taskEndsAt]   epoch ms the task's wall clock runs out (Infinity: none)
 * @param {number} [options.settleMs]     quiet after a finished stream before the session is closed (Infinity: never)
 * @param {number} [options.stopGraceMs]  how long a stopped or exited session gets before its output is cut
 * @param {number} [options.checkMs]      how often the deadlines and the settle state are checked
 * @param {(s: string) => void} [options.write]  where the narrated stream goes
 * @param {(s: string) => void} [options.log]    where the runner's own notices go
 * @param {(which: "attempt" | "task") => string} [options.describeTimeout]  the notice for a deadline stop
 */
export async function runStreaming(cmd, args, options = {}) {
  const {
    input = "",
    onLine,
    env = process.env,
    maxTurns = Infinity,
    attemptEndsAt = Infinity,
    taskEndsAt = Infinity,
    settleMs = Infinity,
    stopGraceMs = 30_000,
    checkMs = 15_000,
    write = (s) => console.log(s),
    log = (s) => console.error(s),
    describeTimeout = (which) => `\n  ✗ stopped: the ${which} ran past its wall-clock limit`,
  } = options;

  // Through proc.mjs, so an npm-installed `claude.cmd` starts on Windows as the native
  // binary does. The prompt goes in on stdin rather than as an argument: brief, memory,
  // history and feedback together can pass Windows' 32,767-character command line, and a
  // spawn that fails on length reads downstream as "nothing ran" — the account-blocked path.
  const child = spawnPortable(cmd, args, {
    stdio: ["pipe", "pipe", "inherit"],
    env,
    // Its own process group on POSIX, so killTree reaches everything the session started.
    detached: !IS_WINDOWS,
  });
  // Detached, it no longer hears the terminal's Ctrl-C or the drain's SIGTERM — so this
  // process passes them on rather than leaving a session writing to a tree it abandoned.
  const forward = () => {
    killTree(child.pid);
    process.exit(130);
  };
  const signals = ["SIGINT", "SIGTERM", "SIGHUP"];
  for (const sig of signals) process.on(sig, forward);
  child.stdin.on("error", () => {}); // A child that exits early closes the pipe; that is not ours to throw.
  child.stdin.end(input);

  const tracker = createSettleTracker({ quietMs: settleMs });
  let killedFor = null;
  let settle = null; // resolves the status below; set once the promise exists
  let exitCode = null;
  let escalation = null;
  let exitGrace = null;
  // Kill and keep killing: the first stop takes the tree as it stands; if the session is
  // still not gone a grace period later, the tree is taken again and the pipes are cut, so a
  // background shell or server that inherited them can no longer hold the runner open.
  const kill = () => {
    // The whole tree: on Windows `child.kill` ends only the shell or the CLI itself and
    // leaves the dotnet and node processes it started holding the ports and the files.
    if (!killTreeHard(child.pid)) child.kill("SIGTERM");
  };
  const stop = (why) => {
    if (killedFor) return;
    killedFor = why;
    kill();
    escalation = setTimeout(() => {
      log("\n  ✗ the session did not end after it was stopped — stopping it again and closing its output.");
      kill();
      child.stdout.destroy(new Error(`stopped (${why})`));
      settle?.(exitCode ?? 1);
    }, stopGraceMs);
  };
  // The dollar ceilings in lib/budget.mjs are checked between tasks, deliberately: killing
  // a task mid-edit leaves a half-finished tree. But that leaves a single attempt able to
  // spend without limit, and one did — $240 against a $50 per-task ceiling. So the CLI is
  // given the remaining budget and turn cap itself (see the call site), and this is the
  // backstop for a session that ignores them.
  const rendered = renderStream(child.stdout, write, {
    maxTurns,
    onLimit: () => stop("turns"),
    onLine,
    onEvent: (raw) => tracker.observe(raw, Date.now()),
  });
  // A deadline on the wall clock, checked often, rather than one long setTimeout: a machine
  // that sleeps through a timer's due time can wake to find it still waiting, and an attempt
  // once ran six hours past its limit. Whichever ends first — the attempt's own limit or what
  // is left of the task's — stops it. The same check closes a session that has settled.
  const deadline = Math.min(attemptEndsAt, taskEndsAt);
  const timeLimit = deadline === attemptEndsAt ? "attempt" : "task";
  const timer =
    Number.isFinite(deadline) || Number.isFinite(settleMs)
      ? setInterval(() => {
          if (killedFor) return;
          const now = Date.now();
          if (now >= deadline) {
            log(describeTimeout(timeLimit));
            stop("time");
          } else if (tracker.isSettled(now)) {
            const quietSec = Math.round((now - tracker.state().lastEventAt) / 1000);
            log(
              `\n  ✓ the session finished (final result, no background tasks) but Claude Code did not exit` +
                ` — closed it after ${quietSec}s of quiet.`,
            );
            stop("settled");
          }
        }, checkMs)
      : null;
  const status = await new Promise((res) => {
    let done = false;
    settle = (code) => {
      if (done) return;
      done = true;
      res(code);
    };
    child.on("error", () => settle(1));
    child.on("exit", (code) => {
      exitCode = code ?? 1;
      // An orphan that inherited the pipes keeps "close" from ever firing; give the output a
      // moment to drain, then stop waiting for it.
      if (!exitGrace) {
        exitGrace = setTimeout(() => {
          child.stdout.destroy(new Error("exited; its output is still held open"));
          settle(exitCode);
        }, stopGraceMs);
      }
    });
    child.on("close", (code) => settle(code ?? exitCode ?? 1));
  });
  if (timer) clearInterval(timer);
  if (escalation) clearTimeout(escalation);
  if (exitGrace) clearTimeout(exitGrace);
  for (const sig of signals) process.off(sig, forward);
  const outcome = await rendered;
  // Spread rather than cherry-pick. Naming the fields here meant that adding `subagents`
  // to renderStream left them undefined at the call site: a task passed verify, then the
  // enforcement that reads them crashed on it. A field added at one end must not need
  // remembering at the other.
  return {
    status,
    ...outcome,
    timedOut: killedFor === "time",
    timeLimit,
    settled: killedFor === "settled",
  };
}
