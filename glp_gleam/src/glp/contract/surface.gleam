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
//// ## The measured surface (T012/T013, 2026-09-03)
////
//// Which modules are runtime-free was **measured**, not assumed — by
//// `test/ring/analyze_imports.py`, taking the transitive closure over `gleam/erlang`
//// imports and `@external(erlang, ...)` FFI declarations. Transitivity is the whole
//// point: a clean-looking module that imports a tainted one still drags the runtime in
//// at build time, and a direct-only scan would have called ~87 of 100 modules pure and
//// been wrong about most of them.
////
////   denominator  100 modules scanned
////   runtime-free  71  (69 of them constitute the contract surface)
////   tainted       29  — 16 directly, 13 transitively
////   not read       0
////
//// The taint is confined to exactly where it should be: the FFI/IO boundary (`repl/`,
//// `engine/kernels`, `be/server`, `fe/client`, `glp_embed`) and the process-based link
//// primitives and transports. Everything else — the parser, the whole type checker, the
//// compiler and bytecode, the term/heap/unify core, the codecs and the link reliability
//// layer — is runtime-free today.
////
//// The surface is enumerated in `test/ring/contract-surface.list` and enforced by
//// `test/ring/check_contract_purity.sh` (C1-R). Per Principle IV-b the extraction is
//// **additive**: those modules stay exactly where they are; the manifest declares which
//// of them constitute L0. Nothing is moved, and nothing is copied.
////
//// This module carries no public definitions by design — the contract is a *boundary*,
//// declared by the manifest and enforced by the gate, not a facade to route calls
//// through. `gleam build` flags "empty module"; that is expected here, as with the
//// feature-033 placeholders.
