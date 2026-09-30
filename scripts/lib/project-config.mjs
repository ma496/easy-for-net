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
    /** A trailer the runner's commits end with. `null` adds none, leaving the author line — each developer's own git user.name/user.email — as the only identity. */
    coAuthor: null,
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
    /** The parent session's turns per attempt, counted as real API turns (see agent-run.mjs). */
    maxTurns: 350,
    /** Wall-clock minutes per attempt, for a session that hangs and so reports neither turns nor cost. */
    maxMinutesPerAttempt: 120,
    /** `opus` is the alias for the newest Opus. `inherit` passes no `--model` at all. */
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

const isPositiveInt = (n) => Number.isInteger(n) && n > 0;

/** Every entry of `list` that is not a usable regular expression, as a problem message. */
function regexProblems(list, where, flags = "") {
  if (list === undefined || list === null) return [];
  if (!Array.isArray(list)) return [`${where} must be an array of regular expressions`];
  const problems = [];
  for (const p of list) {
    try {
      new RegExp(p, flags);
    } catch (err) {
      problems.push(`${where}: invalid regular expression ${JSON.stringify(p)} (${err.message})`);
    }
  }
  return problems;
}

/**
 * Everything wrong with a merged config, one message per problem.
 *
 * Parsing alone let through the values that do the most damage precisely because nothing
 * notices them: `budget.attempts: 0` or `"three"` meant the attempt loop never ran, the run
 * exited as "account blocked", the drain requeued it, and it went round every minute for
 * ever; an invalid department regex was logged once and dropped, silently removing that
 * reviewer's requirement from every unattended run. Both are now refusals at startup.
 */
export function validateConfig(cfg) {
  const problems = [];
  const budget = cfg.budget ?? {};
  for (const key of ["attempts", "maxTurns", "maxMinutesPerAttempt"]) {
    if (!isPositiveInt(budget[key])) {
      problems.push(`budget.${key} must be a positive integer (got ${JSON.stringify(budget[key])})`);
    }
  }
  if (typeof budget.model !== "string" || !budget.model.trim()) {
    problems.push("budget.model must be a model name or alias");
  }
  for (const key of ["gate", "verify"]) {
    if (typeof cfg.commands?.[key] !== "string" || !cfg.commands[key].trim()) {
      problems.push(`commands.${key} must be a command`);
    }
  }

  if (!Array.isArray(cfg.departments)) problems.push("departments must be an array");
  const agents = new Set();
  for (const [i, d] of (Array.isArray(cfg.departments) ? cfg.departments : []).entries()) {
    const where = `departments[${i}]${d?.agent ? ` (${d.agent})` : ""}`;
    // An agent may appear once per phase — a designer that briefs first and reviews after is
    // two entries by design — but twice in one phase is a copy-paste that shadows itself.
    const key = `${d?.agent}\0${d?.phase ?? "review"}`;
    if (typeof d?.agent !== "string" || !d.agent) problems.push(`${where}.agent is required`);
    else if (agents.has(key)) problems.push(`${where}: ${d.agent} is listed twice in the ${d.phase ?? "review"} phase`);
    else agents.add(key);
    if (!d?.always && !Array.isArray(d?.match)) problems.push(`${where} needs \`match\` paths or \`always: true\``);
    problems.push(...regexProblems(d?.match, `${where}.match`));
    problems.push(...regexProblems(d?.exceptWhenOnly, `${where}.exceptWhenOnly`));
    if (d?.exceptWhenOnly !== undefined && !d?.always) {
      problems.push(`${where}.exceptWhenOnly only applies to an \`always: true\` department`);
    }
    if (d?.phase !== undefined && !["design", "build", "review"].includes(d.phase)) {
      problems.push(`${where}.phase must be design, build or review (got ${JSON.stringify(d.phase)})`);
    }
  }

  for (const [i, sk] of (Array.isArray(cfg.skills) ? cfg.skills : []).entries()) {
    const where = `skills[${i}]${sk?.skill ? ` (${sk.skill})` : ""}`;
    if (typeof sk?.skill !== "string" || !sk.skill) problems.push(`${where}.skill is required`);
    problems.push(...regexProblems(sk?.match, `${where}.match`));
    if (sk?.brief !== undefined) problems.push(...regexProblems([sk.brief], `${where}.brief`));
  }

  problems.push(...regexProblems(cfg.unownedPaths, "unownedPaths"));
  for (const [i, c] of (cfg.verify?.checks ?? []).entries()) {
    problems.push(...regexProblems(c?.when, `verify.checks[${i}]${c?.name ? ` (${c.name})` : ""}.when`));
  }
  problems.push(...regexProblems(cfg.hooks?.protectedPaths, "hooks.protectedPaths"));
  for (const [i, rule] of (cfg.hooks?.deniedCommands ?? []).entries()) {
    const pattern = typeof rule === "string" ? rule : rule?.pattern;
    const flags = typeof rule === "object" ? (rule?.flags ?? "") : "";
    problems.push(...regexProblems([pattern], `hooks.deniedCommands[${i}]`, flags));
  }
  for (const [i, rule] of (cfg.hooks?.conventions ?? []).entries()) {
    problems.push(...regexProblems(rule?.paths, `hooks.conventions[${i}].paths`));
    problems.push(...regexProblems(rule?.except, `hooks.conventions[${i}].except`));
    problems.push(...regexProblems([rule?.forbid], `hooks.conventions[${i}].forbid`, rule?.flags ?? "g"));
  }
  if (!Array.isArray(cfg.tests?.roots)) problems.push("tests.roots must be an array");
  problems.push(...regexProblems([cfg.tests?.pattern], "tests.pattern"));
  return problems;
}

/**
 * Refuse to run on an invalid config. Called by the engine's entry points — not on import,
 * because the hooks import this module too, and a hook that exits on a config typo stops
 * guarding rather than stopping the session. The unit suite holds the committed config valid.
 */
export function assertValidConfig(cfg = config) {
  const problems = validateConfig(cfg);
  if (problems.length === 0) return;
  console.error(`agentic.config.json is invalid:\n${problems.map((p) => `  - ${p}`).join("\n")}`);
  process.exit(1);
}

/**
 * A positive integer from an environment variable or flag, else the fallback. A value that
 * is set but is not one stops the run: `AGENT_MAX_RUNS_PER_TASK=abc` read as NaN, and every
 * comparison with NaN is false, so the limit it named silently stopped existing.
 */
export function positiveInt(raw, fallback, label = "value") {
  if (raw === undefined || raw === null || String(raw).trim() === "") return fallback;
  const n = Number(raw);
  if (isPositiveInt(n)) return n;
  console.error(`${label} must be a positive integer (got ${JSON.stringify(raw)}).`);
  process.exit(1);
}

/**
 * The model a session runs on: the environment, then a flag, then `budget.model`. Blank
 * means unset. `inherit` is returned as-is; `modelArgs` turns it into no `--model` at all.
 *
 * One rule for every place that starts a session — the runner, the planner, the timer — so
 * the planner can no longer be the one call quietly left on the CLI's default.
 */
export function resolveModel({ env, arg, configured = config.budget.model } = {}) {
  for (const v of [env, arg, configured]) {
    if (typeof v === "string" && v.trim()) return v.trim();
  }
  return "opus";
}

/** The `--model` arguments for a resolved model — none for `inherit`. */
export const modelArgs = (model) => (model && model !== "inherit" ? ["--model", model] : []);

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
