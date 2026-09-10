export const meta = {
  name: 'spec-implement',
  description: 'Execute specs/<NNN-slug>/tasks.md in file-disjoint waves, with an independent review per task and build and test gates',
  whenToUse: 'Third stage of spec-driven development. Writes application code, one agent per task, scheduled so no two concurrent agents touch the same file.',
  phases: [
    { title: 'Load tasks', detail: 'parse tasks.md into a dependency graph with declared file sets' },
    { title: 'Implement', detail: 'wave by wave: implement, then an independent reviewer that repairs what it finds' },
    { title: 'Gate', detail: 'build and test between waves, repair failures, full gate at the end' },
    { title: 'Report', detail: 'write implementation.md and reconcile the checkboxes' },
  ],
}

// =============================================================================
// STAGE 3 OF 4 - the task list becomes code.
//
//   /specify -> /plan -> /implement -> spec-implement -> /verify
//
// What it does
//   Reads tasks.md, groups the open tasks into waves whose declared file sets do not
//   intersect, and runs each wave concurrently in the real working tree. Every task
//   gets an independent reviewer that did not write the code and that repairs what it
//   finds. Builds and tests gate the waves.
//
// Why the reviewer repairs rather than handing off to a third agent
//   The chain used to be implement -> review -> repair, three agents per task. The
//   repair agent re-read the same guide and the same files the reviewer had just read,
//   to act on a list the reviewer had just written - and nothing re-reviewed its output
//   either, so the handoff bought no extra scrutiny, only a second cold start.
//
//   The reviewer's independence is preserved, because independence here means "did not
//   write this code", not "cannot edit it". What a fixing reviewer does introduce is an
//   incentive to accept, since accepting is cheaper than fixing. Two things hold that
//   in check: it must return a pass/fail with evidence for every acceptance criterion
//   by name before it may conclude anything, and issuesFound is reported separately
//   from issuesFixed, so quietly finding nothing is visible.
//
//   It also inherits the implementer's file list verbatim. That is not a nicety: the
//   reviewer runs while sibling tasks are live, and a reviewer helpfully adding a
//   constant to a shared file that another task owns is exactly the silent edit-loss
//   the wave scheduler exists to prevent.
//
// Why the main working tree and not isolation: 'worktree'
//   Worktrees give every agent its own checkout, which sounds safer and is not:
//     1. This script has no filesystem and no git, so it cannot merge N worktrees
//        back. A merge agent would have to replay diffs by hand - reintroducing the
//        very conflicts worktrees were supposed to prevent, resolved by a model.
//     2. The conflicts in this codebase are semantic, not textual. Two tasks each
//        append a DbSet line, a permission constant, a locale key, a nav entry. Git
//        merges all of that cleanly and the result frequently does not compile, or
//        quietly loses one of the two edits.
//     3. The architecture tests are whole-assembly checks. Per-worktree builds would
//        each pass while the merged assembly fails feature isolation - the single most
//        likely failure mode in this template.
//     4. The metadata to schedule safely already exists: tasks.md declares each task's
//        files, and the coverage gate in spec-plan already checked disjointness.
//   Worktree isolation IS right for a competing-implementations race - N attempts at
//   one hard task, keep the winner, discard the rest, no merge. That is a different
//   workflow, deliberately not bolted on here.
//
// args
//   {specDir: string}         required.
//   {tasks?: string[]}        run only these task ids - how spec-verify hands back a
//                             round of remaining work.
//   {maxParallel?: number}    wave width cap, 1 to 8 (default 5).
//   {gateEveryWave?: boolean} default false; the gate runs every second wave, after
//                             any red gate, and always at the end. Set true to gate
//                             after every single wave.
//   {maxRepairRounds?: number} gate repair attempts per gate (default 3).
//
// Returns
//   {ok, waves, tasksAttempted, completed, failed, unscheduled, gate, followUps,
//    reportPath}
//   unscheduled means the scheduler could not place a task at all - a dependency cycle
//   or a dependency on an id that does not exist. Fix tasks.md rather than hand-coding.
//
// Files written
//   Application source, by task agents hard-scoped to their own declared file lists.
//   Each task agent ticks only its own checkbox in tasks.md; the report agent
//   reconciles the whole file at the end from this script's authoritative results.
//
// Cost
//   Two subagents per task plus gates, where it used to be up to three - about 150 for
//   a 60-task plan. The larger saving here is per agent rather than in the count: a
//   task agent is handed the criteria it must satisfy and the one contract document
//   that governs it, instead of being sent to read the whole specification and all four.
// =============================================================================

// ---------------------------------------------------------------- arguments
const SPEC_DIR = (args && args.specDir) || (typeof args === 'string' ? args : '')
if (!SPEC_DIR) {
  log('spec-implement requires args {specDir: "specs/<NNN-slug>"}.')
  return { ok: false, error: 'missing-specDir' }
}
const SPEC_PATH = `${SPEC_DIR}/spec.md`
const PLAN_PATH = `${SPEC_DIR}/plan.md`
const TASKS_PATH = `${SPEC_DIR}/tasks.md`
const REPORT_PATH = `${SPEC_DIR}/implementation.md`
const ONLY = args && Array.isArray(args.tasks) && args.tasks.length ? args.tasks : null

// Depth scales the run to the token budget set for this turn; budget.total is null
// when none was set. Bounds come only from budget.total and args - never a clock or a
// random number - so an interrupted run resumes and replays identically.
const DEPTH = budget.total ? Math.max(1, Math.min(3, Math.floor(budget.total / 400000))) : 1
const MAX_PARALLEL = Math.max(1, Math.min(8, (args && args.maxParallel) || (DEPTH >= 2 ? 6 : 5)))
const GATE_EVERY_WAVE = (args && args.gateEveryWave) === true
const MAX_REPAIR_ROUNDS = Math.max(1, Math.min(5, (args && args.maxRepairRounds) || 3))

// Prepended to every prompt. CLAUDE.md is not mentioned on purpose: the workflow
// runtime injects it into every subagent already, so an instruction to read it buys a
// duplicate read of the largest document in the repository, once per agent, and there
// are two agents per task.
//
// The namespace line stays, because it is a guard specific to this template rather
// than something CLAUDE.md tells the agent: this file ships into scaffolded projects
// byte-for-byte while only markdown is namespace-rewritten.
const ORIENT = 'Never invent a root namespace or project file name - derive it from the files you are editing.'

const CONVENTIONS = 'Read .claude/skills/coding-conventions/SKILL.md as well as the guide named for your task.'

// Which contract document governs which kind of work. A task agent used to be told to
// read "the contract documents relevant to this task", which in practice meant opening
// all four; this sends it to the one that actually covers its guide. The file names are
// generic rather than namespace-derived, so the table is safe to keep in a .js file.
const CONTRACT_FOR_SKILL = {
  'backend-entity': 'data-model.md',
  'backend-feature': 'api-contract.md',
  'backend-endpoint': 'api-contract.md',
  permissions: 'api-contract.md',
  'api-error-handling': 'api-contract.md',
  'background-jobs': 'api-contract.md',
  notifications: 'api-contract.md',
  'file-storage': 'api-contract.md',
  'rtk-query-api': 'frontend-contract.md',
  'frontend-crud': 'frontend-contract.md',
  'frontend-page': 'frontend-contract.md',
  'ui-component': 'frontend-contract.md',
  'redux-state': 'frontend-contract.md',
  localization: 'frontend-contract.md',
  'backend-tests': 'test-plan.md',
  'frontend-tests': 'test-plan.md',
}

// ---------------------------------------------------------------- schemas
// A schema on agent() forces a StructuredOutput tool call and returns a validated
// object, so the model retries on a mismatch and the scheduler below can rely on
// tasks[].files actually being an array of strings.
const LOAD_SCHEMA = {
  type: 'object',
  properties: {
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
          verify: { type: 'string' },
          done: { type: 'boolean', description: 'true when the checkbox in tasks.md is already ticked' },
        },
        required: ['id', 'title', 'files'],
      },
    },
    // The criterion texts, so a task agent can be handed the two or three sentences it
    // must satisfy instead of being sent to search a specification that may run to
    // forty kilobytes. This is the single largest input-token saving in the stage.
    acs: {
      type: 'array',
      items: {
        type: 'object',
        properties: { id: { type: 'string' }, text: { type: 'string' } },
        required: ['id', 'text'],
      },
    },
    contracts: { type: 'array', items: { type: 'string' } },
    buildCommand: { type: 'string', description: 'the exact backend build command from the Commands section of CLAUDE.md' },
    backendTestCommand: { type: 'string' },
    webLintCommand: { type: 'string' },
    webTestCommand: { type: 'string' },
  },
  required: ['tasks', 'buildCommand'],
}

const IMPL_SCHEMA = {
  type: 'object',
  properties: {
    taskId: { type: 'string' },
    status: { type: 'string', enum: ['done', 'partial', 'blocked'] },
    filesTouched: { type: 'array', items: { type: 'string' } },
    filesOutsideDeclared: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
    followUps: { type: 'array', items: { type: 'string' } },
    blockedReason: { type: 'string' },
  },
  required: ['taskId', 'status', 'filesTouched', 'summary'],
}

// perAc comes first and is required, so the reviewer has to state a verdict against
// every named criterion with evidence before it can reach a conclusion. A reviewer that
// may also repair what it finds has an incentive to conclude "accept" and stop, and a
// single global verdict field makes that shortcut free; an enumeration does not.
//
// issuesFound and issuesFixed are separate for the same reason: the pair makes it
// visible when a reviewer found problems and fixed none, which a single list would hide.
const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    taskId: { type: 'string' },
    perAc: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          acId: { type: 'string' },
          pass: { type: 'boolean' },
          evidence: { type: 'string', description: 'the file and symbol that satisfies it, or what is missing' },
        },
        required: ['acId', 'pass', 'evidence'],
      },
    },
    issuesFound: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          file: { type: 'string' },
          problem: { type: 'string' },
          fix: { type: 'string' },
          severity: { type: 'string', enum: ['must-fix', 'nice-to-have'] },
        },
        required: ['problem', 'fix', 'severity'],
      },
    },
    issuesFixed: { type: 'array', items: { type: 'string' }, description: 'the problems you actually repaired' },
    issuesLeft: { type: 'array', items: { type: 'string' }, description: 'must-fix problems you could not repair inside your file list, and why' },
    status: { type: 'string', enum: ['done', 'partial', 'blocked'] },
    filesTouched: { type: 'array', items: { type: 'string' } },
    summary: { type: 'string' },
  },
  required: ['taskId', 'perAc', 'issuesFound', 'status', 'summary'],
}

const GATE_SCHEMA = {
  type: 'object',
  properties: {
    ok: { type: 'boolean' },
    commandsRun: { type: 'array', items: { type: 'string' } },
    failures: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          area: { type: 'string', enum: ['backend', 'web', 'environment'] },
          command: { type: 'string' },
          symptom: { type: 'string' },
          files: { type: 'array', items: { type: 'string' } },
        },
        required: ['area', 'symptom'],
      },
    },
    skipped: { type: 'array', items: { type: 'string' }, description: 'commands not run, and why' },
  },
  required: ['ok', 'failures'],
}

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    area: { type: 'string' },
    fixed: { type: 'boolean' },
    changed: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
    needsHuman: { type: 'array', items: { type: 'string' } },
  },
  required: ['area', 'fixed', 'changed'],
}

// ---------------------------------------------------------------- scheduling
// Files almost every feature appends to. Two tasks that both touch one of these
// merge cleanly and then fail to compile, or silently drop one of the two edits,
// so they are treated as a shared claim even when only one task declares them.
//
// KEEP IN SYNC with HOTSPOT_BASENAMES in .claude/workflows/spec-plan.js. These scripts
// cannot import each other, so it is a genuine copy. One deliberate difference: the
// planner's copy also lists index.ts, because a gate that is STRICTER than this
// scheduler is harmless, while a looser one would pass a task list this scheduler then
// has to serialise behind the planner's back. Barrel files are omitted here because
// they already collide by full path, and a global token would needlessly serialise
// tasks touching entirely different barrels.
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
]

// tasks.md is written by a model, so paths arrive with mixed separators, stray "./"
// prefixes and inconsistent casing. Normalising before comparison is what stops two
// spellings of the same file from looking disjoint and landing in the same wave.
function normPath(p) {
  return String(p || '').trim().replace(/\\/g, '/').replace(/^\.\//, '').replace(/^\/+/, '').toLowerCase()
}

// Turns a task into the set of things it exclusively claims for the duration of a
// wave. A task claims each file it declared, plus a shared token for any hotspot it
// touches, plus one "hot:locales" token if it touches any locale file at all - two
// tasks adding keys to different locale files of the same translation still have to
// be serialised, because they are really editing one logical thing.
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

// Which halves of the tree a set of tasks actually touches. The gate uses this to skip
// commands that cannot possibly have anything to say: running the web lint after a wave
// that only edited C# costs a full lint run and an agent's attention to learn nothing.
function waveAreas(wave) {
  const files = wave.flatMap((t) => (t.files || []).map(normPath))
  return {
    backend: files.some((f) => f.indexOf('src/backend') === 0),
    web: files.some((f) => f.indexOf('src/frontend') === 0),
  }
}

// Greedy wave scheduler, deterministic and dependency-respecting.
//
// A wave is a set of tasks that may run at the same time. A task joins the current
// wave only if all its known dependencies are already done, none of its claim tokens
// is taken by a task already in this wave, and the wave is not full. A task marked
// non-parallel gets a wave to itself.
//
// Greedy is deliberate: an optimal packing would not be worth the complexity, and the
// input order from tasks.md already encodes the planner's intended sequence. The guard
// counter and the empty-wave check exist so a malformed dependency graph - a cycle, or
// an "after:" naming a task that was deleted - stalls loudly and reports the tasks it
// could not place, rather than looping or dropping them in silence.
//
// Dependencies on ids that are not in this run (filtered by `known`) are ignored: when
// the caller passes args.tasks to re-run a subset, those dependencies are already done.
function buildWaves(tasks, maxWidth) {
  const done = new Set()
  const known = new Set(tasks.map((t) => t.id))
  const remaining = tasks.slice()
  const waves = []
  let guard = 0
  while (remaining.length && guard < 500) {
    guard++
    const wave = []
    const claimed = new Set()
    for (const t of remaining) {
      if (wave.length >= maxWidth) break
      const deps = (t.dependsOn || []).filter((d) => known.has(d))
      if (deps.some((d) => !done.has(d))) continue
      if (t.parallel === false && wave.length) continue
      const tokens = claimTokens(t)
      if (tokens.some((x) => claimed.has(x))) continue
      wave.push(t)
      tokens.forEach((x) => claimed.add(x))
      if (t.parallel === false) break
    }
    if (!wave.length) {
      log(`Scheduler stalled: ${remaining.length} task(s) have unsatisfiable dependencies and will not run: ${remaining.map((t) => t.id).join(', ')}`)
      break
    }
    for (const t of wave) {
      done.add(t.id)
      remaining.splice(remaining.indexOf(t), 1)
    }
    waves.push(wave)
  }
  return { waves, unscheduled: remaining }
}

// ---------------------------------------------------------------- Load tasks
// Parsing is delegated to an agent rather than done with a regex here for two reasons:
// tasks.md is prose-adjacent and a strict parser would be brittle, and the same agent
// can read the build and test commands out of CLAUDE.md, which keeps those commands out
// of this file where scaffolding would not be able to rewrite them.
//
// This agent is deliberately NOT downgraded to a small model despite being a parse. Its
// files[] output is the sole input to wave disjointness, so a dropped path puts two
// agents on one file at the same moment; its done[] flags decide what gets skipped
// entirely; and the commands it returns are now used by every gate in the run. The
// output is shape-checked below rather than trusted.
phase('Load tasks')
const loaded = await agent(
  [
    ORIENT,
    `Read ${TASKS_PATH} and return every task in structured form, in file order.`,
    'A task line looks like: "- [ ] **T-001** [P] Title - `files:` a, b - `skill:` name - `acs:` AC-001 - `after:` T-000".',
    '"[P]" means parallel=true and its absence means parallel=false. "after:" is dependsOn. A ticked checkbox means done=true.',
    'The declared file list must be reproduced completely and exactly - it is used to decide which tasks may run at the same time.',
    `Then read ${SPEC_PATH} and return every acceptance criterion with its exact id and text.`,
    `Also list the contract documents present in ${SPEC_DIR}.`,
    'Also read the Commands section of CLAUDE.md and return the exact backend build command, backend test command, web lint command and web test command as written there.',
    'Do not modify any file.',
  ].join('\n'),
  { label: 'load tasks.md', schema: LOAD_SCHEMA, effort: 'low' }
)
if (!loaded || !loaded.tasks || !loaded.tasks.length) {
  return { ok: false, error: 'cannot-read-tasks', specDir: SPEC_DIR }
}

// Shape-check what came back. Every one of these is a failure that would otherwise show
// up much later as something far harder to read: a task with no files is invisible to
// the scheduler's collision check, duplicate ids corrupt the done-set, and a command
// string that is a placeholder rather than a command turns every gate into a skip.
const seenIds = new Set()
const malformed = []
for (const t of loaded.tasks) {
  if (!t.id || seenIds.has(t.id)) malformed.push(`duplicate or missing id: ${t.id || '(blank)'}`)
  seenIds.add(t.id)
  if (!t.done && !(t.files || []).length) malformed.push(`${t.id} declares no files`)
}
if (malformed.length) {
  log(`tasks.md did not parse cleanly: ${malformed.slice(0, 8).join('; ')}${malformed.length > 8 ? ` (+${malformed.length - 8} more)` : ''}`)
  log('Fix tasks.md rather than letting the scheduler work from an incomplete file list.')
  return { ok: false, error: 'malformed-tasks', specDir: SPEC_DIR, problems: malformed }
}

// A command string is usable only if it looks like a command. Anything shorter than a
// few characters, or that reads as a placeholder, is dropped and the gate is told to
// find the real one itself.
function usable(cmd) {
  const s = String(cmd || '').trim()
  return s.length > 4 && !/^[<[(]/.test(s) && !/^(n\/?a|none|tbd|unknown)$/i.test(s)
}
const COMMANDS = {
  build: usable(loaded.buildCommand) ? loaded.buildCommand.trim() : '',
  backendTest: usable(loaded.backendTestCommand) ? loaded.backendTestCommand.trim() : '',
  webLint: usable(loaded.webLintCommand) ? loaded.webLintCommand.trim() : '',
  webTest: usable(loaded.webTestCommand) ? loaded.webTestCommand.trim() : '',
}
Object.keys(COMMANDS).forEach((k) => {
  if (!COMMANDS[k]) log(`No usable ${k} command came back from the loader; the gate will read it from CLAUDE.md itself.`)
})

const AC_TEXT = new Map((loaded.acs || []).map((a) => [a.id, a.text]))
if (!AC_TEXT.size) log('The loader returned no acceptance criterion texts; task agents will be sent to read spec.md instead.')

let queue = loaded.tasks.filter((t) => !t.done)
const alreadyDone = loaded.tasks.filter((t) => t.done).map((t) => t.id)
if (alreadyDone.length) log(`Skipping ${alreadyDone.length} task(s) already ticked in tasks.md: ${alreadyDone.join(', ')}`)
if (ONLY) {
  const before = queue.length
  queue = queue.filter((t) => ONLY.indexOf(t.id) !== -1)
  log(`Caller restricted the run to ${queue.length} of ${before} open task(s).`)
}
if (!queue.length) {
  return { ok: true, specDir: SPEC_DIR, waves: 0, tasksAttempted: 0, completed: [], failed: [], note: 'nothing to do' }
}

const schedule = buildWaves(queue, MAX_PARALLEL)
log(`Scheduled ${queue.length - schedule.unscheduled.length} task(s) into ${schedule.waves.length} wave(s), max width ${MAX_PARALLEL}.`)
schedule.waves.forEach((w, i) => log(`  wave ${i + 1}: ${w.map((t) => t.id).join(', ')}`))
if (schedule.unscheduled.length) {
  log(`NOT scheduled - dependency cycle or unknown dependency: ${schedule.unscheduled.map((t) => t.id).join(', ')}`)
}

// Builds the context block for one task: the criteria it must satisfy, in full, and the
// single contract document that governs its guide.
function taskContext(t) {
  const texts = (t.acs || []).map((id) => (AC_TEXT.has(id) ? `  ${id}: ${AC_TEXT.get(id)}` : `  ${id}: (text not found in the spec - look it up in ${SPEC_PATH})`))
  const contract = CONTRACT_FOR_SKILL[t.skill]
  return [
    texts.length
      ? `Acceptance criteria this task must satisfy literally:\n${texts.join('\n')}`
      : `Acceptance criteria this task must satisfy: ${(t.acs || []).join(', ') || '(none declared)'} - look them up in ${SPEC_PATH}.`,
    contract
      ? `Read ${PLAN_PATH} and ${SPEC_DIR}/${contract}, which is the contract document governing this kind of work. You do not need the other contract documents.`
      : `Read ${PLAN_PATH} and the contract documents in ${SPEC_DIR} relevant to this task.`,
  ].join('\n')
}

// ---------------------------------------------------------------- Implement
// Wave by wave. The boundary between waves is a plain for-loop, not parallel(): wave
// N+1 edits files that wave N creates, so it genuinely must wait. Inside a wave nothing
// blocks, which is where the time is saved.
const completed = []
const failed = []
const followUps = []
let gate = { ok: true, failures: [], skipped: [] }
let lastGateRed = false

for (let w = 0; w < schedule.waves.length; w++) {
  const wave = schedule.waves[w]
  phase('Implement')
  log(`Wave ${w + 1}/${schedule.waves.length}: ${wave.map((t) => t.id).join(', ')}`)

  // Two stages per task: implement, then an independent reviewer that repairs what it
  // finds. See the header for why the reviewer repairs rather than handing to a third
  // agent, and why it inherits the implementer's file list.
  //
  // pipeline(), not parallel(): each task's chain is independent, so a fast task is
  // already being reviewed while a slow sibling is still being written.
  const results = await pipeline(
    wave,
    (t) =>
      agent(
        [
          ORIENT,
          CONVENTIONS,
          `You are executing task ${t.id} from ${TASKS_PATH}.`,
          `Title: ${t.title}`,
          `Governing guide: read .claude/skills/${t.skill || 'coding-conventions'}/SKILL.md and follow it exactly.`,
          taskContext(t),
          '',
          'HARD RULE - you may create or edit ONLY these files:',
          (t.files || []).map((f) => `  ${f}`).join('\n'),
          'Other agents are editing other files at this very moment. If you believe you must touch a file outside that list,',
          'do NOT touch it: finish what you can, report status "partial", and list the file under filesOutsideDeclared with the reason.',
          `You may also tick this task's own checkbox in ${TASKS_PATH} at the very end - change nothing else in that file.`,
          '',
          'Follow the conventions in the guides exactly: file layout, naming, accessibility modifiers, sealed types, primary constructors,',
          'documentation comments, kebab-case web file names, barrel exports, and no hard-coded user-visible strings.',
          t.verify ? `Self-check before finishing: ${t.verify}` : 'Self-check that the criteria above are actually satisfied before finishing.',
          'Do not run the full build or test suite - a separate gate does that. Do not commit anything.',
        ].join('\n'),
        { label: `${t.id} implement`, phase: 'Implement', schema: IMPL_SCHEMA }
      ),
    (impl, t) => {
      if (!impl) return null
      return agent(
        [
          ORIENT,
          CONVENTIONS,
          `You are an independent reviewer for task ${t.id}: "${t.title}". You did NOT write this code.`,
          `Read the current state of these files: ${(t.files || []).join(', ')}.`,
          `Read .claude/skills/${t.skill || 'coding-conventions'}/SKILL.md.`,
          taskContext(t),
          '',
          'Work in this order and do not skip the first step.',
          '',
          'STEP 1 - enumerate. For EVERY acceptance criterion named above, state pass or fail and cite the file and symbol that satisfies it,',
          'or say exactly what is missing. Include its failure branch, not only the happy path. You may not conclude anything before this list is complete.',
          '',
          'STEP 2 - look for what is WRONG or MISSING beyond the criteria:',
          '- did the implementer skip a step the guide requires - a mirrored constant, a barrel export, a translation key, a validator, a permission on the endpoint, documentation on a new type?',
          '- does it violate a convention - naming, accessibility modifier, sealed, primary constructor, a per-file using that belongs in the shared global-usings file, a hard-coded user-visible string?',
          '- would it break the architecture tests by depending on another feature slice without the allowed escape hatch?',
          '- is there dead, duplicated or speculative code?',
          'Record everything you find under issuesFound, including the things you are about to fix.',
          '',
          'STEP 3 - repair every must-fix issue yourself, and list what you repaired under issuesFixed.',
          'HARD RULE - you may edit ONLY these files:',
          (t.files || []).map((f) => `  ${f}`).join('\n'),
          'Other agents are editing other files at this very moment. A must-fix issue whose repair would need a file outside that list',
          'goes under issuesLeft with the file named - do NOT touch it, however small the edit looks.',
          'If an issue turns out to be wrong on a closer look, leave the code alone and say so in the summary.',
          '',
          'Do not run the full build or test suite - a separate gate does that. Do not commit anything.',
          '',
          'The implementer reported:',
          JSON.stringify(impl),
        ].join('\n'),
        { label: `${t.id} review`, phase: 'Implement', schema: REVIEW_SCHEMA }
      ).then((rev) => {
        if (!rev) return impl
        const mustLeft = (rev.issuesLeft || []).length
        const found = (rev.issuesFound || []).filter((i) => i.severity === 'must-fix').length
        const failing = (rev.perAc || []).filter((a) => !a.pass)
        if (found || mustLeft) log(`${t.id}: reviewer found ${found} must-fix issue(s), fixed ${(rev.issuesFixed || []).length}, left ${mustLeft}.`)
        if (failing.length) log(`${t.id}: ${failing.length} criterion/criteria still failing after review: ${failing.map((a) => a.acId).join(', ')}`)
        // The reviewer's verdict is authoritative over the implementer's: it looked at
        // the code afterwards, and a criterion it marked failing is not done whatever
        // the implementer claimed.
        return {
          taskId: t.id,
          status: mustLeft || failing.length ? 'partial' : rev.status || impl.status,
          filesTouched: (impl.filesTouched || []).concat(rev.filesTouched || []),
          filesOutsideDeclared: impl.filesOutsideDeclared || [],
          summary: `${impl.summary} | review: ${rev.summary}`,
          followUps: (impl.followUps || []).concat(rev.issuesLeft || []).concat(failing.map((a) => `${a.acId} not satisfied: ${a.evidence}`)),
          blockedReason: impl.blockedReason,
        }
      })
    }
  )

  results.forEach((r, i) => {
    const t = wave[i]
    if (!r) {
      failed.push({ id: t.id, reason: 'agent returned nothing' })
      return
    }
    if (r.status === 'done') completed.push(r.taskId || t.id)
    else failed.push({ id: r.taskId || t.id, reason: r.blockedReason || r.summary || r.status })
    ;(r.followUps || []).forEach((f) => followUps.push(`${t.id}: ${f}`))
    ;(r.filesOutsideDeclared || []).forEach((f) => followUps.push(`${t.id}: wanted to edit the undeclared file ${f}`))
  })

  // Gate policy. A build check between waves surfaces a mistake next to the task that
  // caused it, but one after every single wave is the largest line item in this stage
  // on a big feature - twelve waves means twelve builds plus their repair rounds. So:
  // every second wave, always after a red one (a red gate means the tree is broken and
  // the next wave is building on it), always at the end, and never when the budget is
  // nearly gone. Every skip is logged, because a silently skipped gate reads like a
  // passing one.
  const lastWave = w === schedule.waves.length - 1
  const budgetTight = budget.total && budget.remaining() < 120000
  const scheduledGate = GATE_EVERY_WAVE || lastGateRed || w % 2 === 1
  if (!lastWave && (!scheduledGate || budgetTight)) {
    if (budgetTight) log(`Skipping the wave-${w + 1} build gate: ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
    else log(`No gate after wave ${w + 1}; the next one runs after wave ${w + 2}.`)
    continue
  }

  phase('Gate')
  const areas = waveAreas(wave)
  // Only the halves of the tree this wave touched can have broken. On the last wave
  // everything runs regardless, because that gate is the one the caller reads.
  const runBackend = lastWave || areas.backend
  const runWeb = lastWave || areas.web
  if (!lastWave && !runBackend) log(`Wave ${w + 1} touched no backend file; skipping the backend build.`)
  if (!lastWave && !runWeb) log(`Wave ${w + 1} touched no web file; skipping the web lint.`)

  let round = 0
  let previousSymptoms = ''
  while (round < MAX_REPAIR_ROUNDS) {
    gate = await agent(
      [
        ORIENT,
        'Run the gate.',
        COMMANDS.build || COMMANDS.backendTest || COMMANDS.webLint || COMMANDS.webTest
          ? [
              'Use these commands, read from the Commands section of CLAUDE.md at the start of this run:',
              COMMANDS.build ? `  backend build: ${COMMANDS.build}` : '',
              COMMANDS.backendTest ? `  backend tests: ${COMMANDS.backendTest}` : '',
              COMMANDS.webLint ? `  web lint:      ${COMMANDS.webLint}` : '',
              COMMANDS.webTest ? `  web tests:     ${COMMANDS.webTest}` : '',
              'If one of them fails to START - an unknown command, a missing project, a path that does not exist, as opposed to a compile or test failure -',
              'then it was transcribed wrongly. Read the Commands section of CLAUDE.md, use what it says, and note which command you actually ran.',
            ]
              .filter(Boolean)
              .join('\n')
          : 'Read the Commands section of CLAUDE.md and use the commands EXACTLY as written there.',
        lastWave
          ? 'Full gate: the backend build, the backend tests, the web lint and the web tests. Run all four.'
          : `Fast gate: ${[runBackend ? 'the backend build' : '', runWeb ? 'the web lint' : ''].filter(Boolean).join(' and ') || 'nothing - report ok'}. Run nothing else; the other half of the tree was not touched in this wave.`,
        'The backend tests need a running database as described in CLAUDE.md. If it is unavailable, that is NOT a code failure -',
        'record the command under "skipped" with the reason and keep ok=true for it.',
        'For each real failure, report the area, the command, a one-line symptom and the source files implicated.',
        'Do not fix anything, and do not commit.',
      ].join('\n'),
      { label: lastWave ? 'full gate' : `build gate w${w + 1}`, phase: 'Gate', schema: GATE_SCHEMA, effort: 'low' }
    )
    if (!gate) {
      gate = { ok: false, failures: [{ area: 'environment', symptom: 'the gate agent returned nothing' }], skipped: [] }
      break
    }
    ;(gate.skipped || []).forEach((s) => log(`Gate skipped: ${s}`))
    if (gate.ok || !gate.failures.length) {
      log(`Gate green after wave ${w + 1}${round ? ` (${round} repair round(s))` : ''}.`)
      break
    }
    if (budget.total && budget.remaining() < 100000) {
      log(`Gate red with ${gate.failures.length} failure(s), but only ${Math.round(budget.remaining() / 1000)}k tokens remain - stopping repairs.`)
      break
    }

    // A repair round that leaves the failures byte-for-byte identical did not help, and
    // three rounds of the same is guaranteed waste - which is exactly what happens when
    // the cause is environmental rather than in the code.
    const symptoms = gate.failures.map((f) => `${f.area}:${f.symptom}`).sort().join('||')
    if (round && symptoms === previousSymptoms) {
      log(`Repair round ${round} changed nothing - the same ${gate.failures.length} failure(s) remain. Stopping rather than retrying identically.`)
      gate.failures.forEach((f) => followUps.push(`gate/${f.area}: unresolved after repair - ${f.symptom}`))
      break
    }
    previousSymptoms = symptoms

    const failingAreas = ['backend', 'web'].filter((a) => gate.failures.some((f) => f.area === a))
    log(`Gate red: ${gate.failures.length} failure(s) in ${failingAreas.join(' and ') || 'the environment'}; repair round ${round + 1}.`)
    if (!failingAreas.length) break

    // One fixer per area, running together. This barrier is safe rather than merely
    // convenient: src/backend and src/frontend are disjoint subtrees, and each fixer is
    // told to stay inside its own. Both must finish before the gate is re-run, since
    // the gate measures the tree as a whole.
    const fixes = await parallel(
      failingAreas.map((area) => () =>
        agent(
          [
            ORIENT,
            CONVENTIONS,
            `Fix the ${area} gate failures below. Reproduce them first.`,
            area === 'backend'
              ? `Reproduce with: ${COMMANDS.build || 'the backend build command in the Commands section of CLAUDE.md'}. Stay inside src/backend and do not touch anything under src/frontend.`
              : `Reproduce with: ${COMMANDS.webLint || 'the web lint command in the Commands section of CLAUDE.md'}. Stay inside src/frontend and do not touch anything under src/backend.`,
            `Do not weaken or delete a test to make it pass, and do not change ${SPEC_PATH}. Read the relevant guide under .claude/skills before editing.`,
            'If a failure comes from outside your area or from a missing environment dependency, do not guess - report it under needsHuman.',
            '',
            JSON.stringify(gate.failures.filter((f) => f.area === area)),
          ].join('\n'),
          { label: `fix ${area} r${round + 1}`, phase: 'Gate', schema: FIX_SCHEMA }
        )
      )
    )
    fixes.filter(Boolean).forEach((f) => (f.needsHuman || []).forEach((n) => followUps.push(`gate/${f.area}: ${n}`)))
    round++
  }
  lastGateRed = Boolean(gate && gate.ok === false)
}

// ---------------------------------------------------------------- Report
// The checkbox state in tasks.md is the one piece of shared mutable state in this
// workflow: each task agent ticks its own line, which is safe but leaves gaps when an
// agent dies mid-task. This final agent reconciles the file against the authoritative
// results collected above, so the next stage reads accurate state.
phase('Report')
const report = await agent(
  [
    ORIENT,
    `Write ${REPORT_PATH} summarising this implementation run. Read ${TASKS_PATH} for context.`,
    'Sections: what was implemented (task id, files, acceptance criteria), what failed and why, the gate result,',
    'and "Follow-ups" listing anything an agent flagged that is not yet done.',
    `Also reconcile the checkbox state in ${TASKS_PATH} with reality: tick completed tasks, leave failed ones unticked,`,
    'and add an HTML comment naming the reason after any failed task line.',
    `Write ONLY ${REPORT_PATH} and those checkbox and comment edits in ${TASKS_PATH}. Do not modify application code.`,
    '',
    JSON.stringify({
      completed,
      failed,
      unscheduled: schedule.unscheduled.map((t) => t.id),
      gate,
      followUps,
      waves: schedule.waves.map((wv) => wv.map((t) => t.id)),
    }),
  ].join('\n'),
  { label: 'write implementation.md', phase: 'Report', effort: 'low' }
)

return {
  ok: failed.length === 0 && !schedule.unscheduled.length && gate.ok !== false,
  specDir: SPEC_DIR,
  waves: schedule.waves.length,
  tasksAttempted: queue.length,
  completed,
  failed,
  unscheduled: schedule.unscheduled.map((t) => t.id),
  gate,
  followUps,
  reportPath: REPORT_PATH,
  reportWritten: Boolean(report),
  nextStep: `Run spec-verify with args {specDir: "${SPEC_DIR}"}.`,
}
