export const meta = {
  name: 'spec-specify',
  description: 'Turn a feature request into a reviewed spec.md with EARS acceptance criteria under specs/<NNN-slug>/',
  whenToUse: 'First stage of spec-driven development. Writes specs/<NNN-slug>/spec.md and returns the blocking questions a human must answer before planning.',
  phases: [
    { title: 'Allocate', detail: 'pick and create the next specs/<NNN-slug> directory' },
    { title: 'Survey', detail: 'multi-modal sweep for what already exists and can be reused' },
    { title: 'Draft', detail: 'write spec.md with numbered EARS acceptance criteria' },
    { title: 'Critique', detail: 'diverse review lenses, each triaged by independent skeptics' },
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
//   drafts spec.md with numbered EARS acceptance criteria, tears the draft apart
//   from seven review angles, and folds back only the criticism that survived an
//   independent skeptic vote.
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
//   Roughly 34 subagents at the default depth, more when the caller sets a larger
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
const MAJORITY = Math.floor(SKEPTICS / 2) + 1
const MAX_PATCH_ROUNDS = DEPTH >= 2 ? 2 : 1

// ---------------------------------------------------------------- shared prompt fragments
// Every subagent starts in a fresh context and knows nothing about this repository,
// so each prompt has to re-orient it. ORIENT is prepended to all of them. It points
// at markdown rather than restating anything: this file is copied byte-for-byte into
// scaffolded projects while only markdown is namespace-rewritten, so a namespace or a
// solution file name written here would arrive wrong and stay wrong.
const ORIENT = [
  'Ground yourself first: read CLAUDE.md at the repo root, then .claude/skills/spec-driven/SKILL.md,',
  'then .claude/skills/coding-conventions/SKILL.md.',
  'CLAUDE.md is the authority on architecture, layout and the exact build and test commands.',
  'Never invent or hardcode a root namespace, project file name or solution file name - read them from the repo.',
].join(' ')

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
          reason: { type: 'string' },
        },
        required: ['id', 'refuted'],
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

function digestSurvey(results) {
  return results.filter(Boolean).map((r) => ({
    lens: r.lens,
    summary: r.summary,
    reusable: trim(r.reusable, 8, `${r.lens} reuse notes`),
    conflicts: trim(r.conflicts, 6, `${r.lens} conflicts`),
    constraints: trim(r.constraints, 8, `${r.lens} constraints`),
    openQuestions: trim(r.openQuestions, 6, `${r.lens} questions`),
  }))
}

// index-driven, never random, so a resumed run replays identically
const STANCES = [
  'You are a pragmatic tech lead who hates busywork. Refute any issue that is stylistic, speculative, or already covered elsewhere in the spec.',
  'You are a QA lead. Refute any issue whose stated problem would not change what a test asserts.',
  'You are the codebase owner. Refute any issue that misreads this repository - open the files it cites before believing it.',
]

// ---------------------------------------------------------------- Allocate
// The script itself cannot touch the filesystem, so a cheap agent reads specs/ and
// mints the next number. Two concurrent spec-specify runs would therefore claim the
// same number - run this stage one at a time, or pass args.specDir explicitly.
// Being the first agent() call also makes it the first cached result on resume.
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
      '5. Return the repo-relative path. Do not draft any spec content.',
    ].join('\n'),
    { label: 'allocate spec dir', schema: ALLOC_SCHEMA, effort: 'low' }
  )
}

if (!alloc || !alloc.created || !alloc.specDir) {
  return { ok: false, error: 'could-not-allocate-spec-dir' }
}
const SPEC_DIR = alloc.specDir
const SPEC_PATH = `${SPEC_DIR}/spec.md`
log(`Spec directory: ${SPEC_DIR}`)

// ---------------------------------------------------------------- Survey
phase('Survey')
const BASE_LENSES = [
  {
    key: 'backend-slices',
    ask: 'Enumerate the vertical slices under src/backend/Source/Features. Decide which one would own this capability, or whether a new slice is required. Read the backend-feature skill and note the feature-isolation rules that would apply.',
  },
  {
    key: 'api-surface',
    ask: 'Search src/backend/Source/Features/*/Endpoints for endpoints that already do part of this, plus their route groups, request and response shapes, validators and list/paging conventions. Read the backend-endpoint skill.',
  },
  {
    key: 'data-model',
    ask: 'Inspect src/backend/Source/Data and the entity base classes and interfaces (audit, soft delete, normalized properties). Say which tables and columns already exist that this request touches, and what would be new. Read the backend-entity skill.',
  },
  {
    key: 'permissions-auth',
    ask: 'Inspect src/backend/Source/Permissions, the per-feature permission definition providers, and src/frontend/web/allow.ts, auth-urls.ts and nav-items.ts. Which permissions already exist, and which would this request need? Read the permissions skill.',
  },
  {
    key: 'frontend-surface',
    ask: 'Inspect the routes under src/frontend/web/app, the API slices under src/frontend/web/store/api and the shared components under src/frontend/web/components. What screens, tables, forms, hooks and components could this request reuse? Read the frontend-crud and rtk-query-api skills.',
  },
  {
    key: 'i18n',
    ask: 'Inspect src/frontend/web/public/locales and src/frontend/web/i18n. List the key namespaces in use and how many locale files must be kept in sync. Read the localization skill.',
  },
  {
    key: 'errors-and-failure',
    ask: 'Inspect the error-code constants under src/backend/Source/ErrorHandling and the web-side error message mapping. Which failure modes does this request introduce, and how are comparable ones surfaced today? Read the api-error-handling skill.',
  },
  {
    key: 'tests-and-fixtures',
    ask: 'Inspect src/backend/Tests (fixtures, seeders, architecture tests) and the vitest tests under src/frontend/web. What test scaffolding already exists that this feature would plug into? Read the backend-tests and frontend-tests skills.',
  },
]

const EXTRA_LENSES = [
  {
    key: 'async-and-storage',
    ask: 'Would this request need background jobs, in-app notifications or file storage? Inspect the existing usages and read the background-jobs, notifications and file-storage skills. If none apply, say so explicitly.',
  },
  {
    key: 'prior-art',
    ask: 'Search the git history (git log --oneline -n 200, and git log -S with keywords from the request) for previous attempts, reverts or related refactors. Report anything that constrains how this should be built.',
  },
]

// Multi-modal sweep: each lens searches a different way and is blind to what the
// others turn up, which is how you find things one search angle would miss.
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
      ].join('\n'),
      { label: `survey: ${l.key}`, phase: 'Survey', schema: SURVEY_SCHEMA }
    )
  )
)

const surveyDigest = digestSurvey(survey)
if (!surveyDigest.length) {
  return { ok: false, error: 'survey-produced-nothing', specDir: SPEC_DIR }
}
log(`${surveyDigest.length}/${LENSES.length} survey lenses returned.`)

// ---------------------------------------------------------------- Draft
// One drafter, not a panel: at this point there is nothing to choose between, only
// facts to write down. The competing-approaches judge panel lives in spec-plan, where
// there genuinely is a design decision to make.
phase('Draft')
const draft = await agent(
  [
    ORIENT,
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
    '  ## Acceptance criteria           the EARS list below, grouped by area',
    '  ## Non-functional                performance, paging limits, audit, soft delete, concurrency',
    '  ## Reuse and existing behaviour  what already exists (cite survey paths) and must not be duplicated',
    '  ## Out of scope                  explicit exclusions',
    '  ## Assumptions                   what you assumed because nobody said',
    '  ## Open questions                questions only a human can answer; an empty list is allowed and preferred',
    '',
    EARS,
    '',
    `Cover these cross-cutting concerns as acceptance criteria wherever they apply: ${CROSS_CUTTING}.`,
    'Every unwanted-behaviour case (validation failure, missing permission, not found, conflict) gets its own "If ..., then the system shall ..." criterion.',
    'Do NOT design the implementation, and do NOT invent requirements the request did not imply - genuine ambiguity goes in Open questions instead.',
  ].join('\n'),
  { label: 'draft spec.md', schema: DRAFT_SCHEMA }
)

if (!draft) {
  return { ok: false, error: 'draft-failed', specDir: SPEC_DIR }
}
log(`Draft written with ${draft.acIds.length} acceptance criteria.`)

// ---------------------------------------------------------------- Critique -> triage
phase('Critique')
const CRITIQUE_LENSES = [
  {
    key: 'ambiguity-testability',
    ask: 'For each criterion ask: could two competent engineers implement this differently and both claim compliance? Could a test fail it deterministically? Flag every weasel word ("appropriate", "fast", "user-friendly", "as needed", "etc").',
  },
  {
    key: 'ears-form',
    ask: 'Check each criterion against the five EARS forms. Flag any that is not in a valid form, bundles two behaviours into one criterion, uses "should" or "may" instead of "shall", or is a design decision disguised as a requirement.',
  },
  {
    key: 'scope-creep',
    ask: 'Compare the spec to the original one-line request. Flag every criterion the request did not ask for and that is not a genuine consequence of it. Bias hard toward cutting.',
  },
  {
    key: 'architecture-conflict',
    ask: 'Read CLAUDE.md and the backend-feature, backend-endpoint and coding-conventions skills. Flag every criterion that cannot be satisfied without breaking a documented rule: feature isolation between slices, one endpoint per file, permission constants mirrored on both sides, locale-prefixed routing, whitelisted sortable fields, shared global usings.',
  },
  {
    key: 'cross-cutting-coverage',
    ask: `Read .claude/skills/spec-driven/SKILL.md and check the completeness checklist there, plus: ${CROSS_CUTTING}. For each concern that applies but has no criterion, raise a missing-criterion issue and write the exact EARS sentence that should be added.`,
  },
  {
    key: 'failure-and-security',
    ask: 'Enumerate the ways this feature can fail or be abused: unauthorized caller, missing record, soft-deleted record, concurrent edit, oversized payload, injection through a search or sort parameter, information leaked in an error message. Flag each one that has no corresponding "If ..., then ..." criterion.',
  },
  {
    key: 'reuse-vs-duplication',
    ask: 'Using the Reuse section and the repository itself, flag any criterion that would force building something the codebase already has, or that contradicts how the existing equivalent behaves.',
  },
]

// Adversarial review. Each lens produces issues, then independent skeptics are asked
// to REFUTE them and an issue dies on a majority refutation. Without this step a
// confident-sounding but wrong criticism gets folded straight into the spec.
//
// pipeline(), not parallel(): there is no cross-lens dependency, so lens A can be
// triaged while lens B is still critiquing. A barrier here would idle every fast lens
// behind the slowest one for a whole stage.
const triaged = await pipeline(
  CRITIQUE_LENSES,
  (l) =>
    agent(
      [
        ORIENT,
        `Read ${SPEC_PATH} in full. The original request was: "${REQUEST}".`,
        `Your review lens is "${l.key}". ${l.ask}`,
        'Report only issues your lens is responsible for - another reviewer owns the other angles.',
        'Every issue needs concrete evidence: quote the offending sentence, or cite the repo path that contradicts it.',
        'Mark severity "blocking" only when the spec cannot be planned against until it is resolved.',
        'Set isQuestionForHuman when the resolution needs a product decision rather than an edit.',
        'Do not edit any file.',
      ].join('\n'),
      { label: `critique: ${l.key}`, phase: 'Critique', schema: CRITIQUE_SCHEMA }
    ),
  (crit, l) => {
    if (!crit || !crit.issues || !crit.issues.length) return { lens: l.key, issues: [] }
    // inner barrier: majority rule needs every vote for this lens
    return parallel(
      Array.from({ length: SKEPTICS }, (unused, k) => () =>
        agent(
          [
            ORIENT,
            STANCES[k % STANCES.length],
            `Read ${SPEC_PATH}. Another reviewer (lens "${l.key}") raised the issues below.`,
            'Your job is to REFUTE them. For each issue return refuted=true unless it is clearly real and clearly worth acting on.',
            'Verify any claim about the repository by opening the files. A claim that misreads this codebase is refuted.',
            'When genuinely uncertain, refute.',
            '',
            JSON.stringify(crit.issues),
          ].join('\n'),
          { label: `triage ${l.key} #${k + 1}`, phase: 'Critique', schema: TRIAGE_SCHEMA }
        )
      )
    ).then((votes) => {
      const good = votes.filter(Boolean)
      const kept = crit.issues.filter((iss) => {
        const against = good.filter((v) => (v.verdicts || []).some((x) => x.id === iss.id && x.refuted)).length
        return against < MAJORITY
      })
      if (good.length < SKEPTICS) log(`Lens ${l.key}: only ${good.length}/${SKEPTICS} skeptics returned; judged on the votes received.`)
      log(`Lens ${l.key}: ${kept.length}/${crit.issues.length} issues survived triage.`)
      return { lens: l.key, issues: kept.map((iss) => ({ ...iss, id: `${l.key}:${iss.id}` })) }
    })
  }
)

const surviving = triaged.filter(Boolean).flatMap((t) => t.issues)
log(`${surviving.length} issues survived adversarial triage across ${CRITIQUE_LENSES.length} lenses.`)

// ---------------------------------------------------------------- Revise
// A single writer folds the surviving issues in. It is deliberately allowed to decline
// an issue (recorded under "rejected") but never to silently drop one, and it may not
// guess at product intent - that becomes a blocking question instead.
phase('Revise')
let revised = await agent(
  [
    ORIENT,
    `Revise ${SPEC_PATH} in place. The original request was: "${REQUEST}".`,
    '',
    'Surviving review issues, each already past independent skeptics:',
    JSON.stringify(trim(surviving, 60, 'review issues')),
    '',
    'Rules:',
    '- Apply every issue you agree with. List each one you decline under "rejected" with a one-line reason.',
    '- NEVER renumber an existing AC id. Amend text in place, or append new ids at the end of the relevant section.',
    '- Keep every criterion in a valid EARS form.',
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
      `Read ${SPEC_PATH} and the original request: "${REQUEST}".`,
      'You are the completeness critic. Ask only one question: what is MISSING?',
      'Consider a user journey with no criteria; a lifecycle stage never mentioned (create, read, update, delete, restore, export);',
      `a cross-cutting concern skipped (${CROSS_CUTTING}); an error path with no unwanted-behaviour criterion;`,
      'an interaction with an existing feature the survey found but the spec never mentions; a non-functional limit never stated.',
      'Do not restate issues already handled, and do not propose implementation. Return an empty gaps array if the spec is complete.',
      'Do not edit any file.',
    ].join('\n'),
    {
      label: `completeness critic r${round + 1}`,
      phase: 'Completeness',
      schema: GAP_SCHEMA,
      effort: DEPTH >= 2 ? 'high' : undefined,
    }
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
      `Patch ${SPEC_PATH} to close the gaps below. Append new AC ids; never renumber an existing one. Keep EARS form.`,
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
