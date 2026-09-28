# docs/capabilities — what the product promises

One record per capability: what it does, the behaviours a person can rely on, and the
constraints that are not obvious from the code.

These are read at **planning** time, not only by people. When a spec touches an area with a
record here, the planner carries that record's observable behaviours into the task's
**Done when** section — so a change that satisfies its own brief and quietly breaks a
standing promise is caught before it ships rather than after.

A record answers three questions:

- **What can someone do with this?** In the language of the person using it, not the code.
- **What must stay true?** The behaviours other work is allowed to depend on.
- **What does it deliberately not do?** The boundary, so nobody re-implements it by accident.

Keep them current with the product rather than with the plan. A capability record that
describes an intention is worse than none, because everything downstream treats it as fact.
