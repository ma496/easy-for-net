export const meta = {
  name: 'spec-plan',
  description: 'Turn specs/<NNN-slug>/spec.md into plan.md, four contract documents and a coverage-gated tasks.md',
  whenToUse: 'Second stage of spec-driven development, once the spec has no unanswered open questions. Produces the implementation plan and the task list spec-implement consumes.',
  phases: [
    { title: 'Load spec', detail: 'parse the acceptance criteria and check for unresolved blockers' },
    { title: 'Design panel', detail: 'independent approaches from different framings, ranked head to head' },
    { title: 'Synthesize', detail: 'write plan.md from the winner, grafting the best runner-up ideas' },
    { title: 'Contracts', detail: 'data model, API, frontend and test contracts in parallel' },
    { title: 'Decompose', detail: 'ordered atomic tasks with file sets, skills, criterion ids and parallel markers' },
    { title: 'Coverage gate', detail: 'exact traceability and wave-safety checks in code, plus a judgment audit, then repair' },
  ],
}

// =============================================================================
// STAGE 2 OF 4 - the specification becomes a design and a work list.
//
//   /specify -> /plan -> spec-plan -> /implement -> /verify
//
// What it does
//   Generates several genuinely different designs in parallel, ranks them head to head
//   against this repository's own conventions, writes plan.md from the winner while
//   grafting the best ideas from the losers, expands it into four contract documents,
//   and decomposes those into an ordered tasks.md that is then gated for coverage.
//
// Why a judge panel rather than one design, iterated
//   The solution space here is wide - extend an existing slice, or add a new one; lead
//   with the data model, or with the riskiest constraint. One attempt refined in place
//   tends to entrench whichever framing it happened to start from. Independent attempts
//   ranked by independent rubrics surface the trade-off instead of hiding it, and
//   plan.md records why the winner beat the alternatives by name.
//
// Why the judges rank rather than score
//   Three judges each scoring five proposals out of ten is fifteen agents producing
//   numbers with no shared anchor - one judge's 7 is another's 5, and the mean the
//   ranking used to be built from was averaging incomparable quantities. A judge that
//   sees every proposal at once and orders them is both cheaper (one agent per rubric
//   rather than one per rubric per proposal) and better calibrated, because it is doing
//   the comparison the script actually needs instead of a scoring exercise the script
//   then has to compare for it.
//
// Why the coverage gate is mostly not a language model any more
//   Whether every acceptance criterion reaches a task, whether a task cites a criterion
//   that exists, whether the dependency graph has a cycle, and whether two concurrent
//   tasks share a file are all set operations over data this script already holds. Code
//   answers them exactly and for free; an auditor agent answered them approximately and
//   for the price of an agent per dimension per round. One auditor remains, for the two
//   questions that genuinely need judgment - and it is also the thing that re-reads
//   tasks.md, so the exact checks run against the written file rather than against the
//   decomposer's own account of what it wrote.
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
//   Roughly 16 subagents at the default depth, against 27 before.
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

// Prepended to every prompt. CLAUDE.md is deliberately not mentioned: the workflow
// runtime injects it into every subagent already, so asking for it buys a duplicate
// read of the largest document in the repository once per agent.
//
// The namespace line stays because it is not something CLAUDE.md tells the agent. It
// is a guard specific to this template: this script is copied verbatim into scaffolded
// projects while only markdown gets namespace-rewritten, so anything repo-specific
// belongs in the markdown and an agent must never guess it from here.
const ORIENT = 'Never invent a root namespace, project file name or solution file name - read them from the repo.'

// Added only for agents judging or applying a convention, which is a different test
// from whether the agent writes a file: the repository-fit rubric edits nothing and
// needs this more than most.
const CONVENTIONS = 'Read .claude/skills/coding-conventions/SKILL.md and the relevant guide under .claude/skills before deciding any convention question.'

// Added only for the agents that must reproduce the tasks.md line format or work from
// the completeness checklist. Everyone else gets the specific rule inlined, which is
// cheaper than a 10 KB read.
const PROCESS = 'Read .claude/skills/spec-driven/SKILL.md for the tasks.md line format and the completeness checklist.'

// The task decomposer must name a governing guide for every task, and the implementing
// agent in the next stage reads that guide before it writes a line. This table is how
// the two stages agree on which guide covers which kind of work.
//
// It is data rather than prose because the coverage gate validates against it: a task
// naming a guide that does not exist would send the next stage to read a missing file.
// The prose the agents see is derived from it just below.
const SKILL_ROUTES = [
  { kind: 'new vertical slice, or a cross-feature dependency', skill: 'backend-feature' },
  { kind: 'entity, EF configuration, DbSet, migration', skill: 'backend-entity' },
  { kind: 'HTTP endpoint, request, validator, mapper', skill: 'backend-endpoint' },
  { kind: 'permission constant, provider, web mirror', skill: 'permissions' },
  { kind: 'error code and its user-facing message', skill: 'api-error-handling' },
  { kind: 'integration tests for the API', skill: 'backend-tests' },
  { kind: 'RTK Query slice and DTOs', skill: 'rtk-query-api' },
  { kind: 'list, create, update and delete screens', skill: 'frontend-crud' },
  { kind: 'a single new route or screen', skill: 'frontend-page' },
  { kind: 'shared React component', skill: 'ui-component' },
  { kind: 'client-only state', skill: 'redux-state' },
  { kind: 'translation keys and locale files', skill: 'localization' },
  { kind: 'vitest tests', skill: 'frontend-tests' },
  { kind: 'Hangfire work', skill: 'background-jobs' },
  { kind: 'in-app notifications', skill: 'notifications' },
  { kind: 'uploads and binary content', skill: 'file-storage' },
  { kind: 'anything at all, as a second reference', skill: 'coding-conventions' },
]
const KNOWN_SKILLS = SKILL_ROUTES.map((r) => r.skill)
const SKILL_MAP = ['Skill routing - name the guide each task must be executed with:']
  .concat(SKILL_ROUTES.map((r) => `  ${r.kind} -> ${r.skill}`))
  .join('\n')

// Hotspots are the files nearly every feature appends to. Two tasks editing one of
// them merge cleanly at the text level and then fail to compile, or silently lose one
// of the two edits. The planner must therefore give each hotspot a single owning task.
//
// KEEP IN SYNC with HOTSPOT_BASENAMES in .claude/workflows/spec-implement.js. These
// scripts cannot import each other, so this is a genuine copy. The gate below must
// never be looser than the scheduler there, or the planner will pass a task list the
// implementer then has to serialise behind the planner's back.
//
// Barrel index files are matched by name here because two tasks appending exports to
// any barrel is the same hazard; the scheduler in the next stage collides them by full
// path instead, which is stricter per file and looser across files. Being stricter in
// the gate is the safe direction.
const HOTSPOT_BASENAMES = [
  'appdbcontext.cs',
  'allow.cs',
  'meta.cs',
  'program.cs',
  'errorcodes.cs',
  'appsettings.json',
  'appsettings.development.json',
  'appsettings.testing.json',
  'allow.ts',
  'auth-urls.ts',
  'nav-items.ts',
  'searchable-items.ts',
  'index.ts',
]

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

// The ranking carries weaknesses and a graft candidate per proposal, not just an order.
// Synthesize consumes both - the winner's weaknesses become mandatory fixes in plan.md
// and the losers' best ideas are offered up for grafting - so a schema that returned a
// bare ordering would quietly strip the panel of the thing that justifies running it.
const RANK_SCHEMA = {
  type: 'object',
  properties: {
    rubric: { type: 'string' },
    ranking: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          framing: { type: 'string' },
          rank: { type: 'number', description: '1 is best; no ties' },
          strengths: { type: 'array', items: { type: 'string' } },
          weaknesses: { type: 'array', items: { type: 'string' } },
          bestIdeaWorthGrafting: { type: 'string' },
        },
        required: ['framing', 'rank', 'weaknesses'],
      },
    },
    verdict: { type: 'string', description: 'one or two sentences on why the top proposal beat the second' },
  },
  required: ['rubric', 'ranking'],
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

const TASK_ITEM = {
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
}

const TASKS_SCHEMA = {
  type: 'object',
  properties: {
    tasksPath: { type: 'string' },
    tasks: { type: 'array', items: TASK_ITEM },
  },
  required: ['tasksPath', 'tasks'],
}

// The auditor returns the task list it PARSED OUT OF THE FILE, not the list it was
// given. That is the point of the stage: the deterministic checks below then run
// against what tasks.md actually says, so a decomposer whose self-report diverges from
// the file it wrote cannot produce a clean gate.
const AUDIT_SCHEMA = {
  type: 'object',
  properties: {
    audit: { type: 'string' },
    parsedTasks: { type: 'array', items: TASK_ITEM },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: {
            type: 'string',
            enum: ['uncovered-ac', 'orphan-task', 'bad-path', 'bad-order', 'unsafe-parallel', 'missing-skill', 'under-declared-files', 'duplicate-task', 'other'],
          },
          ref: { type: 'string', description: 'an AC id, a task id, or a file path' },
          problem: { type: 'string' },
          fix: { type: 'string' },
        },
        required: ['kind', 'ref', 'problem', 'fix'],
      },
    },
  },
  required: ['audit', 'parsedTasks', 'findings'],
}

function trim(list, n, what) {
  const arr = Array.isArray(list) ? list : []
  if (arr.length > n) log(`Dropped ${arr.length - n} of ${arr.length} ${what} when building a prompt (kept ${n}).`)
  return arr.slice(0, n)
}

// ---------------------------------------------------------------- deterministic gate helpers
// tasks.md is written by a model, so paths arrive with mixed separators, stray "./"
// prefixes and inconsistent casing. Normalising before comparison is what stops two
// spellings of the same file from looking disjoint.
//
// KEEP IN SYNC with normPath and claimTokens in spec-implement.js - see the note on
// HOTSPOT_BASENAMES above.
function normPath(p) {
  return String(p || '').trim().replace(/\\/g, '/').replace(/^\.\//, '').replace(/^\/+/, '').toLowerCase()
}

// The set of things a task exclusively claims: each declared file, plus a shared token
// for any hotspot it touches, plus one "hot:locales" token if it touches any locale
// file at all - two tasks adding keys to different locale files of the same translation
// still have to be serialised, because they are really editing one logical thing.
function claimTokens(task) {
  const out = []
  for (const f of task.files || []) {
    const n = normPath(f)
    if (!n) continue
    out.push(n)
    const base = n.split('/').pop()
    if (HOTSPOT_BASENAMES.indexOf(base) !== -1) out.push(`hot:${base}`)
    if (/\/locales\/[a-z-]+\.json$/.test(n)) out.push('hot:locales')
  }
  return out
}

// Transitive dependency closure, so "is A sequenced behind B?" is answered properly
// rather than by looking only at direct "after:" entries. Also the cycle detector: a
// node reachable from itself is a cycle, which is exactly what makes the next stage's
// scheduler stall.
function reachability(tasks) {
  const direct = new Map(tasks.map((t) => [t.id, (t.dependsOn || []).filter((d) => tasks.some((x) => x.id === d))]))
  const closure = new Map()
  // A plain breadth-first walk per node, with no memoisation across nodes. Caching a
  // partial result computed under one traversal path is what makes a shared-ancestor
  // graph come out with an incomplete closure, and an incomplete closure here would
  // report two properly-sequenced tasks as an unsafe pair. Task lists are tens of
  // entries, so the quadratic walk costs nothing worth optimising.
  for (const t of tasks) {
    const seen = new Set()
    const queue = (direct.get(t.id) || []).slice()
    while (queue.length) {
      const next = queue.shift()
      if (seen.has(next)) continue
      seen.add(next)
      for (const d of direct.get(next) || []) queue.push(d)
    }
    closure.set(t.id, seen)
  }
  return closure
}

// Everything a language model used to be asked and code can answer exactly. Each of
// these was previously one third of an auditor agent's job, done approximately, once
// per gate round.
function deterministicFindings(tasks, acIds) {
  const findings = []
  const add = (kind, ref, problem, fix) => findings.push({ kind, ref, problem, fix, source: 'exact' })
  const acSet = new Set(acIds)
  const taskIds = new Set(tasks.map((t) => t.id))

  // Forward coverage: every acceptance criterion must reach at least one task, or the
  // feature ships incomplete.
  const covered = new Set()
  for (const t of tasks) for (const a of t.acs || []) covered.add(a)
  for (const id of acIds) {
    if (!covered.has(id)) add('uncovered-ac', id, `No task in tasks.md cites ${id}.`, `Add a task that satisfies ${id}, or fold it into an existing task's "acs:" list if that task already does the work.`)
  }

  // Backward coverage and per-task well-formedness.
  for (const t of tasks) {
    if (!(t.acs || []).length) {
      add('orphan-task', t.id, `${t.id} cites no acceptance criterion.`, 'Cite the criterion this task serves, or delete the task - work no criterion asked for is scope creep by definition.')
    }
    for (const a of t.acs || []) {
      if (!acSet.has(a)) add('orphan-task', t.id, `${t.id} cites ${a}, which is not an acceptance criterion in spec.md.`, `Correct the id, or drop it from ${t.id}.`)
    }
    if (!t.skill) add('missing-skill', t.id, `${t.id} names no governing guide.`, 'Name the guide from the skill routing table.')
    else if (KNOWN_SKILLS.indexOf(t.skill) === -1) {
      add('missing-skill', t.id, `${t.id} names the guide "${t.skill}", which is not one of the guides under .claude/skills.`, `Use one of: ${KNOWN_SKILLS.join(', ')}.`)
    }
    if (!(t.files || []).length) add('other', t.id, `${t.id} declares no files.`, 'Declare the exact files this task may edit - the next stage uses that list as a hard boundary.')
    for (const d of t.dependsOn || []) {
      if (!taskIds.has(d)) add('bad-order', t.id, `${t.id} declares "after: ${d}", but no task ${d} exists.`, `Point it at a real task id, or remove the dependency.`)
    }
  }

  // Ordering: cycles, and dependencies declared on tasks that come later in the file.
  const closure = reachability(tasks)
  for (const t of tasks) {
    if ((closure.get(t.id) || new Set()).has(t.id)) {
      add('bad-order', t.id, `${t.id} is part of a dependency cycle.`, 'Break the cycle - the scheduler in the next stage cannot place any task in it and will drop all of them.')
    }
  }

  // Wave safety, checked pairwise rather than by simulating the scheduler, so the
  // answer does not depend on the wave-width cap the next stage happens to use.
  const parallelTasks = tasks.filter((t) => t.parallel !== false)
  const tokens = new Map(parallelTasks.map((t) => [t.id, claimTokens(t)]))
  for (let i = 0; i < parallelTasks.length; i++) {
    for (let j = i + 1; j < parallelTasks.length; j++) {
      const a = parallelTasks[i]
      const b = parallelTasks[j]
      const ordered = (closure.get(a.id) || new Set()).has(b.id) || (closure.get(b.id) || new Set()).has(a.id)
      if (ordered) continue
      const shared = (tokens.get(a.id) || []).filter((x) => (tokens.get(b.id) || []).indexOf(x) !== -1)
      if (shared.length) {
        add(
          'unsafe-parallel',
          `${a.id}+${b.id}`,
          `${a.id} and ${b.id} are both marked [P], nothing sequences one behind the other, and they share: ${shared.join(', ')}.`,
          `Give the shared file a single owning task and put the other behind it with "after:", or drop [P] from one of them.`
        )
      }
    }
  }

  // Cheap duplicate flag: identical declared file sets and near-identical titles.
  // Judging whether two differently-worded tasks are really the same job stays with the
  // auditor; this catches only the unambiguous case.
  for (let i = 0; i < tasks.length; i++) {
    for (let j = i + 1; j < tasks.length; j++) {
      const fa = (tasks[i].files || []).map(normPath).sort().join('|')
      const fb = (tasks[j].files || []).map(normPath).sort().join('|')
      if (fa && fa === fb && String(tasks[i].title || '').toLowerCase() === String(tasks[j].title || '').toLowerCase()) {
        add('duplicate-task', `${tasks[i].id}+${tasks[j].id}`, `${tasks[i].id} and ${tasks[j].id} have the same title and the same file set.`, 'Merge them.')
      }
    }
  }

  return findings
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
const AC_IDS = spec.acs.map((a) => a.id)
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
    conventions: true,
    ask: 'Rank by how well each fits the documented conventions of THIS repository. Check each proposal\'s decisions against the guides. Penalise anything that would fail the architecture tests, skip a permission mirror, hard-code a user-visible string, or add a per-file using that belongs in the shared global-usings file.',
  },
  {
    key: 'coverage-risk',
    conventions: false,
    ask: 'Rank by acceptance-criterion coverage and delivery risk. Penalise unaddressed criteria, hand-waved error paths, missing tests, and migrations that would need a manual data fix.',
  },
  {
    key: 'simplicity',
    conventions: false,
    ask: 'Rank by simplicity and reversibility. Penalise new abstractions, new shared code, new configuration, and anything that increases the number of files a future change must touch.',
  },
]

// Barrier: a judge cannot rank proposals it has not seen, so every framing must report
// before any judge starts. This is the textbook case for a barrier rather than a
// pipeline - the next stage genuinely needs cross-item context.
const proposals = (
  await parallel(
    FRAMINGS.map((f) => () =>
      agent(
        [
          ORIENT,
          CONVENTIONS,
          SKILL_MAP,
          `Design an implementation approach for the specification at ${SPEC_PATH}. Read it in full first.`,
          FOCUS ? `Caller steer: ${FOCUS}` : 'No additional caller steer.',
          `Your framing is "${f.key}". ${f.ask}`,
          'Explore the repository enough to name real paths. Do NOT write any file and do NOT modify code.',
          'For every acceptance criterion, either address it or list it under acsNotAddressed with the reason.',
          'Every decision needs a rationale and the alternative you rejected.',
        ].join('\n'),
        { label: `approach: ${f.key}`, phase: 'Design panel', schema: PROPOSAL_SCHEMA }
      )
    )
  )
).filter(Boolean)

if (!proposals.length) return { ok: false, error: 'design-panel-produced-nothing', specDir: SPEC_DIR }

// Compute the real coverage gap for each proposal here rather than asking a judge to
// eyeball a hundred criteria against five proposals. The judges get the answer and can
// spend their attention on whether the gaps matter.
const coverageByFraming = new Map(
  proposals.map((p) => {
    const addressed = new Set(p.acsAddressed || [])
    return [p.framing, AC_IDS.filter((id) => !addressed.has(id))]
  })
)

// One agent per rubric, each ranking every proposal head to head.
//
// The presentation order is rotated by judge index. Every judge seeing the same fixed
// order would give all of them the same primacy bias, which averaging cannot remove
// because it is systematic rather than noise. Rotation is index-driven - no clock, no
// randomness - so a resumed run replays identically.
const rankings = (
  await parallel(
    RUBRICS.map((r, i) => () => {
      const ordered = proposals.slice(i % proposals.length).concat(proposals.slice(0, i % proposals.length))
      return agent(
        [
          ORIENT,
          r.conventions ? CONVENTIONS : '',
          `Rank the competing design proposals below for the specification at ${SPEC_PATH}. Read the spec first.`,
          `Your rubric is "${r.key}". ${r.ask}`,
          'Rank them 1 to N with no ties, where 1 is the one you would ship. Judge them against each other, not against an absolute standard.',
          'Be harsh, and verify their claims against the repository rather than taking them at face value.',
          'For EVERY proposal, including the winner, list its weaknesses under this rubric and name the single best idea in it that a competing approach should steal.',
          'The proposals are presented in an arbitrary order that carries no information about quality.',
          '',
          'Acceptance criteria each proposal leaves unaddressed, computed from the spec - this is fact, not a claim:',
          JSON.stringify(
            ordered.map((p) => ({ framing: p.framing, unaddressedAcs: trim(coverageByFraming.get(p.framing) || [], 40, `${p.framing} unaddressed criteria`) }))
          ),
          '',
          'The proposals:',
          JSON.stringify(ordered),
        ]
          .filter(Boolean)
          .join('\n'),
        { label: `rank: ${r.key}`, phase: 'Design panel', schema: RANK_SCHEMA, effort: 'medium' }
      )
    })
  )
).filter(Boolean)

if (!rankings.length) return { ok: false, error: 'no-judge-returned', specDir: SPEC_DIR }
if (rankings.length < RUBRICS.length) log(`Only ${rankings.length}/${RUBRICS.length} judges returned; ranking on the votes received.`)

// Rank-sum aggregation: each judge contributes the position it put a proposal in, and
// the lowest total wins. Unlike averaging three independent 0-to-10 scores, this needs
// no shared anchor between judges - it only needs each judge to be internally
// consistent, which is the thing a single agent comparing proposals side by side is
// actually good at. A proposal a judge did not rank is charged last place, so silence
// is never an advantage.
const lastPlace = proposals.length + 1
const scored = proposals.map((p) => {
  const rows = rankings.map((r) => (r.ranking || []).find((x) => x.framing === p.framing)).filter(Boolean)
  const rankSum = rankings.reduce((sum, r) => {
    const row = (r.ranking || []).find((x) => x.framing === p.framing)
    return sum + (row && row.rank ? row.rank : lastPlace)
  }, 0)
  return {
    framing: p.framing,
    proposal: p,
    rankSum,
    weaknesses: rows.flatMap((x) => x.weaknesses || []),
    grafts: rows.map((x) => x.bestIdeaWorthGrafting).filter(Boolean),
  }
})

const ranked = scored.sort((a, b) => a.rankSum - b.rankSum)
const winner = ranked[0]
const runnersUp = ranked.slice(1)
log(`Winner: ${winner.framing} (rank sum ${winner.rankSum}). Runners-up: ${runnersUp.map((r) => `${r.framing} ${r.rankSum}`).join(', ') || 'none'}.`)

// ---------------------------------------------------------------- Synthesize
// Take the winner, but do not take it uncritically: the weaknesses the judges found
// become mandatory fixes, and the single best idea each judge spotted in a losing
// approach is offered up for grafting. This is where a panel beats picking one design
// and running with it - the good idea in the third-place proposal still reaches plan.md.
phase('Synthesize')
const grafts = runnersUp.flatMap((r) => r.grafts.map((idea) => `${r.framing}: ${idea}`))
const winnerGaps = coverageByFraming.get(winner.framing) || []
if (winnerGaps.length) log(`The winning approach leaves ${winnerGaps.length} criteria unaddressed; plan.md must close them.`)

const plan = await agent(
  [
    ORIENT,
    CONVENTIONS,
    SKILL_MAP,
    `Write ${PLAN_PATH}. Read ${SPEC_PATH} first.`,
    '',
    `Base it on the winning approach "${winner.framing}":`,
    JSON.stringify(winner.proposal),
    '',
    'Weaknesses the judges found in the winner - you MUST resolve each one in the plan:',
    JSON.stringify(trim(winner.weaknesses, 24, 'winner weaknesses')),
    '',
    'Acceptance criteria the winning approach did not address - the plan MUST cover every one of them:',
    JSON.stringify(trim(winnerGaps, 40, 'unaddressed criteria')),
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
  { label: 'write plan.md', schema: PLAN_SCHEMA, effort: 'high' }
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
        CONVENTIONS,
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

// Deduplicate the declared artifacts by normalised path before the list is trimmed.
// The hotspot files appear in two or three contracts each, so without this the cap
// below binds on a large feature and the decomposer silently plans from a truncated
// list - which the gate would then have to rediscover.
const artifactByPath = new Map()
for (const c of goodContracts) {
  for (const a of c.artifacts || []) {
    const key = normPath(a.file)
    if (!key) continue
    if (!artifactByPath.has(key)) artifactByPath.set(key, { ...a, contracts: [c.contract] })
    else artifactByPath.get(key).contracts.push(c.contract)
  }
}
const declaredArtifacts = Array.from(artifactByPath.values())
const rawArtifactCount = goodContracts.reduce((n, c) => n + (c.artifacts || []).length, 0)
if (rawArtifactCount !== declaredArtifacts.length) {
  log(`Contracts declared ${rawArtifactCount} artifacts covering ${declaredArtifacts.length} distinct files.`)
}

// ---------------------------------------------------------------- Decompose
// The critical output is not the task list but each task's declared file set: the next
// stage schedules tasks into concurrent waves purely on the basis that their file sets
// do not intersect, and hard-scopes each implementing agent to the files it declared.
// An under-declared file set is the one mistake that makes the next stage misbehave.
phase('Decompose')
let tasksDoc = await agent(
  [
    ORIENT,
    PROCESS,
    SKILL_MAP,
    HOTSPOT_NOTE,
    `Write ${TASKS_PATH}. Read ${SPEC_PATH}, ${PLAN_PATH} and every contract document in ${SPEC_DIR} first.`,
    '',
    'Distinct files the contracts declared, deduplicated across contracts:',
    JSON.stringify(trim(declaredArtifacts, 200, 'declared artifacts')),
    '',
    'Emit one task per atomic unit of work, ordered so that a task never precedes something it depends on.',
    'Use exactly this line format, one task per line:',
    '- [ ] **T-001** [P] Title - `files:` path, path - `skill:` name - `acs:` AC-001, AC-002 - `after:` T-000',
    'Rules:',
    '- "[P]" marks a task that may run beside its siblings; omit it when the task must run alone.',
    '- "files:" is a hard boundary - the implementing agent may edit nothing else, so it must be complete and exact.',
    '- Two tasks that can run together must share NO file. When they would, sequence one behind the other with "after:".',
    '- Every task cites at least one AC id. A task citing none is scope creep - drop it or find the criterion.',
    `- Every task names a governing guide, and it must be one of: ${KNOWN_SKILLS.join(', ')}.`,
    '- Every file the contracts declared must be claimed by exactly one task.',
    '- Prefer many small tasks over few large ones, but never split a single file across two tasks.',
    '',
    'End the file with a "## Coverage" section: a table mapping every AC id to the task ids that satisfy it.',
    `Write ONLY ${TASKS_PATH}. Do not modify application code.`,
  ].join('\n'),
  { label: 'decompose into tasks', schema: TASKS_SCHEMA, effort: 'high' }
)

if (!tasksDoc || !tasksDoc.tasks || !tasksDoc.tasks.length) {
  return { ok: false, error: 'decomposition-failed', specDir: SPEC_DIR, planPath: PLAN_PATH }
}
log(`${tasksDoc.tasks.length} task(s) emitted, ${tasksDoc.tasks.filter((t) => t.parallel).length} marked parallel.`)

// ---------------------------------------------------------------- Coverage gate
// Traceability in both directions plus a schedulability check, and almost all of it is
// arithmetic. The one agent left in the loop does two things code cannot: it re-reads
// tasks.md so the exact checks run against the file rather than the decomposer's
// account of it, and it answers the two questions that need judgment - whether a task
// really serves the criteria it cites, and whether its declared file list is complete.
//
// That last one is the important one. Code can only see the files a task DECLARED, so
// it is structurally blind to the under-declaration the header calls the one mistake
// that breaks the next stage. Only a reader who understands the work can catch it.
phase('Coverage gate')
let clean = false
let findings = []
let gateRound = 0
let gateTasks = tasksDoc.tasks

while (gateRound < MAX_GATE_ROUNDS) {
  const audit = await agent(
    [
      ORIENT,
      CONVENTIONS,
      PROCESS,
      HOTSPOT_NOTE,
      `Read ${TASKS_PATH} as it now stands, then audit it against ${SPEC_PATH}, ${PLAN_PATH} and the contract documents in ${SPEC_DIR}.`,
      '',
      'First, parse the file. Return every task line under parsedTasks exactly as written -',
      'id, title, the declared files, the skill, the cited criterion ids, the "after:" dependencies, and whether it carries [P].',
      'Report what the FILE says, not what it ought to say; a separate exact check runs against your parse.',
      '',
      'Then report findings on the three things only a careful reader can judge:',
      '1. UNDER-DECLARED FILES. For each task, name any file it will certainly have to create or edit that is missing from its "files:" list.',
      '   The next stage forbids a task from touching anything it did not declare, so an incomplete list stalls the work.',
      '   Look especially for the second half of a paired change: a permission constant without its web mirror, an entity without the DbSet line,',
      '   a new type without its registration, a user-visible string without the locale files, a new component without its barrel export.',
      '   Report these as "under-declared-files".',
      '2. WORK THAT DOES NOT MATCH ITS CRITERIA. For each task, check the work described genuinely satisfies the criteria it cites,',
      '   rather than merely mentioning them. Report as "orphan-task".',
      '3. PATHS THAT DO NOT FIT THE LAYOUT. Report any declared path that does not match the project layout as "bad-path".',
      '',
      'Coverage arithmetic, dependency cycles and file collisions are checked separately and exactly - do not spend effort on them.',
      'Do not edit any file.',
    ].join('\n'),
    { label: `audit tasks r${gateRound + 1}`, phase: 'Coverage gate', schema: AUDIT_SCHEMA, effort: 'high' }
  )

  // Fall back to the decomposer's self-report only if the auditor died, and say so:
  // checking the self-report is weaker than checking the file, and a gate that
  // quietly downgraded itself would read exactly like a gate that passed.
  const parsed = audit && audit.parsedTasks && audit.parsedTasks.length ? audit.parsedTasks : null
  if (!parsed) {
    log(`Gate round ${gateRound + 1}: the auditor returned no parse of ${TASKS_PATH}; checking the decomposer's own task list instead, which is weaker.`)
  }
  gateTasks = parsed || gateTasks

  const exact = deterministicFindings(gateTasks, AC_IDS)
  const judged = (audit && audit.findings) || []
  findings = exact.concat(judged)

  if (!findings.length) {
    clean = true
    log(`Coverage gate clean after ${gateRound + 1} round(s): ${gateTasks.length} tasks, all ${AC_IDS.length} criteria covered.`)
    break
  }
  const byKind = {}
  findings.forEach((f) => (byKind[f.kind] = (byKind[f.kind] || 0) + 1))
  log(`Gate round ${gateRound + 1}: ${exact.length} exact and ${judged.length} judged finding(s) - ${Object.keys(byKind).map((k) => `${k} ${byKind[k]}`).join(', ')}.`)

  if (budget.total && budget.remaining() < 80000) {
    log(`Stopping the coverage gate with ${findings.length} finding(s) unresolved: ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
    break
  }
  if (gateRound + 1 >= MAX_GATE_ROUNDS) break

  const repaired = await agent(
    [
      ORIENT,
      PROCESS,
      SKILL_MAP,
      HOTSPOT_NOTE,
      `Repair ${TASKS_PATH} so every finding below is resolved. Read ${SPEC_PATH}, ${PLAN_PATH} and the contract documents in ${SPEC_DIR} first.`,
      '- Findings marked source "exact" were computed from the file itself and are not opinions. Fix them.',
      '- Never renumber an existing task id. Amend in place, or append new ids at the end and fix the ordering through "after:".',
      '- Keep the line format exactly, and refresh the "## Coverage" table.',
      '- If a judged finding is wrong, leave the task alone and add a one-line HTML comment in tasks.md explaining why.',
      '',
      JSON.stringify(trim(findings, 80, 'gate findings')),
    ].join('\n'),
    { label: `repair tasks r${gateRound + 1}`, phase: 'Coverage gate', schema: TASKS_SCHEMA }
  )
  if (!repaired) {
    log('The repair agent failed; keeping the previous tasks.md.')
    break
  }
  tasksDoc = repaired
  gateTasks = repaired.tasks || gateTasks
  gateRound++
}

if (!clean && findings.length) {
  log(`Coverage gate exited with ${findings.length} unresolved finding(s) after ${gateRound} repair round(s).`)
}

// One last exact check the gate above cannot make, because it compares two different
// documents: every file the contracts said must change should be claimed by some task.
// A file the contracts declared and no task owns is work that will simply not happen.
const claimedFiles = new Set(gateTasks.flatMap((t) => (t.files || []).map(normPath)))
const unclaimed = declaredArtifacts.map((a) => normPath(a.file)).filter((f) => f && !claimedFiles.has(f))
if (unclaimed.length) {
  log(`${unclaimed.length} file(s) the contracts declared are claimed by no task: ${trim(unclaimed, 12, 'unclaimed files').join(', ')}`)
}

return {
  ok: true,
  specDir: SPEC_DIR,
  planPath: PLAN_PATH,
  tasksPath: TASKS_PATH,
  contracts: goodContracts.map((c) => c.path),
  chosenApproach: winner.framing,
  approachScores: ranked.map((r) => ({ framing: r.framing, rankSum: r.rankSum })),
  runnersUpGrafted: plan.grafted || [],
  taskCount: gateTasks.length,
  parallelTaskCount: gateTasks.filter((t) => t.parallel).length,
  coverageClean: clean,
  uncoveredAcs: findings.filter((f) => f.kind === 'uncovered-ac').map((f) => f.ref),
  orphanTasks: findings.filter((f) => f.kind === 'orphan-task').map((f) => f.ref),
  underDeclaredFiles: findings.filter((f) => f.kind === 'under-declared-files').map((f) => f.ref),
  unclaimedContractFiles: unclaimed,
  unresolvedFindings: clean ? [] : findings,
  needsClarification: contractQuestions,
  nextStep: `Run spec-implement with args {specDir: "${SPEC_DIR}"}.`,
}
