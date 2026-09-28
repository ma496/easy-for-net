/**
 * The pure half of the timer: names, environment and the files each platform's scheduler is
 * handed. No I/O, so what gets scheduled is unit-testable on any machine.
 */

/** A name safe for a launchd label or a Task Scheduler task, one per project. */
export function slugOf(projectName) {
  return String(projectName ?? "project").toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "") || "project";
}

/** The launchd label (macOS). */
export const launchdLabel = (projectName) => `com.${slugOf(projectName)}.queue-drain`;

/** The Task Scheduler task name (Windows). */
export const windowsTaskName = (projectName) => `${slugOf(projectName)}-queue-drain`;

/**
 * Every setting the loop depends on, as environment variables. A scheduled run inherits no
 * shell of yours and the scripts never read `.env`, so a setting not written into the
 * scheduled command is a setting the unattended runs do not have.
 */
export function scheduleEnv({ model, maxUsdPerTask, maxUsdPerDrain, maxRunsPerTask, autoPush, refuseDirty }) {
  return {
    AGENT_MODEL: model,
    AGENT_MAX_USD_PER_TASK: maxUsdPerTask,
    AGENT_MAX_USD_PER_DRAIN: maxUsdPerDrain,
    AGENT_MAX_RUNS_PER_TASK: maxRunsPerTask,
    AGENT_AUTO_PUSH: autoPush ? "1" : "0",
    AGENT_REFUSE_DIRTY_START: refuseDirty ? "1" : "0",
  };
}

/** The POSIX command line a launchd agent or a cron entry runs. */
export function posixCommand(root, env) {
  const assignments = Object.entries(env).map(([k, v]) => `${k}=${v}`).join(" ");
  return `cd ${JSON.stringify(root)} && ${assignments} npm run loop`;
}

/**
 * The batch file Task Scheduler runs on Windows. `call` because `npm` is itself a batch
 * file, and a batch file run without `call` never returns to the one that started it.
 */
export function windowsWrapper(root, env, logFile) {
  return [
    "@echo off",
    `cd /d "${root}"`,
    ...Object.entries(env).map(([k, v]) => `set "${k}=${v}"`),
    `call npm run loop >> "${logFile}" 2>&1`,
    "",
  ].join("\r\n");
}

/**
 * A launcher that runs the batch file with no window. Task Scheduler would otherwise flash a
 * console onto the desktop every interval, for as long as the timer is installed. It waits for
 * the loop to finish, so the task reads as running for as long as a drain is, and the
 * scheduler's own rule — never start a second instance of a running task — applies.
 */
export function windowsLauncher(wrapperPath) {
  return `CreateObject("WScript.Shell").Run """${wrapperPath}""", 0, True\r\n`;
}

/** `schtasks /Create` arguments: every N minutes, replacing an earlier install. */
export function schtasksCreateArgs(taskName, intervalSeconds, launcherPath) {
  const minutes = Math.max(1, Math.round(Number(intervalSeconds) / 60) || 1);
  return ["/Create", "/F", "/SC", "MINUTE", "/MO", String(minutes), "/TN", taskName, "/TR", `wscript.exe "${launcherPath}"`];
}
