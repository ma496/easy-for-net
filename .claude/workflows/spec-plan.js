export const meta = {
  name: 'spec-plan',
  description: 'Turn specs/<NNN-slug>/spec.md into plan.md, four contract documents and a coverage-gated tasks.md',
  whenToUse: 'Second stage of spec-driven development, once the spec has no unanswered open questions. Produces the implementation plan and the task list spec-implement consumes.',
  phases: [
    { title: 'Load spec', detail: 'parse the acceptance criteria and check for unresolved blockers' },
    { title: 'Design panel', detail: 'independent approaches from different framings, each scored by rubric judges' },
    { title: 'Synthesize', detail: 'write plan.md from the winner, grafting the best runner-up ideas' },
    { title: 'Contracts', detail: 'data model, API, frontend and test contracts in parallel' },
    { title: 'Decompose', detail: 'ordered atomic tasks with file sets, skills, criterion ids and parallel markers' },
    { title: 'Coverage gate', detail: 'forward, backward and wave-safety audits, then repair until clean' },
  ],
}

// =============================================================================
// STAGE 2 OF 4 - the specification becomes a design and a work list.
//
//   /specify -> /plan -> spec-plan -> /implement -> /verify
//
// What it does
//   Generates several genuinely different designs in parallel, scores each against
//   this repository's own conventions, writes plan.md from the winner while grafting
//   the best ideas from the losers, expands it into four contract documents, and
//   decomposes those into an ordered tasks.md that is then gated for coverage.
//
// Why a judge panel rather than one design, iterated
//   The solution space here is wide - extend an existing slice, or add a new one; lead
//   with the data model, or with the riskiest constraint. One attempt refined in place
//   tends to entrench whichever framing it happened to start from. Independent attempts
//   scored by independent rubrics surface the trade-off instead of hiding it, and
//   plan.md records why the winner beat the alternatives by name.
//
// args
//   {specDir: string}      required, e.g. "specs/007-user-csv-export".
//   {focus?: string}       free-text steer handed to every design agent.
//   {approaches?: number}  panel size, 2 to 5.
//
// Returns
//   {ok, planPath, tasksPath, contracts, chosenApproach, approachScores, taskCount,
//    coverageClean, uncoveredAcs, orphanTasks, needsClarification}
//   ok is false with needsClarification when spec.md still has unanswered questions -
//   planning against an ambiguous spec is how a whole stage gets wasted.
//
// Files written
//   plan.md, then data-model.md / api-contract.md / frontend-contract.md /
//   test-plan.md concurrently (four agents, four distinct filenames, so no lock is
//   needed), then tasks.md. Never two writers on one file.
//
// Cost
//   Roughly 27 subagents at the default depth.
// =============================================================================

// ---------------------------------------------------------------- arguments
const SPEC_DIR = (args && args.specDir) || (typeof args === 'string' ? args : '')
if (!SPEC_DIR) {
  log('spec-plan requires args {specDir: "specs/<NNN-slug>"}.')
  return { ok: false, error: 'missing-specDir' }
}
const SPEC_PATH = `${SPEC_DIR}/spec.md`
const PLAN_PATH = `${SPEC_DIR}/plan.md`
const TASKS_PATH = `${SPEC_DIR}/tasks.md`
const FOCUS = (args && args.focus) || ''

// Depth scales the run to the token budget set for this turn; budget.total is null
// when none was set. Every bound comes from budget.total or args, never from a clock
// or a random number, so an interrupted run resumes and replays identically.
const DEPTH = budget.total ? Math.max(1, Math.min(3, Math.floor(budget.total / 250000))) : 1
const PANEL = Math.max(2, Math.min(5, (args && args.approaches) || (DEPTH >= 2 ? 5 : 3)))
const MAX_GATE_ROUNDS = DEPTH >= 2 ? 3 : 2

// Prepended to every prompt: subagents start with no context and no memory of this
// repository. Note that it points at markdown files rather than restating their
// content - this script is copied verbatim into scaffolded projects while only
// markdown gets namespace-rewritten, so anything repo-specific belongs there.
const ORIENT = [
  'Ground yourself first: read CLAUDE.md at the repo root for architecture, layout and the exact build and test commands,',
  'then .claude/skills/spec-driven/SKILL.md, then the relevant guides under .claude/skills/<name>/SKILL.md.',
  'Never invent a root namespace, project file name or solution file name - read them from the repo.',
].join(' ')

// The task decomposer must name a governing guide for every task, and the implementing
// agent in the next stage reads that guide before it writes a line. This table is how
// the two stages agree on which guide covers which kind of work.
const SKILL_MAP = [
  'Skill routing - name the guide each task must be executed with:',
  '  new vertical slice, or a cross-feature dependency  -> backend-feature',
  '  entity, EF configuration, DbSet, migration         -> backend-entity',
  '  HTTP endpoint, request, validator, mapper          -> backend-endpoint',
  '  permission constant, provider, web mirror          -> permissions',
  '  error code and its user-facing message             -> api-error-handling',
  '  integration tests for the API                      -> backend-tests',
  '  RTK Query slice and DTOs                           -> rtk-query-api',
  '  list, create, update and delete screens            -> frontend-crud',
  '  a single new route or screen                       -> frontend-page',
  '  shared React component                             -> ui-component',
  '  client-only state                                  -> redux-state',
  '  translation keys and locale files                  -> localization',
  '  vitest tests                                       -> frontend-tests',
  '  Hangfire work                                      -> background-jobs',
  '  in-app notifications                               -> notifications',
  '  uploads and binary content                         -> file-storage',
  '  anything at all, as a second reference             -> coding-conventions',
].join('\n')

// Hotspots are the files nearly every feature appends to. Two tasks editing one of
// them merge cleanly at the text level and then fail to compile, or silently lose one
// of the two edits. The planner must therefore give each hotspot a single owning task;
// spec-implement enforces the same rule again at schedule time, as a backstop.
const HOTSPOT_NOTE = [
  'Hotspot files are edited by almost every feature and merge cleanly while failing to compile:',
  'the application DbContext, the permission constants file, the shared global-usings file, the startup file,',
  'the error-code constants file, and on the web side allow.ts, auth-urls.ts, nav-items.ts, searchable-items.ts,',
  'every barrel index.ts and every file under public/locales.',
  'Give each hotspot a SINGLE owning task; every other task that needs it lists that task in "after:" instead of declaring the file.',
].join(' ')

// ---------------------------------------------------------------- schemas
// A schema on agent() forces the subagent through a StructuredOutput tool call and
// returns a validated object, so the model retries on a mismatch and this script reads
// fields rather than parsing prose. Root must be an object; "required" must be a
// subset of "properties" or agent() throws.
const SPEC_SCHEMA = {
  type: 'object',
  properties: {
    title: { type: 'string' },
    summary: { type: 'string' },
    acs: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          text: { type: 'string' },
          ears: { type: 'string', enum: ['ubiquitous', 'event', 'state', 'optional', 'unwanted', 'unknown'] },
          area: { type: 'string', description: 'backend | frontend | data | cross-cutting | tests' },
        },
        required: ['id', 'text'],
      },
    },
    constraints: { type: 'array', items: { type: 'string' } },
    outOfScope: { type: 'array', items: { type: 'string' } },
    unresolvedQuestions: { type: 'array', items: { type: 'string' } },
  },
  required: ['title', 'acs', 'unresolvedQuestions'],
}

const PROPOSAL_SCHEMA = {
  type: 'object',
  properties: {
    framing: { type: 'string' },
    headline: { type: 'string', description: 'one sentence describing the approach' },
    decisions: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          decision: { type: 'string' },
          rationale: { type: 'string' },
          alternative: { type: 'string' },
        },
        required: ['decision', 'rationale'],
      },
    },
    componentsTouched: { type: 'array', items: { type: 'string' } },
    newArtifacts: { type: 'array', items: { type: 'string' } },
    reuses: { type: 'array', items: { type: 'string' } },
    risks: { type: 'array', items: { type: 'string' } },
    acsAddressed: { type: 'array', items: { type: 'string' } },
    acsNotAddressed: { type: 'array', items: { type: 'string' } },
    estimatedTaskCount: { type: 'number' },
  },
  required: ['framing', 'headline', 'decisions', 'componentsTouched', 'risks', 'acsAddressed'],
}

const SCORE_SCHEMA = {
  type: 'object',
  properties: {
    rubric: { type: 'string' },
    score: { type: 'number', description: '0 to 10' },
    strengths: { type: 'array', items: { type: 'string' } },
    weaknesses: { type: 'array', items: { type: 'string' } },
    bestIdeaWorthGrafting: { type: 'string' },
  },
  required: ['rubric', 'score', 'weaknesses'],
}

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    planPath: { type: 'string' },
    approach: { type: 'string' },
    grafted: { type: 'array', items: { type: 'string' } },
    workstreams: { type: 'array', items: { type: 'string' } },
    openRisks: { type: 'array', items: { type: 'string' } },
  },
  required: ['planPath', 'approach'],
}

const CONTRACT_SCHEMA = {
  type: 'object',
  properties: {
    contract: { type: 'string' },
    path: { type: 'string' },
    artifacts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string', description: 'repo-relative path this contract says must be created or edited' },
          change: { type: 'string', enum: ['create', 'edit'] },
          what: { type: 'string' },
          acs: { type: 'array', items: { type: 'string' } },
          skill: { type: 'string' },
        },
        required: ['file', 'change', 'what'],
      },
    },
    openQuestions: { type: 'array', items: { type: 'string' } },
  },
  required: ['contract', 'path', 'artifacts'],
}

const TASKS_SCHEMA = {
  type: 'object',
  properties: {
    tasksPath: { type: 'string' },
    tasks: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          title: { type: 'string' },
          files: { type: 'array', items: { type: 'string' } },
          skill: { type: 'string' },
          acs: { type: 'array', items: { type: 'string' } },
          dependsOn: { type: 'array', items: { type: 'string' } },
          parallel: { type: 'boolean' },
          verify: { type: 'string', description: 'how to check this task alone is done' },
        },
        required: ['id', 'title', 'files', 'skill', 'acs', 'parallel'],
      },
    },
  },
  required: ['tasksPath', 'tasks'],
}

const AUDIT_SCHEMA = {
  type: 'object',
  properties: {
    audit: { type: 'string' },
    clean: { type: 'boolean' },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: {
            type: 'string',
            enum: ['uncovered-ac', 'orphan-task', 'bad-path', 'bad-order', 'unsafe-parallel', 'missing-skill', 'other'],
          },
          ref: { type: 'string', description: 'an AC id, a task id, or a file path' },
          problem: { type: 'string' },
          fix: { type: 'string' },
        },
        required: ['kind', 'ref', 'problem', 'fix'],
      },
    },
  },
  required: ['audit', 'clean', 'findings'],
}

function trim(list, n, what) {
  const arr = Array.isArray(list) ? list : []
  if (arr.length > n) log(`Dropped ${arr.length - n} of ${arr.length} ${what} when building a prompt (kept ${n}).`)
  return arr.slice(0, n)
}

// ---------------------------------------------------------------- Load spec
// Also the gate that protects this whole stage: if spec.md still has unanswered
// entries under "## Open questions", stop here and send them back. Designing against
// an ambiguous specification produces a plan that has to be thrown away.
phase('Load spec')
const spec = await agent(
  [
    ORIENT,
    `Read ${SPEC_PATH} in full and return it in structured form.`,
    'Extract every acceptance criterion with its exact id and text, and classify its EARS form and its area.',
    'Under unresolvedQuestions, list any entry in the "## Open questions" section that has not been answered inline in the document.',
    'Do not edit any file.',
  ].join('\n'),
  { label: 'load spec', schema: SPEC_SCHEMA, effort: 'low' }
)

if (!spec) return { ok: false, error: 'cannot-read-spec', specDir: SPEC_DIR }
if (spec.unresolvedQuestions && spec.unresolvedQuestions.length) {
  log(`${spec.unresolvedQuestions.length} open question(s) are still unanswered in spec.md - stopping before planning.`)
  return {
    ok: false,
    needsClarification: spec.unresolvedQuestions,
    specDir: SPEC_DIR,
    nextStep: 'Answer these in spec.md, remove them from "## Open questions", then re-run spec-plan.',
  }
}
log(`Loaded ${spec.acs.length} acceptance criteria.`)

// ---------------------------------------------------------------- Design panel
phase('Design panel')
const FRAMINGS = [
  {
    key: 'reuse-first',
    ask: 'Maximise reuse. Prefer extending an existing vertical slice, existing endpoints, existing components and existing translation namespaces over creating anything new. Justify every new artifact.',
  },
  {
    key: 'clean-slice',
    ask: 'Prefer a new, self-contained vertical slice with its own core, endpoints, permission provider and tests. Optimise for feature isolation - no cross-slice dependency that would need an escape-hatch attribute.',
  },
  {
    key: 'risk-first',
    ask: 'Start from the hardest constraint (the migration, the feature-isolation rules, permission reconciliation on startup, locale synchronisation, session and auth behaviour) and design backwards so the riskiest part is proven first.',
  },
  {
    key: 'data-first',
    ask: 'Start from the persistence model - entities, keys, indexes, soft delete, audit columns, migration order - and derive the API and the UI from it.',
  },
  {
    key: 'thin-slice',
    ask: 'Design the smallest end-to-end increment that satisfies the highest-value acceptance criteria, then a follow-on increment for the rest. Sequence the two explicitly.',
  },
].slice(0, PANEL)

const RUBRICS = [
  {
    key: 'convention-fit',
    ask: 'Score how well this fits the documented conventions of THIS repository. Open CLAUDE.md and the relevant skills and check each decision against them. Penalise anything that would fail the architecture tests, skip a permission mirror, hard-code a user-visible string, or add a per-file using that belongs in the shared global-usings file.',
  },
  {
    key: 'coverage-risk',
    ask: 'Score acceptance-criterion coverage and delivery risk. Penalise unaddressed criteria, hand-waved error paths, missing tests, and migrations that would need a manual data fix.',
  },
  {
    key: 'simplicity',
    ask: 'Score simplicity and reversibility. Penalise new abstractions, new shared code, new configuration, and anything that increases the number of files a future change must touch.',
  },
]

// pipeline(), not parallel(): the three judges of framing A depend only on framing A,
// so A can be scored while B is still being written. The ranking below needs every
// framing, but pipeline() already resolves as a single array - no extra barrier.
const scored = await pipeline(
  FRAMINGS,
  (f) =>
    agent(
      [
        ORIENT,
        SKILL_MAP,
        `Design an implementation approach for the specification at ${SPEC_PATH}. Read it in full first.`,
        FOCUS ? `Caller steer: ${FOCUS}` : 'No additional caller steer.',
        `Your framing is "${f.key}". ${f.ask}`,
        'Explore the repository enough to name real paths. Do NOT write any file and do NOT modify code.',
        'For every acceptance criterion, either address it or list it under acsNotAddressed with the reason.',
        'Every decision needs a rationale and the alternative you rejected.',
      ].join('\n'),
      { label: `approach: ${f.key}`, phase: 'Design panel', schema: PROPOSAL_SCHEMA }
    ),
  (proposal, f) => {
    if (!proposal) return null
    // inner barrier: the mean score for this framing needs every rubric
    return parallel(
      RUBRICS.map((r) => () =>
        agent(
          [
            ORIENT,
            `Score one design proposal for the specification at ${SPEC_PATH}. Read the spec first.`,
            `Your rubric is "${r.key}". ${r.ask}`,
            'Score 0 to 10. Be harsh: 7 or above means you would ship it as written. Verify its claims against the repository.',
            'Name the single best idea in this proposal that a competing approach should steal.',
            '',
            JSON.stringify(proposal),
          ].join('\n'),
          { label: `judge ${f.key}/${r.key}`, phase: 'Design panel', schema: SCORE_SCHEMA }
        )
      )
    ).then((scores) => {
      const good = scores.filter(Boolean)
      const avg = good.length ? good.reduce((s, x) => s + (x.score || 0), 0) / good.length : 0
      if (good.length < RUBRICS.length) log(`Approach ${f.key}: only ${good.length}/${RUBRICS.length} judges returned.`)
      log(`Approach ${f.key}: mean score ${avg.toFixed(1)}.`)
      return { framing: f.key, proposal, scores: good, avg }
    })
  }
)

const ranked = scored.filter(Boolean).sort((a, b) => b.avg - a.avg)
if (!ranked.length) return { ok: false, error: 'design-panel-produced-nothing', specDir: SPEC_DIR }
const winner = ranked[0]
const runnersUp = ranked.slice(1)
log(`Winner: ${winner.framing} (${winner.avg.toFixed(1)}). Runners-up: ${runnersUp.map((r) => `${r.framing} ${r.avg.toFixed(1)}`).join(', ') || 'none'}.`)

// ---------------------------------------------------------------- Synthesize
// Take the winner, but do not take it uncritically: the weaknesses the judges found
// become mandatory fixes, and the single best idea each judge spotted in a losing
// approach is offered up for grafting. This is where a panel beats picking one design
// and running with it - the good idea in the third-place proposal still reaches plan.md.
phase('Synthesize')
const grafts = runnersUp.flatMap((r) =>
  r.scores.map((s) => s.bestIdeaWorthGrafting).filter(Boolean).map((idea) => `${r.framing}: ${idea}`)
)
const winnerWeaknesses = winner.scores.flatMap((s) => s.weaknesses || [])

const plan = await agent(
  [
    ORIENT,
    SKILL_MAP,
    `Write ${PLAN_PATH}. Read ${SPEC_PATH} first.`,
    '',
    `Base it on the winning approach "${winner.framing}":`,
    JSON.stringify(winner.proposal),
    '',
    'Weaknesses the judges found in the winner - you MUST resolve each one in the plan:',
    JSON.stringify(trim(winnerWeaknesses, 20, 'winner weaknesses')),
    '',
    'Ideas from the runner-up approaches worth grafting in - adopt the ones that improve the plan, and say which you adopted:',
    JSON.stringify(trim(grafts, 15, 'graft candidates')),
    '',
    'Structure of plan.md:',
    '  # Implementation plan - <feature>',
    '  ## Chosen approach        the headline, plus why it beat the alternatives (name them)',
    '  ## Architecture decisions a table: decision | rationale | rejected alternative',
    '  ## Workstreams            data, api, permissions, web, i18n, tests - what changes and which skill governs it',
    '  ## Sequencing             what must land before what, and why',
    '  ## Risks and mitigations',
    '  ## Explicitly not doing',
    'Cite real repo-relative paths. Do not write code, and do not create any other file.',
  ].join('\n'),
  { label: 'write plan.md', schema: PLAN_SCHEMA }
)
if (!plan) return { ok: false, error: 'plan-synthesis-failed', specDir: SPEC_DIR }

// ---------------------------------------------------------------- Contracts
// plan.md says what the shape is; the contracts pin down the specifics an implementer
// needs and a verifier can check - exact type names, exact routes, exact permission
// constants, exact translation keys, exact test names. Splitting them across four
// documents also means the four writers below never contend for a file.
phase('Contracts')
const CONTRACTS = [
  {
    key: 'data-model',
    file: 'data-model.md',
    skills: 'backend-entity and backend-feature',
    ask: [
      'Specify every entity, property, key, index, relationship and enum this feature needs.',
      'State which base class and which audit, soft-delete and normalized-property interfaces each entity uses, and why.',
      'Specify the EF configuration class per entity, the DbSet line to add to the application DbContext,',
      'the migration name, and anything a migration cannot do automatically (backfills, a non-nullable column on an existing table).',
      'Migrations are not shipped to generated projects, so state the command to run rather than the migration file contents.',
    ].join(' '),
  },
  {
    key: 'api-contract',
    file: 'api-contract.md',
    skills: 'backend-endpoint, backend-feature, permissions and api-error-handling',
    ask: [
      'Specify every endpoint: HTTP verb, route, route group and its prefix, request type, validator rules,',
      'response type and DTOs, mapper types, and the permission constant that guards it.',
      'Give the permission constant name and value, and where it must be declared in the owning feature provider.',
      'Give every error code this feature introduces, the condition that raises it, and the HTTP status.',
      'For list endpoints, state the whitelisted sortable fields and the paging defaults.',
      'Name types using the naming table in the coding-conventions guide. Do not write code - this is a contract document.',
    ].join(' '),
  },
  {
    key: 'frontend-contract',
    file: 'frontend-contract.md',
    skills: 'rtk-query-api, frontend-crud, frontend-page, ui-component, localization and permissions',
    ask: [
      'Specify the RTK Query endpoints to inject on the shared api slice, the DTO file and interface names mirroring the API,',
      'and the cache tag decisions: which endpoints provide tags, which invalidate them, which need none.',
      'Specify the locale-prefixed routes, their server page components and client components,',
      'the shared components reused versus created, the permission mirror entries, the route-guard entries,',
      'the navigation and global-search entries, and every translation key with its English value and its namespace.',
      'Do not write code - this is a contract document.',
    ].join(' '),
  },
  {
    key: 'test-plan',
    file: 'test-plan.md',
    skills: 'backend-tests and frontend-tests',
    ask: [
      'Specify the test for every acceptance criterion: which project it lives in, its file and test-method name,',
      'the fixture or seeder it uses, and the assertion that would fail if the behaviour were removed.',
      'Every unwanted-behaviour criterion needs a negative test. Note any criterion that cannot be tested automatically and say why.',
      'Respect the parallel-collection rule: a test must not depend on global database state it did not create.',
      'Do not write code - this is a contract document.',
    ].join(' '),
  },
]

// Barrier: the decomposer needs all four contracts at once to order tasks and
// compute file-level disjointness. Each writer owns a distinct filename, so the
// four can write concurrently without a lock.
const contractResults = await parallel(
  CONTRACTS.map((c) => () =>
    agent(
      [
        ORIENT,
        `Read ${SPEC_PATH} and ${PLAN_PATH}, then write ${SPEC_DIR}/${c.file}.`,
        `Read the ${c.skills} guides under .claude/skills first and follow their conventions.`,
        `Your contract is "${c.key}". ${c.ask}`,
        'Tie every element back to the acceptance criterion ids it serves.',
        'Under artifacts, list every repo-relative file this contract says must be created or edited, and the skill that governs it.',
        `Write ONLY ${SPEC_DIR}/${c.file}. Do not touch any other file, and do not modify application code.`,
        'If something genuinely cannot be decided without a human, list it under openQuestions rather than guessing.',
      ].join('\n'),
      { label: `contract: ${c.key}`, phase: 'Contracts', schema: CONTRACT_SCHEMA }
    )
  )
)

const goodContracts = contractResults.filter(Boolean)
if (goodContracts.length < CONTRACTS.length) {
  log(`${CONTRACTS.length - goodContracts.length} contract(s) failed to produce a document; decomposing from the rest.`)
}
if (!goodContracts.length) return { ok: false, error: 'contracts-produced-nothing', specDir: SPEC_DIR }
const contractQuestions = goodContracts.flatMap((c) => c.openQuestions || [])
if (contractQuestions.length) log(`Contracts raised ${contractQuestions.length} open question(s).`)

// ---------------------------------------------------------------- Decompose
// The critical output is not the task list but each task's declared file set: the next
// stage schedules tasks into concurrent waves purely on the basis that their file sets
// do not intersect, and hard-scopes each implementing agent to the files it declared.
// An under-declared file set is the one mistake that makes the next stage misbehave.
phase('Decompose')
let tasksDoc = await agent(
  [
    ORIENT,
    SKILL_MAP,
    HOTSPOT_NOTE,
    `Write ${TASKS_PATH}. Read ${SPEC_PATH}, ${PLAN_PATH} and every contract document in ${SPEC_DIR} first.`,
    '',
    'Artifacts the contracts declared:',
    JSON.stringify(trim(goodContracts.flatMap((c) => c.artifacts || []), 120, 'declared artifacts')),
    '',
    'Emit one task per atomic unit of work, ordered so that a task never precedes something it depends on.',
    'Use exactly this line format, one task per line:',
    '- [ ] **T-001** [P] Title - `files:` path, path - `skill:` name - `acs:` AC-001, AC-002 - `after:` T-000',
    'Rules:',
    '- "[P]" marks a task that may run beside its siblings; omit it when the task must run alone.',
    '- "files:" is a hard boundary - the implementing agent may edit nothing else, so it must be complete and exact.',
    '- Two tasks that can run together must share NO file. When they would, sequence one behind the other with "after:".',
    '- Every task cites at least one AC id. A task citing none is scope creep - drop it or find the criterion.',
    '- Every task names the governing skill.',
    '- Prefer many small tasks over few large ones, but never split a single file across two tasks.',
    '',
    'End the file with a "## Coverage" section: a table mapping every AC id to the task ids that satisfy it.',
    `Write ONLY ${TASKS_PATH}. Do not modify application code.`,
  ].join('\n'),
  { label: 'decompose into tasks', schema: TASKS_SCHEMA }
)

if (!tasksDoc || !tasksDoc.tasks || !tasksDoc.tasks.length) {
  return { ok: false, error: 'decomposition-failed', specDir: SPEC_DIR, planPath: PLAN_PATH }
}
log(`${tasksDoc.tasks.length} task(s) emitted, ${tasksDoc.tasks.filter((t) => t.parallel).length} marked parallel.`)

// ---------------------------------------------------------------- Coverage gate
// Traceability in both directions, plus a schedulability check. Forward: every
// acceptance criterion reaches at least one task, or the feature ships incomplete.
// Backward: every task serves a real criterion, or it is scope creep. Wave safety:
// no two concurrently-marked tasks share a file, or the next stage corrupts itself.
phase('Coverage gate')
const AUDITS = [
  {
    key: 'forward-coverage',
    ask: 'For every acceptance criterion in spec.md, find the task(s) that satisfy it. Report any criterion with no task as "uncovered-ac". A task that only mentions the criterion in passing does not count - the work must actually satisfy it.',
  },
  {
    key: 'backward-coverage',
    ask: 'For every task, check that the criteria it cites exist and that the work described genuinely serves them. Report a task citing no real criterion as "orphan-task". Also report tasks that duplicate each other.',
  },
  {
    key: 'wave-safety',
    ask: 'Check the file sets. Report as "unsafe-parallel" any two tasks that are both marked [P], are not sequenced by "after:", and share a file - including the hotspot files, which count as shared even when only one task names them. Report as "bad-order" any task whose dependencies come after it. Report as "bad-path" any declared path that does not match the layout described in CLAUDE.md.',
  },
]

// Audit, repair, re-audit - because a repair can introduce a new violation of a
// different audit. The loop ends clean, out of rounds, or out of budget, and says
// which: leaving unresolved findings unreported would read like a clean gate.
let clean = false
let findings = []
let gateRound = 0
while (gateRound < MAX_GATE_ROUNDS) {
  // Barrier: a repair driven by one auditor alone re-breaks another's invariant.
  const audits = await parallel(
    AUDITS.map((a) => () =>
      agent(
        [
          ORIENT,
          HOTSPOT_NOTE,
          `Audit ${TASKS_PATH} against ${SPEC_PATH}, ${PLAN_PATH} and the contract documents in ${SPEC_DIR}.`,
          `Your audit is "${a.key}". ${a.ask}`,
          'Every finding needs a concrete fix. Report only your own dimension. Do not edit any file.',
        ].join('\n'),
        { label: `audit: ${a.key} r${gateRound + 1}`, phase: 'Coverage gate', schema: AUDIT_SCHEMA }
      )
    )
  )

  findings = audits.filter(Boolean).flatMap((a) => a.findings || [])
  if (!findings.length) {
    clean = true
    log(`Coverage gate clean after ${gateRound + 1} round(s).`)
    break
  }
  log(`Gate round ${gateRound + 1}: ${findings.length} finding(s); repairing tasks.md.`)

  if (budget.total && budget.remaining() < 80000) {
    log(`Stopping the coverage gate with ${findings.length} finding(s) unresolved: ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
    break
  }

  const repaired = await agent(
    [
      ORIENT,
      SKILL_MAP,
      HOTSPOT_NOTE,
      `Repair ${TASKS_PATH} so every finding below is resolved. Read ${SPEC_PATH}, ${PLAN_PATH} and the contract documents in ${SPEC_DIR} first.`,
      '- Never renumber an existing task id. Amend in place, or append new ids at the end and fix the ordering through "after:".',
      '- Keep the line format exactly, and refresh the "## Coverage" table.',
      '- If a finding is wrong, leave the task alone and add a one-line HTML comment in tasks.md explaining why.',
      '',
      JSON.stringify(trim(findings, 60, 'gate findings')),
    ].join('\n'),
    { label: `repair tasks r${gateRound + 1}`, phase: 'Coverage gate', schema: TASKS_SCHEMA }
  )
  if (!repaired) {
    log('The repair agent failed; keeping the previous tasks.md.')
    break
  }
  tasksDoc = repaired
  gateRound++
}

if (!clean && findings.length) {
  log(`Coverage gate exited with ${findings.length} unresolved finding(s) after ${gateRound} repair round(s).`)
}

return {
  ok: true,
  specDir: SPEC_DIR,
  planPath: PLAN_PATH,
  tasksPath: TASKS_PATH,
  contracts: goodContracts.map((c) => c.path),
  chosenApproach: winner.framing,
  approachScores: ranked.map((r) => ({ framing: r.framing, score: Number(r.avg.toFixed(1)) })),
  runnersUpGrafted: plan.grafted || [],
  taskCount: tasksDoc.tasks.length,
  parallelTaskCount: tasksDoc.tasks.filter((t) => t.parallel).length,
  coverageClean: clean,
  uncoveredAcs: findings.filter((f) => f.kind === 'uncovered-ac').map((f) => f.ref),
  orphanTasks: findings.filter((f) => f.kind === 'orphan-task').map((f) => f.ref),
  unresolvedFindings: clean ? [] : findings,
  needsClarification: contractQuestions,
  nextStep: `Run spec-implement with args {specDir: "${SPEC_DIR}"}.`,
}
