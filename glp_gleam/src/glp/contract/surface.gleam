//// glp/contract/surface — the L0 contract surface: the runtime-free description of the
//// GLP capability that every ring realization is held to (feature 101, C1 · FR-001).
////
//// Why this package exists. The engineer directive needs one GLP capability on BEAM at the
//// workstation (ring L1b) *and* on AtomVM inside the MAUI Blazor Hybrid app (ring L1a/L2).
//// `LATTICE.md` line 27 forbids L1a and L1b sharing anything directly and pushes whatever
//// both need down into L0 — but L0 admits **zero third-party runtime dependencies**, and
//// BEAM and AtomVM are both third-party runtimes. Taken literally the directive is
//// unsatisfiable. The shape LATTICE line 35 already prescribes is the one that works, and
//// it is what this package implements: **the contract sits at L0 and is runtime-free; each
//// ring carries its own realization of it.** Recorded as `008` FR-017 / FR-018.
////
//// The consequence for glpnet is that its delivery mode is **resynthesis, never copy**.
//// Nothing under `glp_runtime/`, `glp_multiagent/` or `programs/` is part of the delivered
//// set (FR-005, guarded by test/ring/test_retention.sh).
////
//// C1-R, the purity rule this module is the anchor for: **no module under `glp/contract/`
//// may reference a third-party runtime.** The positive control for that rule lives in
//// test/ring/test_contract_purity.sh — it introduces a runtime dependency here and asserts
//// the build FAILS (SC-004). A purity rule with no failing case is not a rule.
////
//// Empty-but-building at T002 by intent: the runtime-free surface is *measured* into this
//// package by T012/T013, not assumed. `gleam build` flags "empty module" — expected for an
//// intentional skeleton, as with the feature-033 placeholders.
