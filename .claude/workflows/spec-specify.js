export const meta = {
  name: 'spec-specify',
  description: 'Turn a feature request into a reviewed spec.md with EARS acceptance criteria under specs/<NNN-slug>/',
  whenToUse: 'First stage of spec-driven development. Writes specs/<NNN-slug>/spec.md and returns the blocking questions a human must answer before planning.',
  phases: [
    { title: 'Allocate', detail: 'pick and create the next specs/<NNN-slug> directory' },
    { title: 'Survey', detail: 'multi-modal sweep for what already exists and can be reused' },
    { title: 'Draft', detail: 'write spec.md with numbered EARS acceptance criteria' },
    { title: 'Critique', detail: 'diverse review lenses, then two independent skeptics triage every issue' },
    { title: 'Revise', detail: 'fold surviving issues in and collect the blocking questions' },
    { title: 'Completeness', detail: 'a critic asks what is still missing, then patches' },
  ],
}

// =============================================================================
// STAGE 1 OF 4 - the feature request becomes a specification.
//
//   /specify -> spec-specify -> /plan -> /implement -> /verify
//
// What it does
//   Allocates specs/<NNN-slug>/, sweeps the codebase from several angles at once,
//   drafts spec.md with numbered EARS acceptance criteria, tears the draft apart from
//   five review angles, and folds back only the criticism that survived independent
//   skeptics.
//
// args
//   {request: string}   the one-line feature request. A bare string also works.
//   {slug?: string}     override the derived kebab-case directory suffix.
//   {specDir?: string}  re-enter an existing spec directory instead of allocating
//                       a new one - use this to re-run over a spec you have edited.
//   {specsRoot?: string} default "specs".
//
// Returns
//   {ok, specDir, specPath, acIds, blockingQuestions, assumptions, residualGaps}
//   blockingQuestions is the payload that matters: a workflow cannot ask the user
//   anything while it runs, so questions only a human can answer come back here for
//   the slash command to raise. spec-plan refuses to run until they are resolved.
//
// Files written
//   The spec directory (by the allocator) and spec.md (by the drafter, then the
//   reviser, then the patcher). Never two writers at once - there is no lock, and
//   the orchestrator has no filesystem access with which to build one.
//
// Cost
//   Roughly 17 subagents at the default depth, more when the caller sets a larger
//   token budget for the turn.
// =============================================================================

// ---------------------------------------------------------------- arguments
const REQUEST = typeof args === 'string' ? args : ((args && args.request) || '')
const SPECS_ROOT = (args && args.specsRoot) || 'specs'
const SLUG_HINT = (args && args.slug) || ''
const REENTER = (args && args.specDir) || ''

if (!REQUEST && !REENTER) {
  log('No feature request supplied. Pass a string, or {request: "..."}.')
  return { ok: false, error: 'missing-request' }
}

// Depth scales the run to the token budget the user set for this turn (the "+500k"
// style directive). budget.total is null when no target was set, in which case we
// stay at depth 1. Deriving every loop bound from budget.total and args - never from
// a clock or a random number - is what lets an interrupted run resume and replay
// identically.
const DEPTH = budget.total ? Math.max(1, Math.min(3, Math.floor(budget.total / 250000))) : 1
const SKEPTICS = DEPTH >= 2 ? 3 : 2
const MAX_PATCH_ROUNDS = DEPTH >= 2 ? 2 : 1

// ---------------------------------------------------------------- shared prompt fragments
// Every subagent starts in a fresh context, but it does NOT start blind: the workflow
// runtime injects CLAUDE.md into every subagent automatically. Asking an agent to read
// it would buy a duplicate read of the largest document in the repository, once per
// agent, so this block does not mention it.
//
// The namespace line is here because it is not an instruction CLAUDE.md carries. It is
// a guard specific to this template: this file is copied byte-for-byte into scaffolded
// projects while only markdown is namespace-rewritten, so a namespace or a solution
// file name written here - or guessed by an agent - would arrive wrong and stay wrong.
const ORIENT = 'Never invent or hardcode a root namespace, project file name or solution file name - read them from the repo.'

// Added only for the agents whose job is to judge whether a convention was followed.
// The test is what the agent judges, not whether it writes anything: the
// architecture-conflict lens edits nothing and needs this more than the drafter does.
const CONVENTIONS = 'Read .claude/skills/coding-conventions/SKILL.md and the relevant guide under .claude/skills before judging any convention question.'

// Added only for the agents that must reproduce a document format or work from the
// completeness checklist. The EARS grammar, the cross-cutting list and the document
// structure are inlined below for everyone else, which is cheaper than a 10 KB read.
const PROCESS = 'Read .claude/skills/spec-driven/SKILL.md for the spec document template and the completeness checklist.'

const EARS = [
  'Every acceptance criterion MUST be one testable behaviour written in one of the five EARS forms:',
  '  Ubiquitous:       "The system shall <response>."',
  '  Event-driven:     "When <trigger>, the system shall <response>."',
  '  State-driven:     "While <state>, the system shall <response>."',
  '  Optional-feature: "Where <feature is included>, the system shall <response>."',
  '  Unwanted:         "If <condition>, then the system shall <response>."',
  'Give each criterion a stable id on its own line: "- **AC-001** When ...".',
  'Ids are permanent - never renumber an existing criterion, only append new ones.',
  'Use "shall", never "should" or "may".',
  'No class names, file paths or library names inside a criterion - that is the plan stage\'s job.',
].join('\n')

const CROSS_CUTTING = [
  'authorization (a new capability almost always needs a permission constant mirrored on the web side)',
  'localization (no user-visible string may be hard-coded; every message needs a translation key in every shipped locale)',
  'soft delete and audit columns for anything persisted',
  'a defined error code and the message the user actually sees when the operation fails',
  'paging, sorting and search for anything that returns a list',
  'test coverage: an integration test per endpoint behaviour including its failure branch',
].join('; ')

// ---------------------------------------------------------------- schemas
// Passing a JSON Schema to agent() forces the subagent through a StructuredOutput
// tool call and returns the validated object, so the model retries on a mismatch and
// this script reads fields instead of parsing prose. Every schema root must be an
// object, and "required" must be a subset of "properties" or agent() throws.
const ALLOC_SCHEMA = {
  type: 'object',
  properties: {
    specDir: { type: 'string', description: 'repo-relative path, e.g. specs/007-user-csv-export' },
    slug: { type: 'string' },
    existing: { type: 'array', items: { type: 'string' } },
    created: { type: 'boolean' },
  },
  required: ['specDir', 'slug', 'created'],
}

const SURVEY_SCHEMA = {
  type: 'object',
  properties: {
    lens: { type: 'string' },
    summary: { type: 'string', description: '3-6 sentences, concrete and path-anchored' },
    reusable: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          path: { type: 'string' },
          what: { type: 'string' },
          howToReuse: { type: 'string' },
        },
        required: ['path', 'what'],
      },
    },
    conflicts: { type: 'array', items: { type: 'string' } },
    constraints: { type: 'array', items: { type: 'string' }, description: 'rules the spec must respect, each citing where it is written down' },
    openQuestions: { type: 'array', items: { type: 'string' } },
  },
  required: ['lens', 'summary', 'reusable', 'constraints', 'openQuestions'],
}

const DRAFT_SCHEMA = {
  type: 'object',
  properties: {
    specPath: { type: 'string' },
    acIds: { type: 'array', items: { type: 'string' } },
    assumptions: { type: 'array', items: { type: 'string' } },
    openQuestions: { type: 'array', items: { type: 'string' } },
    outOfScope: { type: 'array', items: { type: 'string' } },
  },
  required: ['specPath', 'acIds'],
}

const CRITIQUE_SCHEMA = {
  type: 'object',
  properties: {
    lens: { type: 'string' },
    issues: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string', description: 'short id, unique within this critique' },
          acId: { type: 'string', description: 'AC-nnn this concerns, or empty for whole-spec issues' },
          severity: { type: 'string', enum: ['blocking', 'major', 'minor'] },
          problem: { type: 'string' },
          evidence: { type: 'string', description: 'a quote from spec.md, or a repo path that proves it' },
          suggestedFix: { type: 'string' },
          isQuestionForHuman: { type: 'boolean' },
        },
        required: ['id', 'severity', 'problem', 'suggestedFix'],
      },
    },
  },
  required: ['lens', 'issues'],
}

// "reason" is required, unlike the earlier version of this schema. A skeptic now
// judging forty issues in one prompt could otherwise refute the lot at near-zero cost,
// and because a kill needs every skeptic to agree, two lazy skeptics would produce a
// silently empty Revise stage that reads exactly like "the draft was clean".
// duplicateOf exists because a whole-list skeptic can see what a per-lens one could
// not: the same issue raised by three different lenses.
const TRIAGE_SCHEMA = {
  type: 'object',
  properties: {
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          refuted: { type: 'boolean' },
          reason: { type: 'string', description: 'when refuting, quote the spec sentence or cite the repo path that makes the issue wrong' },
          duplicateOf: { type: 'string', description: 'the id of an earlier issue in this list that says the same thing' },
        },
        required: ['id', 'refuted', 'reason'],
      },
    },
  },
  required: ['verdicts'],
}

const REVISE_SCHEMA = {
  type: 'object',
  properties: {
    specPath: { type: 'string' },
    acIds: { type: 'array', items: { type: 'string' } },
    applied: { type: 'array', items: { type: 'string' } },
    rejected: { type: 'array', items: { type: 'string' } },
    assumptions: { type: 'array', items: { type: 'string' } },
    blockingQuestions: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          question: { type: 'string' },
          whyBlocking: { type: 'string' },
          defaultAssumption: { type: 'string' },
        },
        required: ['question', 'whyBlocking'],
      },
    },
  },
  required: ['specPath', 'acIds', 'blockingQuestions'],
}

const GAP_SCHEMA = {
  type: 'object',
  properties: {
    gaps: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          area: { type: 'string' },
          gap: { type: 'string' },
          severity: { type: 'string', enum: ['blocking', 'major', 'minor'] },
          fix: { type: 'string' },
        },
        required: ['area', 'gap', 'severity'],
      },
    },
    verdict: { type: 'string' },
  },
  required: ['gaps', 'verdict'],
}

// ---------------------------------------------------------------- helpers
function trim(list, n, what) {
  const arr = Array.isArray(list) ? list : []
  if (arr.length > n) log(`Dropped ${arr.length - n} of ${arr.length} ${what} from the digest (kept ${n}).`)
  return arr.slice(0, n)
}

// These caps are per lens, and they are sized to the lens. Merging two subsystems into
// one lens - as the backend lens below does - and leaving the cap where it was would
// halve what actually reaches the drafter, so a merged lens carries a larger cap. The
// caps travel with the lens definition for exactly that reason.
function digestSurvey(results, lenses) {
  // Map BEFORE filtering. parallel() returns null in place for an agent that died, so
  // the array is positionally aligned with the lens list until something is removed
  // from it - filtering first would pair each surviving lens with the wrong cap.
  return results.map((r, i) => {
    if (!r) return null
    const cap = (lenses[i] && lenses[i].cap) || 8
    return {
      lens: r.lens,
      summary: r.summary,
      reusable: trim(r.reusable, cap * 2, `${r.lens} reuse notes`),
      conflicts: trim(r.conflicts, cap, `${r.lens} conflicts`),
      constraints: trim(r.constraints, cap * 2, `${r.lens} constraints`),
      openQuestions: trim(r.openQuestions, cap, `${r.lens} questions`),
    }
  }).filter(Boolean)
}

// Index-driven, never random, so a resumed run replays identically.
//
// Note the order. With two skeptics the previous version indexed STANCES[k % 3] and
// therefore never used the third stance at all - which was the "open the files it
// cites" stance, the one that actually kills criticism that misread the repository.
// The two stances that matter most are now first.
const STANCES = [
  'You are the codebase owner. Refute any issue that misreads this repository - open the files it cites before believing it.',
  'You are a pragmatic tech lead who hates busywork. Refute any issue that is stylistic, speculative, or already covered elsewhere in the spec.',
  'You are a QA lead. Refute any issue whose stated problem would not change what a test asserts.',
]

// ---------------------------------------------------------------- Allocate
// The script itself cannot touch the filesystem, so a cheap agent reads specs/ and
// mints the next number. Two concurrent spec-specify runs would therefore claim the
// same number - run this stage one at a time, or pass args.specDir explicitly.
// Being the first agent() call also makes it the first cached result on resume.
//
// This is the one agent in the loop that is a pure mechanical parse with a
// self-evident answer, so it is the one that gets a small model. Its output is
// shape-checked below rather than trusted.
phase('Allocate')
let alloc
if (REENTER) {
  alloc = { specDir: REENTER, slug: '', created: true }
  log(`Re-entering the existing spec directory ${REENTER}.`)
} else {
  alloc = await agent(
    [
      `Allocate a spec directory for this feature request: "${REQUEST}".`,
      `1. List "${SPECS_ROOT}/" at the repo root, creating it if it does not exist.`,
      '2. Find the highest existing NNN- prefix among its subdirectories. The new number is that plus one, zero padded to three digits. If there are none, use 001.',
      SLUG_HINT
        ? `3. Use the slug "${SLUG_HINT}".`
        : '3. Derive a two-to-four word kebab-case slug from the request. No articles, no verbs like "add".',
      `4. Create the empty directory "${SPECS_ROOT}/<NNN>-<slug>". Write nothing inside it.`,
      '5. Return the repo-relative path, and list the directories that already existed.',
    ].join('\n'),
    { label: 'allocate spec dir', schema: ALLOC_SCHEMA, effort: 'low', model: 'haiku' }
  )
}

if (!alloc || !alloc.created || !alloc.specDir) {
  return { ok: false, error: 'could-not-allocate-spec-dir' }
}
// Shape-check rather than trust. A small model handled this, and a malformed path here
// would scatter documents outside specs/ for the remaining three stages.
if (!REENTER && !/^[a-z]+\/\d{3}-[a-z0-9-]+$/.test(alloc.specDir)) {
  log(`The allocator returned "${alloc.specDir}", which is not a <root>/NNN-slug path. Stopping rather than writing documents to an unexpected location.`)
  return { ok: false, error: 'bad-spec-dir', specDir: alloc.specDir }
}
if (!REENTER && (alloc.existing || []).some((e) => String(e).replace(/^.*\//, '') === alloc.specDir.replace(/^.*\//, ''))) {
  log(`The allocator claims to have created ${alloc.specDir}, but it also lists that directory as pre-existing. Re-run with an explicit args.specDir.`)
  return { ok: false, error: 'spec-dir-collision', specDir: alloc.specDir }
}
const SPEC_DIR = alloc.specDir
const SPEC_PATH = `${SPEC_DIR}/spec.md`
log(`Spec directory: ${SPEC_DIR}`)

// ---------------------------------------------------------------- Survey
// Multi-modal sweep: each lens searches a different way and is blind to what the
// others turn up, which is how you find things one search angle would miss.
//
// Five lenses rather than eight. The three backend lenses were reading the same
// Features tree three times over and the tests lens was reading it a fourth; merging
// them costs one agent's worth of separation and saves three agents' worth of
// duplicated exploration. The lenses that stayed separate are the cross-stack ones,
// where a detail missed on one side of the stack is expensive: permissions must be
// mirrored, locales must be kept in sync, and the web surface is a different tree
// entirely.
phase('Survey')
const BASE_LENSES = [
  {
    key: 'backend-surface',
    cap: 14,
    ask: [
      'Cover the whole API side in one sweep.',
      'Enumerate the vertical slices under src/backend/Source/Features and decide which one would own this capability, or whether a new slice is required.',
      'Search the Endpoints folders for endpoints that already do part of this, plus their route groups, request and response shapes, validators and list/paging conventions.',
      'Inspect src/backend/Source/Data and the entity base classes and interfaces (audit, soft delete, normalized properties): say which tables and columns already exist that this request touches, and what would be new.',
      'Read the backend-feature, backend-endpoint and backend-entity skills and note the feature-isolation rules that would apply.',
    ].join(' '),
  },
  {
    key: 'permissions-auth',
    cap: 8,
    ask: 'Inspect src/backend/Source/Permissions, the per-feature permission definition providers, and src/frontend/web/allow.ts, auth-urls.ts and nav-items.ts. Which permissions already exist, and which would this request need? Read the permissions skill.',
  },
  {
    key: 'frontend-surface',
    cap: 14,
    ask: [
      'Cover the whole web side in one sweep.',
      'Inspect the routes under src/frontend/web/app, the API slices under src/frontend/web/store/api and the shared components under src/frontend/web/components:',
      'what screens, tables, forms, hooks and components could this request reuse?',
      'Then inspect src/frontend/web/public/locales and src/frontend/web/i18n: list the key namespaces in use and how many locale files must be kept in sync.',
      'Read the frontend-crud, rtk-query-api and localization skills.',
    ].join(' '),
  },
  {
    key: 'failure-and-tests',
    cap: 12,
    ask: [
      'Cover how this codebase fails and how it proves it works.',
      'Inspect the error-code constants under src/backend/Source/ErrorHandling and the web-side error message mapping:',
      'which failure modes does this request introduce, and how are comparable ones surfaced today?',
      'Then inspect src/backend/Tests (fixtures, seeders, architecture tests) and the vitest tests under src/frontend/web:',
      'what test scaffolding already exists that this feature would plug into?',
      'Read the api-error-handling, backend-tests and frontend-tests skills.',
    ].join(' '),
  },
  {
    key: 'async-and-storage',
    cap: 8,
    ask: 'Would this request need background jobs, in-app notifications or file storage? Inspect the existing usages and read the background-jobs, notifications and file-storage skills. If none apply, say so explicitly and briefly.',
  },
]

// prior-art is the only lens whose findings cannot be recreated from the others, so it
// is the one kept behind the depth gate rather than merged away.
const EXTRA_LENSES = [
  {
    key: 'prior-art',
    cap: 8,
    ask: 'Search the git history (git log --oneline -n 200, and git log -S with keywords from the request) for previous attempts, reverts or related refactors. Report anything that constrains how this should be built.',
  },
]

const LENSES = DEPTH >= 2 ? BASE_LENSES.concat(EXTRA_LENSES) : BASE_LENSES
log(`Surveying with ${LENSES.length} lenses at depth ${DEPTH}.`)

// Barrier: the single drafter consumes every lens at once, and the reuse decisions
// are cross-lens - "the web app already has a table export helper" only matters
// together with "the list endpoint already exists".
const survey = await parallel(
  LENSES.map((l) => () =>
    agent(
      [
        ORIENT,
        `Feature request under consideration: "${REQUEST}".`,
        `Your lens is "${l.key}". ${l.ask}`,
        'You are NOT designing a solution and you are NOT writing files. Report what exists.',
        'Be concrete: cite real repo-relative paths and real type or file names you actually opened.',
        'Under constraints, list rules this spec must respect and cite where each is written down (a CLAUDE.md section or a skill name).',
        'Under conflicts, list ways this request would collide with existing behaviour or a documented rule.',
        'This lens covers more than one subsystem, so be thorough across all of them rather than deep in the first.',
      ].join('\n'),
      { label: `survey: ${l.key}`, phase: 'Survey', schema: SURVEY_SCHEMA }
    )
  )
)

const surveyDigest = digestSurvey(survey, LENSES)
if (!surveyDigest.length) {
  return { ok: false, error: 'survey-produced-nothing', specDir: SPEC_DIR }
}
log(`${surveyDigest.length}/${LENSES.length} survey lenses returned.`)

// ---------------------------------------------------------------- Draft
// One drafter, not a panel: at this point there is nothing to choose between, only
// facts to write down. The competing-approaches judge panel lives in spec-plan, where
// there genuinely is a design decision to make.
//
// High effort: everything downstream is a reaction to this document, so a weak draft
// costs three stages of correction.
phase('Draft')
const draft = await agent(
  [
    ORIENT,
    PROCESS,
    `Write the specification for: "${REQUEST}".`,
    `Write it to ${SPEC_PATH}. Create the file.`,
    '',
    'Codebase survey produced by parallel readers - treat it as fact about this repository:',
    JSON.stringify(surveyDigest),
    '',
    'Required structure of spec.md:',
    '  # <Feature title>',
    '  ## Summary                       three to five sentences: the user-visible outcome',
    '  ## Actors and permissions        who does this, and what authorization is implied (behaviour, not constant names)',
    '  ## User scenarios                numbered narrative scenarios in Given/When/Then form',
    '  ## Acceptance criteria           the EARS list below, grouped by area under "###" subheadings',
    '  ## Non-functional                performance, paging limits, audit, soft delete, concurrency',
    '  ## Reuse and existing behaviour  what already exists (cite survey paths) and must not be duplicated',
    '  ## Out of scope                  explicit exclusions',
    '  ## Assumptions                   what you assumed because nobody said',
    '  ## Open questions                questions only a human can answer; an empty list is allowed and preferred',
    '',
    'Group the acceptance criteria under "###" subheadings by area, and keep criteria about the same area together.',
    'A later stage verifies them in batches formed from that grouping, so the grouping is load-bearing, not decoration.',
    '',
    EARS,
    '',
    `Cover these cross-cutting concerns as acceptance criteria wherever they apply: ${CROSS_CUTTING}.`,
    'Every unwanted-behaviour case (validation failure, missing permission, not found, conflict) gets its own "If ..., then the system shall ..." criterion.',
    'Do NOT design the implementation, and do NOT invent requirements the request did not imply - genuine ambiguity goes in Open questions instead.',
  ].join('\n'),
  { label: 'draft spec.md', schema: DRAFT_SCHEMA, effort: 'high' }
)

if (!draft) {
  return { ok: false, error: 'draft-failed', specDir: SPEC_DIR }
}
log(`Draft written with ${draft.acIds.length} acceptance criteria.`)

// ---------------------------------------------------------------- Critique -> triage
// Five lenses rather than seven. The two merges are between lenses that were reading
// the same sentences for the same kind of defect: grammar and testability are one
// question, and "does this fit the repository" covers both an architecture conflict and
// a thing the codebase already has. The three that stayed separate - scope creep,
// cross-cutting coverage, failure and security - are the ones that find things no other
// lens looks for at all.
phase('Critique')
const CRITIQUE_LENSES = [
  {
    key: 'form-and-testability',
    conventions: false,
    ask: [
      'Judge whether each criterion is well formed and could be tested.',
      'Check each against the five EARS forms: flag any that is not in a valid form, bundles two behaviours into one criterion,',
      'uses "should" or "may" instead of "shall", or is a design decision disguised as a requirement.',
      'Then ask of each: could two competent engineers implement this differently and both claim compliance? Could a test fail it deterministically?',
      'Flag every weasel word ("appropriate", "fast", "user-friendly", "as needed", "etc").',
    ].join(' '),
  },
  {
    key: 'scope-creep',
    conventions: false,
    ask: 'Compare the spec to the original one-line request. Flag every criterion the request did not ask for and that is not a genuine consequence of it. Bias hard toward cutting.',
  },
  {
    key: 'repository-fit',
    conventions: true,
    ask: [
      'Judge the spec against the repository as it actually is.',
      'Flag every criterion that cannot be satisfied without breaking a documented rule: feature isolation between slices, one endpoint per file,',
      'permission constants mirrored on both sides, locale-prefixed routing, whitelisted sortable fields, shared global usings.',
      'Then, using the Reuse section and the repository itself, flag any criterion that would force building something the codebase already has,',
      'or that contradicts how the existing equivalent behaves.',
    ].join(' '),
  },
  {
    key: 'cross-cutting-coverage',
    conventions: false,
    process: true,
    ask: `Check the completeness checklist in the spec-driven guide, plus: ${CROSS_CUTTING}. For each concern that applies but has no criterion, raise a missing-criterion issue and write the exact EARS sentence that should be added.`,
  },
  {
    key: 'failure-and-security',
    conventions: false,
    ask: 'Enumerate the ways this feature can fail or be abused: unauthorized caller, missing record, soft-deleted record, concurrent edit, oversized payload, injection through a search or sort parameter, information leaked in an error message. Flag each one that has no corresponding "If ..., then ..." criterion.',
  },
]

// Barrier, and a deliberate one. The skeptics below judge the MERGED issue list, which
// cannot be assembled until every lens has reported. It buys two things a per-lens
// triage could not have: the skeptics can see that three lenses raised the same issue,
// and the fan-out drops from fourteen agents to two.
const critiques = await parallel(
  CRITIQUE_LENSES.map((l) => () =>
    agent(
      [
        ORIENT,
        l.conventions ? CONVENTIONS : '',
        l.process ? PROCESS : '',
        `Read ${SPEC_PATH} in full. The original request was: "${REQUEST}".`,
        `Your review lens is "${l.key}". ${l.ask}`,
        'Report only issues your lens is responsible for - another reviewer owns the other angles.',
        'Every issue needs concrete evidence: quote the offending sentence, or cite the repo path that contradicts it.',
        'Mark severity "blocking" only when the spec cannot be planned against until it is resolved.',
        'Set isQuestionForHuman when the resolution needs a product decision rather than an edit.',
        'Do not edit any file.',
      ]
        .filter(Boolean)
        .join('\n'),
      { label: `critique: ${l.key}`, phase: 'Critique', schema: CRITIQUE_SCHEMA }
    )
  )
)

// Namespace the ids here, in the orchestrator, rather than after triage the way this
// used to work. The skeptics now see every lens's issues in one list, so two lenses
// that both numbered their first issue "1" would be indistinguishable to them and the
// votes would land on the wrong issue.
// flatMap over the unfiltered array, for the same reason digestSurvey maps before it
// filters: a null from a dead agent holds its position, so the index still names the
// lens that produced the entry.
const allIssues = critiques.flatMap((c, i) => {
  if (!c) return []
  const key = (CRITIQUE_LENSES[i] && CRITIQUE_LENSES[i].key) || c.lens || `lens-${i + 1}`
  return (c.issues || []).map((iss) => ({ ...iss, id: `${key}:${iss.id}`, lens: key }))
})
log(`${CRITIQUE_LENSES.length} critique lenses raised ${allIssues.length} issue(s); triaging with ${SKEPTICS} skeptic(s).`)

let surviving = allIssues
if (allIssues.length) {
  const votes = await parallel(
    Array.from({ length: SKEPTICS }, (unused, k) => () =>
      agent(
        [
          ORIENT,
          CONVENTIONS,
          STANCES[k % STANCES.length],
          `Read ${SPEC_PATH}. Reviewers working from ${CRITIQUE_LENSES.length} different lenses raised the issues below.`,
          'Your job is to REFUTE them. For each issue return refuted=true unless it is clearly real and clearly worth acting on.',
          'Verify any claim about the repository by opening the files. A claim that misreads this codebase is refuted.',
          'When genuinely uncertain, refute.',
          'Every verdict needs a reason. When you refute, quote the spec sentence or cite the repo path that makes the issue wrong -',
          'a bare assertion that an issue is not worth acting on is not a reason.',
          'Because the lenses overlap, the same problem may appear more than once. When it does, set duplicateOf to the id of the first one.',
          'Return a verdict for every issue in the list.',
          '',
          JSON.stringify(allIssues),
        ].join('\n'),
        { label: `triage #${k + 1}`, phase: 'Critique', schema: TRIAGE_SCHEMA, effort: 'medium' }
      )
    )
  )

  const good = votes.filter(Boolean)
  if (good.length < SKEPTICS) log(`Only ${good.length}/${SKEPTICS} skeptics returned; judging on the votes received.`)

  // A skeptic that refutes nearly everything has stopped reading and started rubber
  // stamping, and because a kill requires every skeptic to agree, two of those would
  // empty the Revise stage while looking exactly like a clean draft. The rate is logged
  // so that failure is visible in the run rather than inferred from a suspiciously
  // short revision.
  good.forEach((v, k) => {
    const verdicts = v.verdicts || []
    const refutedCount = verdicts.filter((x) => x.refuted).length
    const pct = verdicts.length ? Math.round((refutedCount / verdicts.length) * 100) : 0
    log(`Skeptic #${k + 1} refuted ${refutedCount}/${verdicts.length} issues (${pct}%).`)
    if (pct >= 85 && verdicts.length >= 10) log(`  Skeptic #${k + 1}'s refutation rate is high enough to be worth a look before trusting this triage.`)
  })

  // Unanimity kills an issue: every skeptic that returned must have refuted it. This is
  // the same bar the per-lens version used (two of two), deliberately kept conservative
  // because the cost of keeping a weak issue is one line in the revision, and the cost
  // of dropping a real one is a defect that reaches the plan.
  const threshold = Math.max(1, good.length)
  surviving = allIssues.filter((iss) => {
    const against = good.filter((v) => (v.verdicts || []).some((x) => x.id === iss.id && x.refuted)).length
    return against < threshold
  })

  // Deduplicate on the skeptics' own duplicateOf pointers, so the reviser is not handed
  // the same problem three times over from three lenses.
  const dupes = new Set()
  good.forEach((v) =>
    (v.verdicts || []).forEach((x) => {
      if (x.duplicateOf && x.duplicateOf !== x.id) dupes.add(x.id)
    })
  )
  const beforeDedupe = surviving.length
  surviving = surviving.filter((iss) => !dupes.has(iss.id))
  if (beforeDedupe !== surviving.length) log(`Merged ${beforeDedupe - surviving.length} duplicate issue(s) raised by more than one lens.`)
}

log(`${surviving.length} of ${allIssues.length} issues survived adversarial triage.`)

// ---------------------------------------------------------------- Revise
// A single writer folds the surviving issues in. It is deliberately allowed to decline
// an issue (recorded under "rejected") but never to silently drop one, and it may not
// guess at product intent - that becomes a blocking question instead.
phase('Revise')
let revised = await agent(
  [
    ORIENT,
    PROCESS,
    `Revise ${SPEC_PATH} in place. The original request was: "${REQUEST}".`,
    '',
    'Surviving review issues, each already past independent skeptics:',
    JSON.stringify(trim(surviving, 80, 'review issues')),
    '',
    'Rules:',
    '- Apply every issue you agree with. List each one you decline under "rejected" with a one-line reason.',
    '- NEVER renumber an existing AC id. Amend text in place, or append new ids at the end of the relevant section.',
    '- Keep every criterion in a valid EARS form, and keep the "###" area grouping intact.',
    '- Anything flagged isQuestionForHuman, and anything you could only resolve by guessing product intent, goes into the "## Open questions" section AND into the returned blockingQuestions, each with the default assumption you would use if nobody answers.',
    '- Record every assumption you made under "## Assumptions".',
    '- The spec must stay implementation-free.',
  ].join('\n'),
  { label: 'revise spec.md', schema: REVISE_SCHEMA }
)

if (!revised) {
  log('The revision agent failed; keeping the original draft.')
  revised = {
    specPath: SPEC_PATH,
    acIds: draft.acIds,
    blockingQuestions: [],
    assumptions: draft.assumptions || [],
    applied: [],
    rejected: [],
  }
}

// ---------------------------------------------------------------- Completeness
// Loop-until-dry. Critics are good at judging what is written and bad at noticing what
// was never written at all, so a dedicated critic asks only "what is missing?" and its
// findings become the next round of work. The loop stops when a round finds nothing
// material, when the round cap is reached, or when the token budget runs low - and it
// says which, because a silent stop reads like "nothing was missing".
phase('Completeness')
const residual = []
let round = 0
while (round < MAX_PATCH_ROUNDS) {
  if (budget.total && budget.remaining() < 60000) {
    log(`Stopping the completeness loop after ${round} round(s): ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
    break
  }
  const gaps = await agent(
    [
      ORIENT,
      PROCESS,
      `Read ${SPEC_PATH} and the original request: "${REQUEST}".`,
      'You are the completeness critic. Ask only one question: what is MISSING?',
      'Consider a user journey with no criteria; a lifecycle stage never mentioned (create, read, update, delete, restore, export);',
      `a cross-cutting concern skipped (${CROSS_CUTTING}); an error path with no unwanted-behaviour criterion;`,
      'an interaction with an existing feature the survey found but the spec never mentions; a non-functional limit never stated.',
      'Do not restate issues already handled, and do not propose implementation. Return an empty gaps array if the spec is complete.',
      'Do not edit any file.',
    ].join('\n'),
    { label: `completeness critic r${round + 1}`, phase: 'Completeness', schema: GAP_SCHEMA, effort: 'high' }
  )
  const material = ((gaps && gaps.gaps) || []).filter((g) => g.severity !== 'minor')
  if (!material.length) {
    log(`Completeness round ${round + 1}: no material gaps.`)
    break
  }
  log(`Completeness round ${round + 1}: ${material.length} material gap(s); patching.`)
  const patched = await agent(
    [
      ORIENT,
      `Patch ${SPEC_PATH} to close the gaps below. Append new AC ids under the right "###" area heading; never renumber an existing one. Keep EARS form.`,
      'Anything needing a product decision goes into "## Open questions" and into blockingQuestions rather than being guessed.',
      JSON.stringify(material),
    ].join('\n'),
    { label: `patch spec r${round + 1}`, phase: 'Completeness', schema: REVISE_SCHEMA }
  )
  if (patched) revised = patched
  else material.forEach((g) => residual.push(`${g.area}: ${g.gap}`))
  round++
}

// The caller (the /specify command) reads blockingQuestions and puts them to the user
// with AskUserQuestion, then edits spec.md with the answers. residualGaps lists gaps a
// failed patch agent left behind, so nothing disappears quietly.
return {
  ok: true,
  specDir: SPEC_DIR,
  specPath: SPEC_PATH,
  acIds: revised.acIds || [],
  blockingQuestions: revised.blockingQuestions || [],
  assumptions: revised.assumptions || [],
  appliedIssues: (revised.applied || []).length,
  rejectedIssues: revised.rejected || [],
  residualGaps: residual,
  nextStep: (revised.blockingQuestions || []).length
    ? 'Answer the blocking questions with the human, edit spec.md, then run spec-plan.'
    : `Run spec-plan with args {specDir: "${SPEC_DIR}"}.`,
}
