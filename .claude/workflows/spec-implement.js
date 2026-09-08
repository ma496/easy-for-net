export const meta = {
  name: 'spec-implement',
  description: 'Execute specs/<NNN-slug>/tasks.md in file-disjoint waves, with an independent review per task and build and test gates',
  whenToUse: 'Third stage of spec-driven development. Writes application code, one agent per task, scheduled so no two concurrent agents touch the same file.',
  phases: [
    { title: 'Load tasks', detail: 'parse tasks.md into a dependency graph with declared file sets' },
    { title: 'Implement', detail: 'wave by wave: implement, independent review, repair' },
    { title: 'Gate', detail: 'build and test after each wave, repair failures, full gate at the end' },
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
//   gets an independent reviewer that did not write the code, and a repair pass when
//   the reviewer finds something must-fix. Builds and tests gate each wave.
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
//   {gateEveryWave?: boolean} default true; false builds only at the end.
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
//   Roughly two subagents per task plus gates - about 59 for a 20-task plan.
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
const GATE_EVERY_WAVE = (args && args.gateEveryWave) !== false
const MAX_REPAIR_ROUNDS = Math.max(1, Math.min(5, (args && args.maxRepairRounds) || 3))

// Prepended to every prompt. Subagents start cold, so they are pointed at the markdown
// that carries the conventions rather than being told them here: this file ships into
// scaffolded projects byte-for-byte while only markdown is namespace-rewritten.
const ORIENT = [
  'Before writing any code: read CLAUDE.md at the repo root, then .claude/skills/spec-driven/SKILL.md,',
  'then the guide named in your task, then .claude/skills/coding-conventions/SKILL.md.',
  'CLAUDE.md is the authority on the build and test commands, the solution and project file names, and the layout.',
  'Never invent a root namespace or project file name - derive it from the files you are editing.',
].join(' ')

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

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    taskId: { type: 'string' },
    verdict: { type: 'string', enum: ['accept', 'repair'] },
    issues: {
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
  },
  required: ['taskId', 'verdict', 'issues'],
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
// Barrel index.ts files are not listed: they collide by full path already, and a
// global token would needlessly serialise tasks touching different barrels.
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
phase('Load tasks')
const loaded = await agent(
  [
    ORIENT,
    `Read ${TASKS_PATH} and return every task in structured form, in file order.`,
    'A task line looks like: "- [ ] **T-001** [P] Title - `files:` a, b - `skill:` name - `acs:` AC-001 - `after:` T-000".',
    '"[P]" means parallel=true and its absence means parallel=false. "after:" is dependsOn. A ticked checkbox means done=true.',
    `Also list the contract documents present in ${SPEC_DIR}.`,
    'Also read the Commands section of CLAUDE.md and return the exact backend build command, backend test command, web lint command and web test command as written there.',
    'Do not modify any file.',
  ].join('\n'),
  { label: 'load tasks.md', schema: LOAD_SCHEMA, effort: 'low' }
)
if (!loaded || !loaded.tasks || !loaded.tasks.length) {
  return { ok: false, error: 'cannot-read-tasks', specDir: SPEC_DIR }
}

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

// ---------------------------------------------------------------- Implement
// Wave by wave. The boundary between waves is a plain for-loop, not parallel(): wave
// N+1 edits files that wave N creates, so it genuinely must wait. Inside a wave nothing
// blocks, which is where the time is saved.
const completed = []
const failed = []
const followUps = []
let gate = { ok: true, failures: [], skipped: [] }

for (let w = 0; w < schedule.waves.length; w++) {
  const wave = schedule.waves[w]
  phase('Implement')
  log(`Wave ${w + 1}/${schedule.waves.length}: ${wave.map((t) => t.id).join(', ')}`)

  // Three stages per task: implement, then an independent review by an agent that did
  // not write the code, then a repair only if the reviewer found something must-fix.
  // The reviewer is separate on purpose - an agent asked to check its own work grades
  // generously, and this is the last chance to catch a skipped convention before the
  // build gate turns it into a compile error somewhere else.
  //
  // pipeline(), not parallel(): each task's chain is independent, so a fast task is
  // already being reviewed while a slow sibling is still being written.
  const results = await pipeline(
    wave,
    (t) =>
      agent(
        [
          ORIENT,
          `You are executing task ${t.id} from ${TASKS_PATH}.`,
          `Title: ${t.title}`,
          `Governing guide: read .claude/skills/${t.skill || 'coding-conventions'}/SKILL.md and follow it exactly.`,
          `Context to read first: ${SPEC_PATH}, ${PLAN_PATH}, and the contract documents in ${SPEC_DIR} relevant to this task.`,
          `Acceptance criteria this task must satisfy: ${(t.acs || []).join(', ') || '(none declared)'} - look them up in ${SPEC_PATH} and satisfy them literally.`,
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
          `You are an independent reviewer for task ${t.id}: "${t.title}". You did NOT write this code.`,
          `Read the current state of these files: ${(t.files || []).join(', ')}.`,
          `Read .claude/skills/${t.skill || 'coding-conventions'}/SKILL.md, the coding-conventions guide, and the criteria ${(t.acs || []).join(', ')} in ${SPEC_PATH}.`,
          '',
          'Your job is to find what is WRONG or MISSING. Check specifically:',
          '- does the code actually satisfy each named acceptance criterion, including its failure branch?',
          '- did the implementer skip a step the guide requires - a mirrored constant, a barrel export, a translation key, a validator, a permission on the endpoint, documentation on a new type?',
          '- does it violate a convention - naming, accessibility modifier, sealed, primary constructor, a per-file using that belongs in the shared global-usings file, a hard-coded user-visible string?',
          '- would it break the architecture tests by depending on another feature slice without the allowed escape hatch?',
          '- is there dead, duplicated or speculative code?',
          'Return verdict "accept" only when there are no must-fix issues. Report issues; do not fix them, and do not edit any file.',
          '',
          'The implementer reported:',
          JSON.stringify(impl),
        ].join('\n'),
        { label: `${t.id} review`, phase: 'Implement', schema: REVIEW_SCHEMA }
      ).then((rev) => ({ impl, rev }))
    },
    (pair, t) => {
      if (!pair) return null
      const must = ((pair.rev && pair.rev.issues) || []).filter((i) => i.severity === 'must-fix')
      if (!pair.rev || pair.rev.verdict === 'accept' || !must.length) return pair.impl
      log(`${t.id}: ${must.length} must-fix issue(s) from review; repairing.`)
      return agent(
        [
          ORIENT,
          `Repair task ${t.id}: "${t.title}". An independent reviewer found must-fix issues.`,
          'HARD RULE - you may edit ONLY these files:',
          (t.files || []).map((f) => `  ${f}`).join('\n'),
          `Read .claude/skills/${t.skill || 'coding-conventions'}/SKILL.md first.`,
          'Fix every must-fix issue. If an issue is wrong, leave the code alone and explain in the summary.',
          '',
          JSON.stringify(must),
        ].join('\n'),
        { label: `${t.id} repair`, phase: 'Implement', schema: IMPL_SCHEMA }
      ).then((fixed) => fixed || pair.impl)
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

  // Gate policy: a cheap build-only check after each wave so a mistake surfaces next to
  // the task that caused it, and the full build-plus-tests-plus-lint gate at the end.
  // The per-wave gate is the first thing dropped when the budget gets tight, and that
  // is logged - a silently skipped gate would read like a passing one.
  const lastWave = w === schedule.waves.length - 1
  const budgetTight = budget.total && budget.remaining() < 120000
  if (!lastWave && (!GATE_EVERY_WAVE || budgetTight)) {
    if (budgetTight) log(`Skipping the wave-${w + 1} build gate: ${Math.round(budget.remaining() / 1000)}k tokens remaining.`)
    continue
  }

  phase('Gate')
  let round = 0
  while (round < MAX_REPAIR_ROUNDS) {
    gate = await agent(
      [
        ORIENT,
        'Run the gate. Read the Commands section of CLAUDE.md and use the commands EXACTLY as written there.',
        lastWave
          ? 'Full gate: the backend build, the backend tests, the web lint and the web tests. Run all four.'
          : 'Fast gate: the backend build only, plus the web lint command if any file under the web app changed in this wave.',
        'The backend tests need a running database as described in CLAUDE.md. If it is unavailable, that is NOT a code failure -',
        'record the command under "skipped" with the reason and keep ok=true for it.',
        'For each real failure, report the area, the command, a one-line symptom and the source files implicated.',
        'Do not fix anything, and do not commit.',
      ].join('\n'),
      { label: lastWave ? 'full gate' : `build gate w${w + 1}`, phase: 'Gate', schema: GATE_SCHEMA }
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
    const areas = ['backend', 'web'].filter((a) => gate.failures.some((f) => f.area === a))
    log(`Gate red: ${gate.failures.length} failure(s) in ${areas.join(' and ') || 'the environment'}; repair round ${round + 1}.`)
    if (!areas.length) break

    // One fixer per area, running together. This barrier is safe rather than merely
    // convenient: src/backend and src/frontend are disjoint subtrees, and each fixer is
    // told to stay inside its own. Both must finish before the gate is re-run, since
    // the gate measures the tree as a whole.
    const fixes = await parallel(
      areas.map((area) => () =>
        agent(
          [
            ORIENT,
            `Fix the ${area} gate failures below. Reproduce them first with the relevant command from the Commands section of CLAUDE.md.`,
            area === 'backend'
              ? 'Stay inside src/backend. Do not touch anything under src/frontend.'
              : 'Stay inside src/frontend. Do not touch anything under src/backend.',
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
    `Write ${REPORT_PATH} summarising this implementation run. Read ${TASKS_PATH} and ${SPEC_PATH} for context.`,
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
  { label: 'write implementation.md', phase: 'Report' }
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
