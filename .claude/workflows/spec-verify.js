export const meta = {
  name: 'spec-verify',
  description: 'Trace every EARS acceptance criterion to code and test evidence, adversarially refute each satisfied claim, and append the remaining work to tasks.md',
  whenToUse: 'Fourth stage of spec-driven development. Produces verification.md, a traceability matrix, and a new round of tasks for whatever is not actually done.',
  phases: [
    { title: 'Load', detail: 'read spec.md and tasks.md into criteria and task state' },
    { title: 'Trace', detail: 'hunt code and test evidence, a batch of related criteria per agent' },
    { title: 'Refute', detail: 'three adversarial lenses over the satisfied claims, batched' },
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
//   Takes the acceptance criteria in batches of closely related ones, hunts for the
//   code and the test that implement each, and then tries to knock the answer down. A
//   criterion is only "satisfied" if a tracer found both kinds of evidence AND
//   independent refuters failed to break the claim. Four critics then look for what the
//   tracers could not see at all, and everything still outstanding is appended to
//   tasks.md as a fresh numbered round.
//
// Why refuters rather than a second opinion
//   Asking "is this right?" gets agreement; asking "prove this wrong" gets scrutiny.
//   Each refuter is given a different lens - is the code path even reachable, would the
//   cited test fail if the behaviour were deleted, are the edge cases of the condition
//   handled - because a claim can be wrong in more than one way, and three identical
//   skeptics all miss the same thing.
//
// Why criteria are batched rather than one agent each
//   Criteria that sit under the same "###" heading in spec.md live in the same slice of
//   the codebase. An agent tracing six of them opens one feature folder once instead of
//   six agents each opening it separately, and it sees the slice as a whole, which is
//   how a tracer notices that criterion four is contradicted by the code criterion two
//   cited. The same argument applies to the refuters: "is this code path reachable" for
//   six criteria in one slice means walking one registration chain once.
//
//   Batching costs one thing, and it is guarded against below: an agent handed six
//   criteria can find one endpoint and declare all six satisfied on the same evidence.
//   findSharedEvidence() detects exactly that and sends the affected criteria back for
//   an individual re-trace.
//
// This is a convergence loop, not a report
//   Unmet criteria come back as tasks. Run spec-implement on those ids, then run this
//   again. Stop when it returns converged. Pass autoFix to let it drive that loop
//   itself via workflow(), though nesting is one level deep only - if this workflow was
//   itself invoked from another one, that call throws and is caught below.
//
// args
//   {specDir: string}     required.
//   {batchSize?: number}  criteria per tracing agent, 2 to 10 (default 6).
//   {maxRounds?: number}  convergence rounds when autoFix is on.
//   {autoFix?: boolean}   run spec-implement inline on the emitted tasks.
//
// Returns
//   {ok, converged, satisfied, partial, missing, criticFindings, specDrift,
//    newTaskIds, matrixPath, acsVerified, acsTotal}
//   Every criterion is traced - there is no cap - so acsVerified below acsTotal now
//   means an agent died rather than that the run was truncated, and converged is false
//   whenever the two differ.
//
// Files written
//   verification.md, and an appended section of tasks.md, by two agents that run
//   together because the two files are disjoint. Never application code: a verifier
//   that can edit the code under test is not a verifier.
//
// Cost
//   Roughly 48 subagents for 100 criteria - one loader, about 17 batch tracers, about
//   24 batched refuters, 4 critics and 2 emitters. The previous per-criterion fan-out
//   was about 146 for the same spec, and it stopped after 60 criteria.
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
// none was set. Depth buys smaller batches rather than more agents per criterion: a
// tracer holding four criteria reads more carefully than one holding eight, which is a
// better use of a larger budget than a fourth refutation lens would be.
const DEPTH = budget.total ? Math.max(1, Math.min(3, Math.floor(budget.total / 300000))) : 1
const BATCH = Math.max(2, Math.min(10, (args && args.batchSize) || (DEPTH >= 2 ? 4 : 6)))
const REFUTE_BATCH = Math.max(2, BATCH + 2)
const MAX_ROUNDS = Math.max(1, Math.min(3, (args && args.maxRounds) || (DEPTH >= 2 ? 3 : 2)))
const AUTO_FIX = Boolean(args && args.autoFix)
// A re-trace is the expensive remedy for a batch that leaned on one shared piece of
// evidence, so it is capped. The cap is logged when it bites - see RE-TRACE below.
const MAX_RETRACE = DEPTH >= 2 ? 12 : 8

// Prepended to every prompt. CLAUDE.md is NOT mentioned here on purpose: the workflow
// runtime already injects it into every subagent, so asking for it again buys a
// duplicate read of the largest document in the repository, once per agent.
//
// The namespace line stays, because it is not in CLAUDE.md as an instruction to the
// agent. It is a guard specific to this template: this file ships into scaffolded
// projects byte-for-byte while only markdown gets namespace-rewritten, so an agent that
// guesses a namespace from this script would guess wrong.
const ORIENT = [
  'Judge against what the repository actually contains, not what it ought to contain.',
  'Never invent a root namespace, project file name or solution file name - read them from the repo.',
].join(' ')

// Added only for the agents whose job is to judge whether a convention was followed.
// The rule is what the agent judges, not whether it writes: the cross-cutting critic
// edits nothing and needs this more than most.
const CONVENTIONS = 'When a convention is in question, read .claude/skills/coding-conventions/SKILL.md and the relevant guide under .claude/skills.'

// Added only for the agents that must reproduce a document format or work from the
// completeness checklist. Everyone else gets the specific rule inlined in their prompt
// instead, which is cheaper than a 10 KB read.
const PROCESS = 'Read .claude/skills/spec-driven/SKILL.md for the completeness checklist and the exact tasks.md line format.'

// ---------------------------------------------------------------- schemas
// A schema on agent() forces a StructuredOutput tool call and returns a validated
// object. Note that the trace schema keeps code evidence and test evidence in separate
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
          area: { type: 'string', description: 'the "###" heading this criterion sits under in spec.md' },
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

const TRACE_ITEM = {
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

const TRACE_BATCH_SCHEMA = {
  type: 'object',
  properties: {
    traces: { type: 'array', items: TRACE_ITEM },
  },
  required: ['traces'],
}

// basis is what makes a single-lens downgrade safe. A refuter that names a concrete
// residual gap has made a positive finding on its own axis and one such finding is
// enough. A refuter that merely could not confirm the claim has made no finding, and
// those are only believed when two of the three lenses agree - otherwise the standing
// instruction to refute when uncertain would downgrade almost everything.
const REFUTE_BATCH_SCHEMA = {
  type: 'object',
  properties: {
    lens: { type: 'string' },
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          acId: { type: 'string' },
          refuted: { type: 'boolean' },
          basis: {
            type: 'string',
            enum: ['named-gap', 'could-not-confirm', 'not-refuted'],
            description: 'named-gap only when you can state a specific concrete defect you verified',
          },
          reason: { type: 'string' },
          residualGap: { type: 'string' },
        },
        required: ['acId', 'refuted', 'basis', 'reason'],
      },
    },
  },
  required: ['lens', 'verdicts'],
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

const MATRIX_SCHEMA = {
  type: 'object',
  properties: {
    matrixPath: { type: 'string' },
    rowsWritten: { type: 'number' },
  },
  required: ['matrixPath'],
}

const APPEND_SCHEMA = {
  type: 'object',
  properties: {
    tasksAppended: { type: 'array', items: { type: 'string' } },
    stillOpenAcs: { type: 'array', items: { type: 'string' } },
  },
  required: ['tasksAppended'],
}

// index-driven lenses, never random, so a resumed run replays identically
const REFUTE_LENSES = [
  {
    key: 'execution-reality',
    ask: 'Execution reality: prove the code path the tracer cited is actually reachable at runtime - the endpoint is registered in a route group, the permission it needs exists and is declared in a provider, the service is registered by the feature module, the web route is reachable and guarded. If any link in that chain is missing, the criterion is NOT satisfied.',
  },
  {
    key: 'test-reality',
    ask: 'Test reality: read the cited tests. Would any of them FAIL if the behaviour were removed or inverted? A test that only asserts a success status, or that never exercises the criterion condition, is not evidence. With no such test the criterion is at best partial.',
  },
  {
    key: 'boundary-reality',
    ask: 'Boundary reality: the criterion has a condition - when, while, where or if. Prove the implementation handles the negative and edge cases of that condition: unauthorized caller, missing or soft-deleted record, empty result, invalid input, oversized input, and the resulting user-visible message being translated rather than hard-coded.',
  },
]

// ---------------------------------------------------------------- helpers
function trim(list, n, what) {
  const arr = Array.isArray(list) ? list : []
  if (arr.length > n) log(`Dropped ${arr.length - n} of ${arr.length} ${what} when building a prompt (kept ${n}).`)
  return arr.slice(0, n)
}

// Fixed-size chunking in document order. spec.md already groups criteria under "###"
// headings by area, so consecutive criteria are related by construction and this needs
// no clustering pass - which also means it is perfectly deterministic and replays
// identically on resume. Deliberately NOT keyed on the loader's free-text "area" field:
// a weak key would reshuffle the batches between runs and invalidate every cached
// agent result.
function chunk(list, size) {
  const out = []
  for (let i = 0; i < list.length; i += size) out.push(list.slice(i, i + size))
  return out
}

// The guard against the one thing batching invites: an agent handed six criteria finds
// one endpoint and one test, then declares all six satisfied citing the same file.
// Genuine evidence diverges - six criteria are six behaviours - so three or more
// criteria whose ONLY test evidence is the same path is a tell, not a coincidence.
// Those criteria are re-traced one at a time below.
function findSharedEvidence(traces) {
  const byPath = new Map()
  for (const t of traces) {
    if (!t || t.verdict !== 'satisfied') continue
    const paths = (t.testEvidence || []).map((e) => String(e.path || '').toLowerCase()).filter(Boolean)
    if (paths.length !== 1) continue // only a SOLE piece of evidence is suspicious
    const key = paths[0]
    if (!byPath.has(key)) byPath.set(key, [])
    byPath.get(key).push(t.acId)
  }
  const suspect = []
  for (const [path, ids] of byPath) {
    if (ids.length >= 3) {
      log(`${ids.length} criteria rest solely on ${path}: ${ids.join(', ')} - re-tracing them individually.`)
      suspect.push(...ids)
    }
  }
  return suspect
}

// ---------------------------------------------------------------- Load
// highestTaskNumber matters more than it looks: the new round of tasks is numbered from
// it, so ids stay unique and nothing already in tasks.md gets renumbered. Renumbering
// would break the traceability matrix and every cached agent result on resume, which is
// why the JS check below distrusts the number and recomputes it from the parsed ids.
phase('Load')
const loaded = await agent(
  [
    ORIENT,
    `Read ${SPEC_PATH} and ${TASKS_PATH}.`,
    'Return every acceptance criterion with its exact id and text, in the order they appear in the file,',
    'together with the "###" heading each one sits under.',
    'Return every task id, the ids of tasks whose checkbox is unticked,',
    'and the highest task number currently used as a number - 23 for T-023.',
    'Do not modify any file.',
  ].join('\n'),
  { label: 'load spec and tasks', schema: LOAD_SCHEMA, effort: 'low' }
)
if (!loaded || !loaded.acs || !loaded.acs.length) {
  return { ok: false, error: 'cannot-read-spec-or-tasks', specDir: SPEC_DIR }
}

// Trust but verify the parse. highestTaskNumber drives the never-renumber contract, so
// recompute it from the task ids the same agent returned and take whichever is larger.
// A loader that under-reports it would silently overwrite existing tasks.
const parsedHighest = (loaded.taskIds || []).reduce((max, id) => {
  const m = /(\d+)/.exec(String(id))
  return m ? Math.max(max, parseInt(m[1], 10)) : max
}, 0)
if (parsedHighest > (loaded.highestTaskNumber || 0)) {
  log(`Loader reported the highest task number as ${loaded.highestTaskNumber}, but the task ids go up to ${parsedHighest}. Using ${parsedHighest}.`)
}
const highestTaskNumber = Math.max(parsedHighest, loaded.highestTaskNumber || 0)

if (loaded.openTaskIds && loaded.openTaskIds.length) {
  log(`Note: ${loaded.openTaskIds.length} task(s) in tasks.md are still unticked: ${loaded.openTaskIds.join(', ')}`)
}

// Every criterion is traced. The previous version capped this at 60 and reported the
// run as complete, so a hundred-criterion spec came back converged having never looked
// at forty of its criteria.
const acs = loaded.acs
const batches = chunk(acs, BATCH)
log(`Tracing ${acs.length} acceptance criteria in ${batches.length} batch(es) of up to ${BATCH}.`)

// ---------------------------------------------------------------- Trace
phase('Trace')
const traceResults = await parallel(
  batches.map((b, i) => () =>
    agent(
      [
        ORIENT,
        `Determine, for each acceptance criterion below, whether it is implemented in this repository.`,
        `They are criteria ${b.map((a) => a.id).join(', ')} of ${SPEC_PATH}${b[0] && b[0].area ? `, under "${b[0].area}"` : ''}.`,
        `${PLAN_PATH} and the contract documents in ${SPEC_DIR} describe how they were meant to be built - read them, then verify against the ACTUAL source.`,
        'Search the real source tree under src/backend and src/frontend. Cite exact repo-relative paths and symbol names you actually opened.',
        'Separate code evidence from test evidence. "satisfied" requires BOTH: code that implements it, and at least one test that would fail without it.',
        'These criteria are related, so you will open some of the same files for several of them. That is expected -',
        'but each criterion is a DIFFERENT behaviour and needs its own evidence. Two criteria must not cite the same test as their',
        'sole evidence unless that test genuinely asserts both behaviours, and when it does, say so explicitly in the "why".',
        'If you cannot find evidence, say "missing" and describe the gap. Do not guess, and do not edit any file.',
        'Return one entry per criterion - all of them, in the order given.',
        '',
        JSON.stringify(b.map((a) => ({ id: a.id, text: a.text }))),
      ].join('\n'),
      { label: `trace ${b[0].id}-${b[b.length - 1].id}`, phase: 'Trace', schema: TRACE_BATCH_SCHEMA }
    )
  )
)

// Flatten, then backfill. A batch agent that died takes its whole batch with it, so
// every criterion it was carrying is recorded as missing with a reason rather than
// vanishing from the matrix - a criterion nobody looked at must never read as passing.
const traceById = new Map()
traceResults.forEach((r, i) => {
  if (!r || !Array.isArray(r.traces)) {
    log(`Trace batch ${i + 1} returned nothing; its ${batches[i].length} criteria are recorded as unverified.`)
    return
  }
  for (const t of r.traces) if (t && t.acId) traceById.set(t.acId, t)
})
for (const ac of acs) {
  if (!traceById.has(ac.id)) {
    traceById.set(ac.id, {
      acId: ac.id,
      verdict: 'missing',
      codeEvidence: [],
      testEvidence: [],
      gap: 'no tracer result was returned for this criterion',
    })
  }
}

// RE-TRACE: the shared-evidence guard described at findSharedEvidence(). Only the
// criteria that tripped it are re-examined, one agent each, which is the expensive
// per-criterion mode used sparingly rather than by default.
const suspect = trim(findSharedEvidence(Array.from(traceById.values())), MAX_RETRACE, 'shared-evidence re-traces')
if (suspect.length) {
  const retraced = await parallel(
    suspect.map((id) => () => {
      const ac = acs.find((a) => a.id === id)
      return agent(
        [
          ORIENT,
          `Re-examine acceptance criterion ${id} on its own. Criterion: "${ac ? ac.text : ''}"`,
          'Another agent verified it alongside several sibling criteria and rested all of them on the same single test.',
          'That is usually a sign the test proves one behaviour and was credited to several.',
          'Read that test and decide, for THIS criterion alone:',
          'would that test fail if THIS criterion\'s behaviour were removed or inverted? If not, find the test that would, or report the criterion as partial.',
          'Cite exact repo-relative paths. Do not edit any file.',
        ].join('\n'),
        { label: `re-trace ${id}`, phase: 'Trace', schema: TRACE_BATCH_SCHEMA }
      )
    })
  )
  retraced.filter(Boolean).forEach((r) => {
    for (const t of r.traces || []) if (t && t.acId) traceById.set(t.acId, t)
  })
}

const traces = acs.map((a) => traceById.get(a.id))
const claimedSatisfied = traces.filter((t) => t.verdict === 'satisfied')
log(`Trace complete: ${claimedSatisfied.length} claimed satisfied, ${traces.filter((t) => t.verdict === 'partial').length} partial, ${traces.filter((t) => t.verdict === 'missing').length} missing.`)

// ---------------------------------------------------------------- Refute
// A barrier was crossed above deliberately. Refuting per criterion, the way this stage
// used to, cost two agents for every satisfied claim - about 120 on a hundred-criterion
// spec - and each of them re-read the same feature slice. Refuting a batch of claims
// from the same area under one lens walks that slice's registration chain once and
// judges eight claims against it.
//
// The batches are formed from the SATISFIED set only, which is why the barrier is
// needed: there is no way to pack them until every tracer has reported.
phase('Refute')
const refuteBatches = chunk(claimedSatisfied, REFUTE_BATCH)
const refutedBy = new Map() // acId -> array of verdicts against it

if (refuteBatches.length) {
  log(`Refuting ${claimedSatisfied.length} satisfied claim(s) in ${refuteBatches.length} batch(es) under ${REFUTE_LENSES.length} lenses.`)
  // Every lens sees every claim, so all three axes are applied to all of them - it is
  // the fan-out that shrank, not the scrutiny. pipeline() over the batches: batch A can
  // be under all three lenses while batch B is still queued.
  const lensResults = await pipeline(
    refuteBatches,
    (batch, unused, i) =>
      parallel(
        REFUTE_LENSES.map((lens) => () =>
          agent(
            [
              ORIENT,
              CONVENTIONS,
              `Another agent claims the acceptance criteria below are fully satisfied. Your job is to REFUTE those claims.`,
              `Your lens: ${lens.ask}`,
              'Open every file each claim cites and read the surrounding code - do not take any of it at face value.',
              'These claims are related and cite overlapping files, so read each file once and judge every claim that rests on it.',
              '',
              'Return one verdict per criterion, and set "basis" carefully - it decides how much weight your vote carries:',
              '  "named-gap"         you verified a specific concrete defect. State it in residualGap. One of these is enough to downgrade a criterion, so do not use it unless you actually confirmed the defect.',
              '  "could-not-confirm" the claim may be fine but you could not establish it. Use this when uncertain.',
              '  "not-refuted"       the claim survives your lens completely.',
              'Set refuted=true for the first two, false for the third.',
              'Do not edit any file.',
              '',
              'The claims:',
              JSON.stringify(
                batch.map((t) => ({
                  acId: t.acId,
                  criterion: (acs.find((a) => a.id === t.acId) || {}).text,
                  codeEvidence: t.codeEvidence,
                  testEvidence: t.testEvidence,
                }))
              ),
            ].join('\n'),
            { label: `refute ${lens.key} b${i + 1}`, phase: 'Refute', schema: REFUTE_BATCH_SCHEMA, effort: 'medium' }
          )
        )
      )
  )

  for (const perBatch of lensResults) {
    for (const lensResult of (perBatch || []).filter(Boolean)) {
      for (const v of lensResult.verdicts || []) {
        if (!v || !v.acId || !v.refuted) continue
        if (!refutedBy.has(v.acId)) refutedBy.set(v.acId, [])
        refutedBy.get(v.acId).push(v)
      }
    }
  }
}

// The downgrade rule, and the reason it is not a simple majority.
//
// The three lenses are orthogonal: a criterion can be genuinely broken on exactly one
// of them - the test is a smoke test, say - and the other two lenses will honestly
// report that it survives THEIR axis. Requiring two of three to agree would therefore
// suppress precisely the single-axis defects that are the most common kind, and a weak
// test is the defect this codebase most needs caught.
//
// So: one lens that names a concrete verified gap is enough. "Could not confirm" is not
// a finding, only an absence of one, and those need two of the three lenses to agree
// before they count - otherwise the standing instruction to refute when uncertain would
// downgrade almost every claim.
const results = traces.map((t) => {
  if (t.verdict !== 'satisfied') return t
  const votes = refutedBy.get(t.acId) || []
  const named = votes.filter((v) => v.basis === 'named-gap')
  const unsure = votes.filter((v) => v.basis === 'could-not-confirm')
  if (!named.length && unsure.length < 2) return t
  const why = (named.length ? named : unsure).map((v) => v.residualGap || v.reason).filter(Boolean)
  log(
    named.length
      ? `${t.acId}: downgraded to partial - ${named.length} lens(es) named a concrete gap.`
      : `${t.acId}: downgraded to partial - ${unsure.length} lenses could not confirm the claim.`
  )
  return { ...t, verdict: 'partial', gap: why.join(' | ') || 'refuted by independent review' }
})

const satisfied = results.filter((r) => r.verdict === 'satisfied')
const partial = results.filter((r) => r.verdict === 'partial')
const missing = results.filter((r) => r.verdict === 'missing')
log(`After refutation: ${satisfied.length} satisfied, ${partial.length} partial, ${missing.length} missing.`)

// ---------------------------------------------------------------- Critique
// The tracers answer "is this criterion met?", a batch at a time. That leaves three
// blind spots, one critic each: work that spans criteria (a permission mirrored on only
// one side, a locale key added to only one file), tests that pass without proving
// anything, and code that no criterion ever asked for. The fourth critic asks what this
// verification itself failed to check.
phase('Critique')
const CRITICS = [
  {
    key: 'cross-cutting',
    conventions: true,
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
    conventions: true,
    ask: [
      'Audit the tests added for this feature. For each, ask whether it would fail if the behaviour were removed.',
      'Flag smoke tests masquerading as coverage, tests that assert only a status code, tests that depend on global database state they did not create,',
      'unwanted-behaviour criteria with no negative test, and criteria whose only test is a happy path.',
    ].join(' '),
  },
  {
    key: 'spec-drift',
    conventions: false,
    ask: [
      'Find code written for this feature that NO acceptance criterion asked for, and behaviour the spec asked for that was quietly reinterpreted.',
      'Inspect what actually changed in the working tree (git status, git diff --stat, git diff).',
      'Report each as spec-drift with a recommendation: remove it, or amend the spec.',
    ].join(' '),
  },
  {
    key: 'completeness',
    conventions: false,
    process: true,
    ask: [
      'You are the completeness critic. What did this verification MISS?',
      'A criterion whose trace produced no result; a file changed by the implementation that nobody reviewed; a claim accepted on a single piece of evidence;',
      'a modality never run - nobody executed the build or the tests, nobody looked at the running web app, nobody checked that the migration applies.',
      'Note that criteria were traced in batches of related criteria, so look specifically for a criterion that was credited to evidence really belonging to a sibling.',
      'Report each as unverified-claim, naming the specific check that should be run.',
    ].join(' '),
  },
]

// Barrier: the emitters must merge findings that one edit would fix into a single task
// rather than three, which needs every critic's output at once.
const critiques = await parallel(
  CRITICS.map((c) => () =>
    agent(
      [
        ORIENT,
        c.conventions ? CONVENTIONS : '',
        c.process ? PROCESS : '',
        `Read ${SPEC_PATH}, ${PLAN_PATH}, ${TASKS_PATH} and the contract documents in ${SPEC_DIR}, then inspect the actual source.`,
        `Your critic role is "${c.key}". ${c.ask}`,
        'Report only your own dimension. Every finding needs a concrete, actionable fix and the file it applies to. Do not edit any file.',
        '',
        'The verdicts the tracers reached, for context - you are not bound by them:',
        JSON.stringify(results.map((r) => ({ acId: r.acId, verdict: r.verdict, gap: r.gap }))),
      ]
        .filter(Boolean)
        .join('\n'),
      { label: `critic: ${c.key}`, phase: 'Critique', schema: CRITIC_SCHEMA, effort: c.key === 'completeness' ? 'high' : undefined }
    )
  )
)

const criticFindings = critiques.filter(Boolean).flatMap((c) => (c.findings || []).map((f) => ({ critic: c.critic, ...f })))
const mustFix = criticFindings.filter((f) => f.severity === 'must-fix')
log(`Critics returned ${criticFindings.length} finding(s), ${mustFix.length} must-fix.`)

// ---------------------------------------------------------------- Emit
// Two outputs and two agents. verification.md and the appended section of tasks.md are
// disjoint files, so writing them concurrently needs no lock, and splitting them means
// a hundred-row traceability matrix is not competing for one agent's attention with the
// job of numbering a new task round correctly.
//
// The prompt caps here are generous on purpose: the previous version trimmed all three
// payloads to 60, which quietly dropped rows from the matrix of a spec this size.
phase('Emit')
const outstanding = partial.concat(missing)
const nextNumber = highestTaskNumber + 1
const CAP = Math.max(120, acs.length + 20)

const [matrixDoc, appendDoc] = await parallel([
  () =>
    agent(
      [
        ORIENT,
        `Write ${MATRIX_PATH}, the traceability matrix for this verification run.`,
        'It holds a table with one row per acceptance criterion, in id order:',
        '  | AC | Verdict | Code evidence | Test evidence | Gap |',
        'followed by "## Critic findings" grouped by critic, and "## Not verified" listing anything nobody could confirm.',
        `Every one of the ${results.length} criteria below gets a row. Do not summarise or omit any.`,
        `Write ONLY ${MATRIX_PATH}. Do not modify application code and do not touch ${TASKS_PATH}.`,
        '',
        'Verdicts:',
        JSON.stringify(trim(results, CAP, 'matrix rows')),
        '',
        'Critic findings:',
        JSON.stringify(trim(criticFindings, CAP, 'critic findings')),
      ].join('\n'),
      { label: 'write verification.md', phase: 'Emit', schema: MATRIX_SCHEMA }
    ),
  () =>
    agent(
      [
        ORIENT,
        PROCESS,
        `Append a new task round to ${TASKS_PATH} - one task per outstanding item below.`,
        `Number them starting at T-${String(nextNumber).padStart(3, '0')}. NEVER renumber or delete an existing task.`,
        'Add them under a new heading "## Verification round - remaining work".',
        'Use exactly the existing line format:',
        '- [ ] **T-0NN** [P] Title - `files:` path, path - `skill:` name - `acs:` AC-001 - `after:` T-0MM',
        'Rules: merge findings that one edit would fix into ONE task; give exact repo-relative file paths;',
        'name the governing guide under .claude/skills; mark [P] only when the task shares no file with another task in this round;',
        'and give every hotspot file - the DbContext, the permission constants, the shared global-usings file, the startup file,',
        'the error-code constants, the web permission mirror, the route-guard, navigation and search lists,',
        'barrel index files and the locale JSON files - a single owning task that the others list in "after:".',
        `Write ONLY the appended section of ${TASKS_PATH}. Do not modify application code and do not touch ${MATRIX_PATH}.`,
        '',
        'Outstanding acceptance criteria:',
        JSON.stringify(trim(outstanding, CAP, 'outstanding criteria')),
        '',
        'Must-fix critic findings:',
        JSON.stringify(trim(mustFix, CAP, 'must-fix findings')),
      ].join('\n'),
      { label: 'append the task round', phase: 'Emit', schema: APPEND_SCHEMA }
    ),
])

const newTaskIds = (appendDoc && appendDoc.tasksAppended) || []
log(`${newTaskIds.length} remaining-work task(s) appended: ${newTaskIds.join(', ') || 'none'}`)

// ---------------------------------------------------------------- Convergence
// Off by default: the human normally reads verification.md and decides. With autoFix
// the loop runs itself, calling spec-implement as a nested workflow. workflow() nests
// one level only, so if this workflow was invoked from another one the call throws -
// hence the try/catch, which degrades to handing the task ids back to the caller
// rather than failing the run.
//
// Note the traced === total term: a run that lost a batch to a dead agent has not
// verified the spec, whatever the surviving verdicts say.
let rounds = 1
const fullyTraced = results.length === acs.length && !results.some((r) => r.gap === 'no tracer result was returned for this criterion')
let converged = !outstanding.length && !mustFix.length && fullyTraced
if (!fullyTraced) log('At least one criterion has no tracer result, so this run cannot report convergence.')

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
  matrixWritten: Boolean(matrixDoc),
  acsVerified: results.filter((r) => r.gap !== 'no tracer result was returned for this criterion').length,
  acsTotal: acs.length,
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
