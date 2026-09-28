/**
 * The brief every unattended run is given, assembled from this project's own configuration.
 *
 * It used to be a hard-coded string naming one repository's directories and one product's
 * conventions, which made the runner unportable for no reason: the *shape* of the brief —
 * read the guide, route the work, load the procedure you owe, review in tiers, verify, do
 * not commit — is the same everywhere. Only the names change. So the shape lives here and
 * the names come from `agentic.config.json`.
 *
 * Two things are deliberately stated in the brief even though they are also enforced in
 * code: the routing table and the review order. A run that is told them produces the right
 * delegations the first time; a run that is only refused afterwards pays for a whole extra
 * attempt to learn what a paragraph could have said.
 */
import { config, PROJECT_NAME } from "./project-config.mjs";

/** The longest agent name, so the routing table's arrows line up. */
function pad(rows) {
  const width = Math.max(0, ...rows.map(([left]) => left.length));
  return rows.map(([left, right]) => `  ${left.padEnd(width)}  →  ${right}`).join("\n");
}

/** `src/db/` from `^src/db/` — a path a person can read, not a regular expression. */
function readablePath(pattern) {
  return String(pattern)
    .replace(/^\^/, "")
    .replace(/\$$/, "")
    .replace(/\\\//g, "/")
    .replace(/\\\./g, ".");
}

function routingTable() {
  const builders = (config.departments ?? []).filter((d) => d.phase === "build" && !d.always);
  if (builders.length === 0) return "";
  return pad(builders.map((d) => [(d.match ?? []).map(readablePath).join(", "), d.agent]));
}

function designAgents() {
  return (config.departments ?? []).filter((d) => d.phase === "design").map((d) => d.agent);
}

function reviewTiers() {
  const reviewers = (config.departments ?? []).filter((d) => d.phase === "review");
  const tiers = [...new Set(reviewers.map((d) => d.order ?? 1))].sort((a, b) => a - b);
  return tiers.map((tier, i) => {
    const agents = reviewers.filter((d) => (d.order ?? 1) === tier).map((d) => d.agent);
    const note =
      agents.length > 1
        ? "INDEPENDENT — dispatch together"
        : i === 0
          ? ""
          : "after the tier above has reported";
    return `  ${i + 1}. ${agents.join(" · ")}${note ? `   ${note}` : ""}`;
  });
}

function skillLine() {
  const skills = config.skills ?? [];
  if (skills.length === 0) return "";
  const named = skills.map((s) => `\`${s.skill}\` ${s.when ?? s.why ?? ""}`.trim()).join("; ");
  return `\n**Load the skill your work owes** — ${named}.\n`;
}

function conventionsBlock() {
  const lines = config.project.conventions ?? [];
  if (lines.length === 0) return "";
  return (
    `\nThe conventions that break at runtime rather than at typecheck:\n` +
    lines.map((c) => `  - ${c}`).join("\n") +
    "\n"
  );
}

/**
 * @param {{ memory?: string, history?: string }} parts
 *   `memory` is what *other* tasks learned; `history` is this task's own failed attempts.
 *   Both are already formatted by their own modules and are pasted in as-is.
 * @returns {string} the brief, ending with `TASK:` — the caller appends the task itself.
 */
export function buildBrief({ memory = "", history = "" } = {}) {
  const routing = routingTable();
  const design = designAgents();
  const guide = config.docs.guide ?? "CLAUDE.md";

  return `You are working unattended in the ${PROJECT_NAME} repository. No human will
answer a question, so do not ask one — make the reasonable call, state the assumption in
your final message, and finish the task.

Read ${guide} first and follow its conventions exactly.
${conventionsBlock()}
When you are done, \`${config.commands.verify}\` must pass. It runs the static gate and
adds whatever live checks the paths you touched demand. Do not report success without
running it. Do not commit or push — the runner handles that.

## The team this task goes through

You are the lead, not the whole team. Route the work with the Task tool:

${routing || "  (this project declares no specialist builders — do the work yourself)"}
${skillLine()}
## The order, and what runs together
${
  design.length > 0
    ? `
**If this task changes a screen, delegate to \`${design.join("` and `")}\` FIRST, before a
line is written**, and build from what it gives you. It returns a design brief: the layout,
what is loudest, every state that is not the happy one, how each control behaves, and what
a narrow phone screen looks like.

This is not a formality. A designer that runs only after the build gives its opinion exactly
when acting on it means rebuilding. A run that writes the page first and asks afterwards is
refused.

Then the writers, then \`${config.commands.verify}\`, then:
`
    : `
The writers first, then \`${config.commands.verify}\`, then:
`
}
${reviewTiers().join("\n")}

**Send each tier in a single message.** Reviewers in the same tier ask unrelated questions,
so waiting for each in turn spends wall-clock for nothing. Read their verdicts together.

If one returns CHANGES NEEDED, fix what it names and run that tier again before moving on.
Repeating a review is expected and is not an ordering mistake.
${memory}
If this task teaches you something durable about THIS repository that a future unrelated
task would trip on too — a trap in the tooling, a convention no guide states, an assumption
that turned out false — record it before you finish:

  node scripts/record-lesson.mjs --title "<short imperative title>" --scope <a keyword a future task's brief would contain, or "always"> --body "<what to do instead, and why. two or three sentences.>"

(One line on purpose: it has to run the same in bash and in PowerShell.)

Record nothing if nothing surprised you. A memory full of the obvious is worse than an
empty one, because it crowds real lessons out of every future brief.
${history}
TASK:
`;
}
