/**
 * The report a gate run leaves for verify (`GATE_REPORT_FILE`): which steps ran, how long each
 * took, and which were skipped. Verify uses it twice — to say where the time went, and to know
 * whether the gate just built the service, so the live check can start it without compiling
 * the same code a second time.
 */
import { existsSync, readFileSync } from "node:fs";

/** The parsed report, or null when the gate wrote none (an older gate, or one that crashed). */
export function readGateReport(file) {
  if (!file || !existsSync(file)) return null;
  try {
    const report = JSON.parse(readFileSync(file, "utf8"));
    return Array.isArray(report?.steps) ? report : null;
  } catch {
    return null;
  }
}

/**
 * True only when the named step ran in this gate and passed. A step that was skipped, failed,
 * or is not named at all leaves the service to build itself, which is always correct, only slower.
 */
export function gateBuilt(report, stepName) {
  if (!report || !stepName) return false;
  return report.steps.some((s) => s.name === stepName && s.ok === true);
}

/** `[{ name, seconds }]` for every step that ran, seconds as numbers. */
export function stepTimings(report) {
  if (!report) return [];
  return report.steps.map((s) => ({ name: s.name, ok: s.ok === true, seconds: Number(s.seconds) || 0 }));
}
