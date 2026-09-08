export const meta = {
  name: 'spec-verify',
  description: 'Trace every EARS acceptance criterion to code and test evidence, adversarially refute each satisfied claim, and append the remaining work to tasks.md',
  whenToUse: 'Fourth stage of spec-driven development. Produces verification.md, a traceability matrix, and a new round of tasks for whatever is not actually done.',
  phases: [
    { title: 'Load', detail: 'read spec.md and tasks.md into criteria and task state' },
    { title: 'Trace', detail: 'per criterion: hunt code and test evidence, then refute the satisfied claims' },
    { title: 'Critique', detail: 'cross-cutting, test-quality, spec-drift and completeness critics' },
    { title: 'Emit', detail: 'write verification.md and append a new task round to tasks.md' },
  ],
}

// =============================================================================
// STAGE 4 OF 4 - the code is measured against the specification.
//
//   /specify -> /plan -> /implement -> /verify -> spec-verify
//
// What it does
//   Takes each acceptance criterion one at a time, hunts for the code and the test
//   that implement it, and then tries to knock the answer down. A criterion is only
//   "satisfied" if a tracer found both kinds of evidence AND independent refuters
//   failed to break the claim. Four critics then look for what the tracers could not
//   see at all, and everything still outstanding is appended to tasks.md as a fresh
//   numbered round.
//
// Why refuters rather than a second opinion
//   Asking "is this right?" gets agreement; asking "prove this wrong" gets scrutiny.
//   Each refuter is given a different lens - is the code path even reachable, would the
//   cited test fail if the behaviour were deleted, are the edge cases of the condition
//   handled - because a claim can be wrong in more than one way, and three identical
//   skeptics all miss the same thing.
//
// This is a convergence loop, not a report
//   Unmet criteria come back as tasks. Run spec-implement on those ids, then run this
//   again. Stop when it returns converged. Pass autoFix to let it drive that loop
//   itself via workflow(), though nesting is one level deep only - if this workflow was
//   itself invoked from another one, that call throws and is caught below.
//
// args
//   {specDir: string}     required.
//   {refuters?: number}   skeptics per satisfied claim, 1 to 3.
//   {maxRounds?: number}  convergence rounds when autoFix is on.
//   {autoFix?: boolean}   run spec-implement inline on the emitted tasks.
//   {acLimit?: number}    safety cap on criteria verified per run (default 60).
//
// Returns
//   {ok, converged, satisfied, partial, missing, criticFindings, specDrift,
//    newTaskIds, matrixPath, acsVerified, acsTotal}
//   acsVerified below acsTotal means the run was capped and some criteria were never
//   checked - the caller is expected to say so rather than report a clean result.
//
// Files written
//   verification.md, and an appended section of tasks.md. Never application code:
//   a verifier that can edit the code under test is not a verifier.
//
// Cost
//   Roughly 76 subagents for 30 criteria at two refuters.
// =============================================================================

// ---------------------------------------------------------------- arguments
const SPEC_DIR = (args && args.specDir) || (typeof args === 'string' ? args : '')
if (!SPEC_DIR) {
  log('spec-verify requires args {specDir: "specs/<NNN-slug>"}.')
  return { ok: false, error: 'missing-specDir' }
}
const SPEC_PATH = `${SPEC_DIR}/spec.md`
const TASKS_PATH = `${SPEC_DIR}/tasks.md`
const PLAN_PATH = `${SPEC_DIR}/plan.md`
const MATRIX_PATH = `${SPEC_DIR}/verification.md`

// Depth scales the run to the token budget set for this turn; budget.total is null when
// none was set. MAJORITY is derived from REFUTERS so the vote threshold stays right at
// any panel size: 2 of 3, or 1 of 2 - a deliberately low bar, because a claim that even
// one focused skeptic can break was not solid evidence to begin with.
const DEPTH = budget.total ? Math.max(1, Math.min(3, Math.floor(budget.total / 300000))) : 1
const REFUTERS = Math.max(1, Math.min(3, (args && args.refuters) || (DEPTH >= 2 ? 3 : 2)))
const MAJORITY = Math.floor(REFUTERS / 2) + 1
const MAX_ROUNDS = Math.max(1, Math.min(3, (args && args.maxRounds) || (DEPTH >= 2 ? 3 : 2)))
const AC_LIMIT = Math.max(1, (args && args.acLimit) || 60)
const AUTO_FIX = Boolean(args && args.autoFix)

// Prepended to every prompt. Points at markdown rather than restating conventions:
// this file ships into scaffolded projects byte-for-byte, and only markdown gets
// namespace-rewritten on the way.
const ORIENT = [
  'Read CLAUDE.md at the repo root before judging anything - it is the authority on layout, conventions and the build and test commands.',
  'Read .claude/skills/spec-driven/SKILL.md for the completeness checklist, and consult the relevant guide under .claude/skills when a convention is in question.',
].join(' ')

// ---------------------------------------------------------------- schemas
// A schema on agent() forces a StructuredOutput tool call and returns a validated
// object. Note that TRACE_SCHEMA keeps code evidence and test evidence in separate
// arrays: the distinction is the whole point, since code with no test that would fail
// without it is not a satisfied criterion, only an unverified one.
const LOAD_SCHEMA = {
  type: 'object',
  properties: {
    acs: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          text: { type: 'string' },
          ears: { type: 'string' },
          area: { type: 'string' },
        },
        required: ['id', 'text'],
      },
    },
    taskIds: { type: 'array', items: { type: 'string' } },
    openTaskIds: { type: 'array', items: { type: 'string' } },
    highestTaskNumber: { type: 'number' },
  },
  required: ['acs', 'taskIds', 'highestTaskNumber'],
}

const TRACE_SCHEMA = {
  type: 'object',
  properties: {
    acId: { type: 'string' },
    verdict: { type: 'string', enum: ['satisfied', 'partial', 'missing'] },
    codeEvidence: {
      type: 'array',
      items: {
        type: 'object',
        properties: { path: { type: 'string' }, symbol: { type: 'string' }, why: { type: 'string' } },
        required: ['path', 'why'],
      },
    },
    testEvidence: {
      type: 'array',
      items: {
        type: 'object',
        properties: { path: { type: 'string' }, test: { type: 'string' }, why: { type: 'string' } },
        required: ['path', 'why'],
      },
    },
    gap: { type: 'string', description: 'what is missing, when not satisfied' },
  },
  required: ['acId', 'verdict', 'codeEvidence', 'testEvidence'],
}

const REFUTE_SCHEMA = {
  type: 'object',
  properties: {
    acId: { type: 'string' },
    refuted: { type: 'boolean' },
    reason: { type: 'string' },
    residualGap: { type: 'string' },
  },
  required: ['acId', 'refuted', 'reason'],
}

const CRITIC_SCHEMA = {
  type: 'object',
  properties: {
    critic: { type: 'string' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: {
            type: 'string',
            enum: ['missing-work', 'weak-test', 'spec-drift', 'convention-violation', 'unverified-claim'],
          },
          acId: { type: 'string' },
          where: { type: 'string' },
          problem: { type: 'string' },
          fix: { type: 'string' },
          severity: { type: 'string', enum: ['must-fix', 'nice-to-have'] },
        },
        required: ['kind', 'problem', 'fix', 'severity'],
      },
    },
  },
  required: ['critic', 'findings'],
}

const EMIT_SCHEMA = {
  type: 'object',
  properties: {
    matrixPath: { type: 'string' },
    tasksAppended: { type: 'array', items: { type: 'string' } },
    stillOpenAcs: { type: 'array', items: { type: 'string' } },
  },
  required: ['matrixPath', 'tasksAppended'],
}

// index-driven lenses, never random, so a resumed run replays identically
const REFUTE_LENSES = [
  'Execution reality: prove the code path the tracer cited is actually reachable at runtime - the endpoint is registered in a route group, the permission it needs exists and is declared in a provider, the service is registered by the feature module, the web route is reachable and guarded. If any link in that chain is missing, the criterion is NOT satisfied.',
  'Test reality: read the cited tests. Would any of them FAIL if the behaviour were removed or inverted? A test that only asserts a success status, or that never exercises the criterion condition, is not evidence. With no such test the criterion is at best partial.',
  'Boundary reality: the criterion has a condition - when, while, where or if. Prove the implementation handles the negative and edge cases of that condition: unauthorized caller, missing or soft-deleted record, empty result, invalid input, oversized input, and the resulting user-visible message being translated rather than hard-coded.',
]

function trim(list, n, what) {
  const arr = Array.isArray(list) ? list : []
  if (arr.length > n) log(`Dropped ${arr.length - n} of ${arr.length} ${what} when building a prompt (kept ${n}).`)
  return arr.slice(0, n)
}

// ---------------------------------------------------------------- Load
// highestTaskNumber matters more than it looks: the new round of tasks is numbered from
// it, so ids stay unique and nothing already in tasks.md gets renumbered. Renumbering
// would break the traceability matrix and every cached agent result on resume.
phase('Load')
const loaded = await agent(
  [
    ORIENT,
    `Read ${SPEC_PATH} and ${TASKS_PATH}.`,
    'Return every acceptance criterion with its exact id and text, every task id, the ids of tasks whose checkbox is unticked,',
    'and the highest task number currently used as a number - 23 for T-023.',
    'Do not modify any file.',
  ].join('\n'),
  { label: 'load spec and tasks', schema: LOAD_SCHEMA, effort: 'low' }
)
if (!loaded || !loaded.acs || !loaded.acs.length) {
  return { ok: false, error: 'cannot-read-spec-or-tasks', specDir: SPEC_DIR }
}
if (loaded.openTaskIds && loaded.openTaskIds.length) {
  log(`Note: ${loaded.openTaskIds.length} task(s) in tasks.md are still unticked: ${loaded.openTaskIds.join(', ')}`)
}

let acs = loaded.acs
if (acs.length > AC_LIMIT) {
  log(`The spec has ${acs.length} acceptance criteria; verifying the first ${AC_LIMIT}. Re-run with args.acLimit raised to cover the rest.`)
  acs = acs.slice(0, AC_LIMIT)
}
log(`Tracing ${acs.length} acceptance criteria with ${REFUTERS} refuter(s) each.`)

// ---------------------------------------------------------------- Trace
phase('Trace')
// pipeline(), not parallel(): each criterion is traced and refuted independently, so
// criterion 4 is being traced while criterion 2 is being refuted. With thirty criteria
// a barrier here would idle every fast trace behind the slowest one for two whole
// stages. Only claims that came back "satisfied" are sent to refuters - there is
// nothing to refute about an admission that something is missing.
const traced = await pipeline(
  acs,
  (ac) =>
    agent(
      [
        ORIENT,
        `Determine whether acceptance criterion ${ac.id} of ${SPEC_PATH} is implemented in this repository.`,
        `Criterion: "${ac.text}"`,
        `${PLAN_PATH} and the contract documents in ${SPEC_DIR} describe how it was meant to be built - read them, then verify against the ACTUAL source.`,
        'Search the real source tree under src/backend and src/frontend. Cite exact repo-relative paths and symbol names you opened.',
        'Separate code evidence from test evidence. "satisfied" requires BOTH: code that implements it, and at least one test that would fail without it.',
        'If you cannot find evidence, say "missing" and describe the gap. Do not guess, and do not edit any file.',
      ].join('\n'),
      { label: `trace ${ac.id}`, phase: 'Trace', schema: TRACE_SCHEMA }
    ),
  (trace, ac) => {
    if (!trace) {
      return { acId: ac.id, verdict: 'missing', codeEvidence: [], testEvidence: [], gap: 'the tracer produced no result' }
    }
    if (trace.verdict !== 'satisfied') return trace
    // inner barrier: majority rule needs every vote for this criterion
    return parallel(
      Array.from({ length: REFUTERS }, (unused, k) => () =>
        agent(
          [
            ORIENT,
            `Another agent claims acceptance criterion ${ac.id} is fully satisfied. Your job is to REFUTE that claim.`,
            `Criterion: "${ac.text}"`,
            `Your lens: ${REFUTE_LENSES[k % REFUTE_LENSES.length]}`,
            'Open every file the claim cites and read the surrounding code - do not take it at face value.',
            'Return refuted=true unless the claim survives your lens completely. When uncertain, refute and say what you could not confirm.',
            'Do not edit any file.',
            '',
            'The claim:',
            JSON.stringify(trace),
          ].join('\n'),
          { label: `refute ${ac.id} #${k + 1}`, phase: 'Trace', schema: REFUTE_SCHEMA }
        )
      )
    ).then((votes) => {
      const good = votes.filter(Boolean)
      const against = good.filter((v) => v.refuted)
      if (good.length < REFUTERS) log(`${ac.id}: only ${good.length}/${REFUTERS} refuters returned; judged on the votes received.`)
      if (against.length >= MAJORITY) {
        log(`${ac.id}: downgraded from satisfied to partial by ${against.length}/${good.length} refuters.`)
        return {
          ...trace,
          verdict: 'partial',
          gap: against.map((v) => v.residualGap || v.reason).filter(Boolean).join(' | ') || 'refuted by independent review',
        }
      }
      return trace
    })
  }
)

const results = traced.filter(Boolean)
const satisfied = results.filter((r) => r.verdict === 'satisfied')
const partial = results.filter((r) => r.verdict === 'partial')
const missing = results.filter((r) => r.verdict === 'missing')
log(`Trace complete: ${satisfied.length} satisfied, ${partial.length} partial, ${missing.length} missing.`)

// ---------------------------------------------------------------- Critique
// The tracers answer "is this criterion met?", one criterion at a time. That leaves
// three blind spots, one critic each: work that spans criteria (a permission mirrored
// on only one side, a locale key added to only one file), tests that pass without
// proving anything, and code that no criterion ever asked for. The fourth critic asks
// what this verification itself failed to check.
phase('Critique')
const CRITICS = [
  {
    key: 'cross-cutting',
    ask: [
      'Audit the cross-cutting concerns end to end for this feature, independently of what the tracers reported.',
      'Check that every new permission constant exists on the API side, is declared in the owning feature provider, guards every endpoint that needs it,',
      'and is mirrored in the web permission map, the route guards and the navigation and search entries.',
      'Check that every user-visible string goes through translation and that every key exists in every locale file the project ships.',
      'Check that persisted entities carry the audit and soft-delete behaviour the spec implies, that a migration exists for the schema change, and that the DbSet is registered.',
      'Check that every failure path raises a defined error code the web side can translate.',
      'Check that nothing violates the feature-isolation rules enforced by the architecture tests.',
    ].join(' '),
  },
  {
    key: 'test-quality',
    ask: [
      'Audit the tests added for this feature. For each, ask whether it would fail if the behaviour were removed.',
      'Flag smoke tests masquerading as coverage, tests that assert only a status code, tests that depend on global database state they did not create,',
      'unwanted-behaviour criteria with no negative test, and criteria whose only test is a happy path.',
    ].join(' '),
  },
  {
    key: 'spec-drift',
    ask: [
      'Find code written for this feature that NO acceptance criterion asked for, and behaviour the spec asked for that was quietly reinterpreted.',
      'Inspect what actually changed in the working tree (git status, git diff --stat, git diff).',
      'Report each as spec-drift with a recommendation: remove it, or amend the spec.',
    ].join(' '),
  },
  {
    key: 'completeness',
    ask: [
      'You are the completeness critic. What did this verification MISS?',
      'A criterion nobody traced; a file changed by the implementation that nobody reviewed; a claim accepted on a single piece of evidence;',
      'a modality never run - nobody executed the build or the tests, nobody looked at the running web app, nobody checked that the migration applies.',
      'Report each as unverified-claim, naming the specific check that should be run.',
    ].join(' '),
  },
]

// Barrier: the emitter must merge findings that one edit would fix into a single
// task rather than three, which needs every critic's output at once.
const critiques = await parallel(
  CRITICS.map((c) => () =>
    agent(
      [
        ORIENT,
        `Read ${SPEC_PATH}, ${PLAN_PATH}, ${TASKS_PATH} and the contract documents in ${SPEC_DIR}, then inspect the actual source.`,
        `Your critic role is "${c.key}". ${c.ask}`,
        'Report only your own dimension. Every finding needs a concrete, actionable fix and the file it applies to. Do not edit any file.',
        '',
        'The verdicts the tracers reached, for context - you are not bound by them:',
        JSON.stringify(results.map((r) => ({ acId: r.acId, verdict: r.verdict, gap: r.gap }))),
      ].join('\n'),
      { label: `critic: ${c.key}`, phase: 'Critique', schema: CRITIC_SCHEMA }
    )
  )
)

const criticFindings = critiques.filter(Boolean).flatMap((c) => (c.findings || []).map((f) => ({ critic: c.critic, ...f })))
const mustFix = criticFindings.filter((f) => f.severity === 'must-fix')
log(`Critics returned ${criticFindings.length} finding(s), ${mustFix.length} must-fix.`)

// ---------------------------------------------------------------- Emit
// Two outputs: verification.md as the human-readable traceability matrix, and a new
// round of tasks appended to tasks.md so the loop can close. The emitter merges
// findings that a single edit would fix into one task - three critics reporting the
// same missing translation key should produce one task, not three.
phase('Emit')
const outstanding = partial.concat(missing)
const nextNumber = (loaded.highestTaskNumber || 0) + 1

const emitted = await agent(
  [
    ORIENT,
    `Write ${MATRIX_PATH}, then append a new task round to ${TASKS_PATH}.`,
    '',
    `${MATRIX_PATH} holds a traceability table with one row per acceptance criterion:`,
    '  | AC | Verdict | Code evidence | Test evidence | Gap |',
    'followed by "## Critic findings" grouped by critic, and "## Not verified" listing anything nobody could confirm.',
    '',
    `In ${TASKS_PATH}, append a section "## Verification round - remaining work" with one task per outstanding item.`,
    `Number them starting at T-${String(nextNumber).padStart(3, '0')}. NEVER renumber or delete an existing task.`,
    'Use exactly the existing line format:',
    '- [ ] **T-0NN** [P] Title - `files:` path, path - `skill:` name - `acs:` AC-001 - `after:` T-0MM',
    'Rules: merge findings that one edit would fix into ONE task; give exact repo-relative file paths;',
    'name the governing guide under .claude/skills; mark [P] only when the task shares no file with another task in this round;',
    'and give every hotspot file - the DbContext, the permission constants, the web permission mirror, the route-guard, navigation and search lists,',
    'barrel index files and the locale JSON files - a single owning task that the others list in "after:".',
    `Write ONLY ${MATRIX_PATH} and the appended section of ${TASKS_PATH}. Do not modify application code.`,
    '',
    'Outstanding acceptance criteria:',
    JSON.stringify(trim(outstanding, 60, 'outstanding criteria')),
    '',
    'Must-fix critic findings:',
    JSON.stringify(trim(mustFix, 60, 'must-fix findings')),
    '',
    'Satisfied criteria, for the matrix rows:',
    JSON.stringify(trim(satisfied.map((s) => ({ acId: s.acId, codeEvidence: s.codeEvidence, testEvidence: s.testEvidence })), 60, 'satisfied rows')),
  ].join('\n'),
  { label: 'write verification.md and the task round', phase: 'Emit', schema: EMIT_SCHEMA }
)

const newTaskIds = (emitted && emitted.tasksAppended) || []
log(`${newTaskIds.length} remaining-work task(s) appended: ${newTaskIds.join(', ') || 'none'}`)

// ---------------------------------------------------------------- Convergence
// Off by default: the human normally reads verification.md and decides. With autoFix
// the loop runs itself, calling spec-implement as a nested workflow. workflow() nests
// one level only, so if this workflow was invoked from another one the call throws -
// hence the try/catch, which degrades to handing the task ids back to the caller
// rather than failing the run.
let rounds = 1
let converged = !outstanding.length && !mustFix.length
if (AUTO_FIX && newTaskIds.length && !converged) {
  while (rounds < MAX_ROUNDS && newTaskIds.length) {
    if (budget.total && budget.remaining() < 250000) {
      log(`Stopping the convergence loop: ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
      break
    }
    log(`Convergence round ${rounds + 1}: handing ${newTaskIds.length} task(s) to spec-implement.`)
    let child = null
    try {
      child = await workflow('spec-implement', { specDir: SPEC_DIR, tasks: newTaskIds })
    } catch (e) {
      log(`Could not run spec-implement inline (${String(e && e.message ? e.message : e)}). Returning the tasks for the caller to run.`)
      break
    }
    if (!child || !child.ok) {
      log('The nested implementation did not fully succeed; stopping the convergence loop.')
      break
    }
    rounds++
    const recheck = await agent(
      [
        ORIENT,
        `Re-verify only these acceptance criteria against the current source: ${outstanding.map((o) => o.acId).join(', ')}.`,
        `Read ${SPEC_PATH} for their text. For each, state satisfied, partial or missing with code and test evidence.`,
        `Then update the matching rows of the table in ${MATRIX_PATH} in place. Change nothing else.`,
      ].join('\n'),
      { label: `re-verify round ${rounds}`, phase: 'Emit', schema: CRITIC_SCHEMA }
    )
    converged = Boolean(recheck && !(recheck.findings || []).some((f) => f.severity === 'must-fix'))
    if (converged) {
      log(`Converged after ${rounds} round(s).`)
      break
    }
  }
}

return {
  ok: converged,
  specDir: SPEC_DIR,
  matrixPath: MATRIX_PATH,
  matrixWritten: Boolean(emitted),
  acsVerified: results.length,
  acsTotal: loaded.acs.length,
  satisfied: satisfied.map((r) => r.acId),
  partial: partial.map((r) => ({ ac: r.acId, gap: r.gap })),
  missing: missing.map((r) => ({ ac: r.acId, gap: r.gap })),
  criticFindings: mustFix,
  specDrift: criticFindings.filter((f) => f.kind === 'spec-drift'),
  newTaskIds,
  rounds,
  converged,
  nextStep: newTaskIds.length
    ? `Run spec-implement with args {specDir: "${SPEC_DIR}", tasks: ${JSON.stringify(newTaskIds)}}, then re-run spec-verify.`
    : 'All acceptance criteria verified.',
}
