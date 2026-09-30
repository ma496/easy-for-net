/**
 * Which departments a change belongs to, and therefore which agents must have worked on it.
 *
 * A real team does not put every specialist on every ticket, and neither does this. An
 * agent is required for the work it *owns* — schema work needs the data engineer, a change
 * under the front-end's paths needs the front-end engineer and the designer — and is not
 * invoked for work it has nothing to say about. Requiring all of them on a one-line typo
 * fix would make the rule so expensive that the first person it annoyed would delete it,
 * which leaves the system worse off than a rule that is merely strict.
 *
 * Two departments are universal, because there is no change they have nothing to say about:
 * the reviewer, and the acceptance check that asks whether the brief was actually satisfied.
 *
 * The required set is derived from the paths in the finished diff rather than from the
 * brief's prose. A brief describes intent; the diff is what happened. A task that swore it
 * would not touch the schema and did must still answer to the data engineer.
 *
 * The *map* — which agent owns which paths — is project-specific and lives in
 * `agentic.config.json`. Everything below is the logic that reads it, and is the same in
 * every repository.
 *
 * Pure functions over path strings: no git, no filesystem, no network.
 */
import { compileRules, config } from "./project-config.mjs";

/**
 * Every department, the paths it owns, and why it is the one that must look.
 *
 * Order matters only for display. `always: true` marks the universal set — the reviewer and
 * the acceptance check, the two that have something to say about any change at all.
 * Everyone else is owed by the work they own.
 */
export const DEPARTMENTS = (config.departments ?? []).map((d) => ({
  ...d,
  match: compileRules(d.match, `department match for ${d.agent}`),
  exceptWhenOnly: compileRules(d.exceptWhenOnly, `department exceptWhenOnly for ${d.agent}`),
}));

/** Paths that belong to no department: queue bookkeeping and generated records. */
const UNOWNED = compileRules(config.unownedPaths, "unownedPaths entry");

/**
 * Whether a universal department sits this change out: `exceptWhenOnly` names the paths it
 * has nothing to add to, and every owned path in the diff is one of them. A change that owns
 * no path at all is not excused — there is nothing to be excused *from*, and the runner
 * refuses an empty diff before it asks anyway.
 */
function excused(dept, owned) {
  return dept.exceptWhenOnly.length > 0 && owned.length > 0 && owned.every((p) => dept.exceptWhenOnly.some((re) => re.test(p)));
}

export function requiredAgents(paths) {
  const owned = (paths ?? []).filter((p) => p && !UNOWNED.some((re) => re.test(p)));
  const required = [];

  for (const dept of DEPARTMENTS) {
    if (dept.always) {
      if (!excused(dept, owned)) required.push(dept.agent);
      continue;
    }
    if (owned.some((p) => dept.match.some((re) => re.test(p)))) required.push(dept.agent);
  }
  return [...new Set(required)];
}

/** The departments a change belongs to, with their reasons — for explaining a refusal. */
export function departmentsFor(paths) {
  const agents = new Set(requiredAgents(paths));
  return DEPARTMENTS.filter((d) => agents.has(d.agent));
}

/**
 * What a run still owes, given what it actually delegated to.
 *
 * Comparison is on the delegations observed in the run's stream, never on its own account
 * of what it did — an agent that says it consulted the migrator and did not is exactly the
 * case this exists to catch.
 */
export function missingAgents(paths, delegated) {
  const seen = new Set((delegated ?? []).map((d) => String(d).trim()));
  return requiredAgents(paths).filter((agent) => !seen.has(agent));
}

/**
 * The delegations of earlier attempts that still count for this one.
 *
 * A retry keeps what earlier attempts of the same run wrote, so a specialist who designed or
 * built part of it has already done its part: asking it again only because the retry did not
 * happen to call it cost a whole extra attempt and verify, once, for work that was finished.
 * A verdict is different — it was given on a diff the retry may since have changed — so the
 * review requirement is checked against the current attempt alone (see `explainMissing`).
 */
export function carriedDelegations(prior) {
  const writers = new Set(DEPARTMENTS.filter((d) => d.phase === "build" || d.phase === "design").map((d) => d.agent));
  return (prior ?? []).map((d) => String(d).trim()).filter((agent) => writers.has(agent));
}

/**
 * A human-readable explanation of why each missing agent was required.
 *
 * `current` is the delegations of the attempt being judged, when `delegated` also carries
 * earlier attempts' writers: an agent with a review role must appear in `current`, since only
 * a review of this diff counts. Omitted, the two are the same list.
 */
export function explainMissing(paths, delegated, current = delegated) {
  const reviewers = new Set(DEPARTMENTS.filter((d) => d.phase === "review").map((d) => d.agent));
  const missing = new Set([
    ...missingAgents(paths, delegated),
    ...missingAgents(paths, current).filter((agent) => reviewers.has(agent)),
  ]);
  return DEPARTMENTS.filter((d) => missing.has(d.agent)).map((d) => ({
    agent: d.agent,
    label: d.label,
    why: d.why,
    triggeredBy: d.always
      ? ["every change"]
      : (paths ?? []).filter((p) => d.match.some((re) => re.test(p))).slice(0, 4),
  }));
}

/**
 * The order the required agents must run in.
 *
 * Not an arbitrary preference. Everything that *writes* finishes before anything that
 * *judges*, because a reviewer reading a half-finished diff reports findings the next edit
 * would have removed anyway. Then the reviewers run cheapest-question-first:
 *
 *   1  qa-engineer        did it do what the brief asked? if not, nothing else matters
 *   2  security-reviewer  is it dangerous? that blocks regardless of anything else
 *   3  designer           does it look right? blocking for a page, silent elsewhere
 *   4  code-reviewer      the holistic sign-off, last so it can see what the others said
 */
export function expectedSequence(paths) {
  const required = new Set(requiredAgents(paths));
  const design = DEPARTMENTS.filter((d) => required.has(d.agent) && d.phase === "design");
  const build = DEPARTMENTS.filter((d) => required.has(d.agent) && d.phase === "build");
  const review = DEPARTMENTS.filter((d) => required.has(d.agent) && d.phase === "review")
    .sort((a, b) => a.order - b.order);
  return [...design, ...build, ...review].map((d) => ({
    agent: d.agent,
    label: d.label,
    phase: d.phase,
    tier: d.order ?? 0,
  }));
}

/**
 * Whether the delegations that actually happened respect that order.
 *
 * A reviewer returning CHANGES NEEDED means the run fixes the finding and reviews again, so
 * an agent legitimately appears more than once. Two rules survive that:
 *
 *   - every build agent's FIRST delegation precedes the first review delegation; a
 *     specialist called in after the reviewers is a specialist who did not do the work.
 *   - the review agents' LAST delegations are in declared order; the final pass is the one
 *     whose verdict stands.
 *
 * Returns a list of plain-language problems, empty when the order held.
 */
export function sequenceProblems(paths, delegated) {
  const seq = expectedSequence(paths);
  const buildAgents = new Set(seq.filter((d) => d.phase === "build").map((d) => d.agent));
  const reviewOrder = seq.filter((d) => d.phase === "review").map((d) => d.agent);

  const calls = (delegated ?? []).map((d) => String(d).trim());
  const firstAt = (agent) => calls.indexOf(agent);
  const lastAt = (agent) => calls.lastIndexOf(agent);

  const problems = [];

  // Design comes before code, or it is not design — it is a second opinion on something
  // already written, which is what this phase exists to stop being. The check is on the
  // designer's FIRST call, because it appears again later as a reviewer and that repeat is
  // expected.
  const designAgents = new Set(seq.filter((d) => d.phase === "design").map((d) => d.agent));
  for (const agent of designAgents) {
    const designedAt = firstAt(agent);
    const firstBuild = [...buildAgents].map(firstAt).filter((i) => i >= 0).sort((a, b) => a - b)[0];
    if (firstBuild === undefined) continue;
    if (designedAt < 0) {
      problems.push(
        `${agent} never designed this screen. On a change people will look at, what it ` +
          "should be is decided before it is written — afterwards, acting on that decision " +
          "means rebuilding what already exists.",
      );
    } else if (designedAt > firstBuild) {
      problems.push(
        `${agent} was asked to design after the code was already being written. The design ` +
          "phase runs first; reviewing the result is its second, later pass.",
      );
    }
  }

  // "When did reviewing start?" cannot be asked of an agent that also designs: its first
  // call is the design pass, which is supposed to come before the writers. Only the agents
  // that exist solely to judge can date the start of judging.
  const firstReviewIndex = reviewOrder
    .filter((agent) => !designAgents.has(agent))
    .map(firstAt)
    .filter((i) => i >= 0)
    .reduce((min, i) => (min < 0 || i < min ? i : min), -1);

  if (firstReviewIndex >= 0) {
    for (const agent of buildAgents) {
      const at = firstAt(agent);
      if (at >= 0 && at > firstReviewIndex) {
        problems.push(
          `${agent} was called after review had already started — the specialists that write ` +
            "must finish before anything judges the result.",
        );
      }
    }
  }

  // Reviewers sharing an `order` ask independent questions and may run in any sequence, or
  // at the same time — waiting for each in turn spends wall-clock for nothing. Only a jump
  // between *tiers* is a violation.
  const tiers = [...new Set(seq.filter((d) => d.phase === "review").map((d) => d.tier))];
  let previousTierLast = -1;
  let previousTier = null;
  for (const tier of tiers) {
    const inTier = seq.filter((d) => d.phase === "review" && d.tier === tier).map((d) => d.agent);
    const seen = inTier.map(lastAt).filter((i) => i >= 0);
    if (seen.length === 0) continue;
    if (Math.min(...seen) < previousTierLast) {
      problems.push(
        `${inTier.join(" / ")} ran before ${previousTier} finished, but the review order is ` +
          `${tiers.map((t) => seq.filter((d) => d.tier === t).map((d) => d.agent).join(" / ")).join(" → ")}.`,
      );
    }
    previousTierLast = Math.max(...seen);
    previousTier = inTier.join(" / ");
  }
  return problems;
}

/**
 * Which written procedures a change owes, and why.
 *
 * A skill is not an agent — it is a procedure the session reads instead of reasoning from
 * scratch. Leaving that to judgement means the one run that most needs a procedure — the
 * one about to blame a prompt for what is really a cache hit — is exactly the run that will
 * not think to load it.
 *
 * Required only where the trigger is unambiguous. A skill demanded when it does not apply
 * is noise, and noise is how a rule gets deleted by the first person it annoys — the same
 * argument that keeps the department set narrow.
 *
 * A skill entry keys off the diff (`match`) or off what the brief says the problem is
 * (`brief`); both live in `agentic.config.json`.
 */
export const SKILLS = (config.skills ?? []).map((s) => ({
  ...s,
  match: s.match ? compileRules(s.match, `skill match for ${s.skill}`) : null,
  brief: s.brief ? compileRules([s.brief], `skill brief pattern for ${s.skill}`)[0] : null,
}));

/** The skills this change owes, given its paths and the text of its brief. */
export function requiredSkills(paths, briefText) {
  const owned = (paths ?? []).filter((p) => p && !UNOWNED.some((re) => re.test(p)));
  return SKILLS.filter(
    (s) =>
      (s.match && owned.some((p) => s.match.some((re) => re.test(p)))) ||
      (s.brief && s.brief.test(briefText ?? "")),
  ).map((s) => s.skill);
}

/** What it owes and did not load, with the reason each was required. */
export function explainMissingSkills(paths, briefText, loaded) {
  const seen = new Set((loaded ?? []).map((l) => String(l).trim()));
  const owed = new Set(requiredSkills(paths, briefText).filter((s) => !seen.has(s)));
  return SKILLS.filter((s) => owed.has(s.skill)).map((s) => ({
    skill: s.skill,
    why: s.why,
    triggeredBy: s.brief
      ? ["what the brief describes"]
      : (paths ?? []).filter((p) => s.match.some((re) => re.test(p))).slice(0, 3),
  }));
}
