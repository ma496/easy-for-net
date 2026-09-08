# Specifications

One directory per feature, `NNN-slug`, numbered in the order features were specified. These are
source, not scratch work — they are committed, reviewed, and kept in step with the code.

```
specs/001-user-csv-export/
  spec.md               what and why, with numbered EARS acceptance criteria
  plan.md               the chosen design and why it beat the alternatives
  data-model.md         entities, keys, interfaces, the migration to run
  api-contract.md       endpoints, validators, permissions, error codes
  frontend-contract.md  API slices, DTOs, routes, components, translation keys
  test-plan.md          what proves each criterion, and where that test lives
  tasks.md              the ordered work list
  implementation.md     what landed, what failed, follow-ups
  verification.md       the traceability matrix: criterion to code to test
```

The documents are produced by the four workflows under `.claude/workflows/`, driven by `/specify`,
`/plan`, `/implement` and `/verify`. See `.claude/skills/spec-driven/SKILL.md` for the loop, the
document templates and the gate at each stage.

`AC-nnn` and `T-nnn` ids are permanent. Amend their text in place and append new ids at the end —
renumbering breaks the traceability matrix and the workflows' ability to resume.

Run `/specify` one at a time. The next directory number is chosen by reading this directory, so two
concurrent runs would claim the same one.
