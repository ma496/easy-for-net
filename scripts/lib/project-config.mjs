/**
 * The one file that knows anything about *this* repository.
 *
 * Everything else in `scripts/` is the engine: it queues work, spawns a headless Claude,
 * routes the diff to the agents that own it, verifies, commits, and records what happened.
 * None of that should have to know what the project builds. The project-specific half —
 * which paths belong to which specialist, what the gate command is, which checks a change
 * demands, how the app is started for a live check — lives in `agentic.config.json` at the
 * repository root, and is read here.
 *
 * That separation is the whole point of the package. Porting the framework to another
 * repository is writing one JSON file, not editing nine scripts.
 *
 * Missing config is not fatal. The defaults below describe a repository with a `gate`
 * script, no live checks and no services, which is enough for the loop to run end to end;
 * a project adds only what it actually has.
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));

/** The checkout these scripts belong to — `scripts/lib/..` twice up. */
export const REPO_ROOT = resolve(join(HERE, "..", ".."));

/**
 * Where the config is read from. `AGENTIC_CONFIG` overrides it — used by the hook tests,
 * which need a fixture config rather than the host project's own.
 */
export const CONFIG_PATH = process.env.AGENTIC_CONFIG
  ? resolve(process.env.AGENTIC_CONFIG)
  : join(REPO_ROOT, "agentic.config.json");

/**
 * What the framework assumes when the config says nothing.
 *
 * Deliberately thin: a gate command, two universal reviewers, and no live checks. A default
 * that invented a service to start or a test root that does not exist would fail on the
 * first run in a way that looks like a bug in the framework rather than a missing setting.
 */
const DEFAULTS = {
  project: {
    name: "this repository",
    /** The branch work happens on. `null` means whatever branch is checked out. */
    branch: null,
    /** Where pull requests go. `null` means `origin/HEAD`, else `main` or `master`. */
    baseBranch: null,
    /** The trailer the runner's commits end with. */
    coAuthor: "Co-Authored-By: Claude <noreply@anthropic.com>",
    /** Lines every unattended run is told before it starts — the traps a typecheck misses. */
    conventions: [],
  },
  commands: {
    gate: "npm run gate",
    verify: "npm run verify",
  },
  tests: {
    roots: ["tests"],
    pattern: "\\.test\\.(m?js|ts)$",
  },
  docs: {
    builds: "docs/builds",
    capabilities: "docs/capabilities",
    guide: "CLAUDE.md",
  },
  /**
   * Which agent owns which paths. `match` entries are regular expressions as strings,
   * anchored however you like, tested against repository-relative paths.
   */
  departments: [
    {
      agent: "qa-engineer",
      label: "QA / acceptance",
      always: true,
      phase: "review",
      order: 1,
      why: "Asks whether the brief was actually satisfied, not merely whether the build is green.",
    },
    {
      agent: "code-reviewer",
      label: "Code review",
      always: true,
      phase: "review",
      order: 2,
      why: "Sees what a green gate cannot: a check weakened to pass, a guard quietly removed.",
    },
  ],
  /** Procedures a change owes, keyed on the diff's paths or on what the brief says. */
  skills: [],
  /** Paths that belong to no department: queue bookkeeping and generated records. */
  unownedPaths: ["^\\.agent-queue/", "^\\.agent-runs/", "^docs/builds/"],
  verify: {
    /** Always runs, first. Non-zero here stops everything after it. */
    gate: "npm run gate",
    /** Extra checks, each demanded by the paths a change touched. */
    checks: [],
    /** How to start the app when a check needs it live. `null` means there is nothing to start. */
    service: null,
  },
  cycle: {
    /** Things that must be up before a cycle can observe or verify. */
    preflight: [],
    /** A command that reads production and files briefs into `specs/`. `null` to skip. */
    observe: null,
  },
  budget: {
    attempts: 3,
    maxTurns: 900,
    model: "opus",
  },
  hooks: {
    /** Extra shell patterns the Bash guard refuses, as regular expression strings. */
    deniedCommands: [],
    /** Extra path globs no agent may write to. */
    protectedPaths: [],
    /**
     * Conventions a typecheck cannot catch, reported after every edit.
     * `{ paths: [regex], forbid: regex, message: string }`
     */
    conventions: [],
  },
};

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

/**
 * Arrays replace rather than concatenate.
 *
 * A project that lists three departments means those three, not those three plus whatever
 * the defaults happened to contain. Merging arrays would make it impossible to *remove* a
 * default, which is the setting people most often need.
 */
function merge(base, override) {
  if (!isPlainObject(base) || !isPlainObject(override)) return override ?? base;
  const out = { ...base };
  for (const [key, value] of Object.entries(override)) {
    out[key] = isPlainObject(value) && isPlainObject(base[key]) ? merge(base[key], value) : value;
  }
  return out;
}

function readConfigFile() {
  if (!existsSync(CONFIG_PATH)) return {};
  try {
    return JSON.parse(readFileSync(CONFIG_PATH, "utf8"));
  } catch (err) {
    // A malformed config is worth stopping for: every downstream decision — which agents a
    // change owes, what verification it needs — would otherwise silently fall back to the
    // defaults, and a run that verified less than it should have is the failure mode this
    // whole package exists to prevent.
    console.error(`agentic.config.json could not be parsed: ${err.message}`);
    process.exit(1);
  }
}

export const config = merge(DEFAULTS, readConfigFile());

/** Compile an array of regular-expression strings, skipping anything unusable. */
export function compileRules(patterns, label = "rule") {
  return (patterns ?? [])
    .map((p) => {
      if (p instanceof RegExp) return p;
      try {
        return new RegExp(p);
      } catch (err) {
        console.error(`Ignoring an invalid ${label} in agentic.config.json: ${p} (${err.message})`);
        return null;
      }
    })
    .filter(Boolean);
}

/** Split a configured command string into something `spawnSync` can run. */
export { commandParts } from "./proc.mjs";

/** Substitute `{{name}}` placeholders — used for `{{api}}` in live-check commands. */
export function fill(template, values) {
  return String(template ?? "").replace(/\{\{(\w+)\}\}/g, (whole, key) =>
    Object.prototype.hasOwnProperty.call(values ?? {}, key) ? String(values[key]) : whole,
  );
}

function git(...args) {
  const r = spawnSync("git", args, { cwd: REPO_ROOT, encoding: "utf8", windowsHide: true });
  return r.status === 0 ? r.stdout.trim() : "";
}

/**
 * The branch a configured value names, or the one checked out when it names none. Pure
 * apart from the `current` fallback, so the resolution rule is testable without a repo.
 */
export function resolveWorkBranch(configured, current) {
  return configured || current || "main";
}

/**
 * Where pull requests go: the configured branch, else whatever `origin/HEAD` points at,
 * else `main` or `master` — whichever of them exists.
 */
export function resolveBaseBranch(configured, { originHead = "", existing = [] } = {}) {
  if (configured) return configured;
  const head = originHead.replace(/^refs\/remotes\/origin\//, "").replace(/^origin\//, "");
  if (head) return head;
  return existing.find((b) => b === "main" || b === "master") ?? "main";
}

/** The branch work happens on. Everything refuses to build anywhere else. */
export const WORK_BRANCH = resolveWorkBranch(config.project.branch, git("rev-parse", "--abbrev-ref", "HEAD"));

/** The branch pull requests target and "shipped" is measured against. */
export const BASE_BRANCH = resolveBaseBranch(config.project.baseBranch, {
  originHead: git("symbolic-ref", "--quiet", "refs/remotes/origin/HEAD"),
  existing: git("branch", "--list", "main", "master", "--format=%(refname:short)")
    .split("\n")
    .map((b) => b.trim())
    .filter(Boolean),
});

/** The project's own name, for briefs and launchd labels. */
export const PROJECT_NAME = config.project.name || "this repository";
