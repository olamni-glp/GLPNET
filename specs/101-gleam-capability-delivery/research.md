# Research — 101-gleam-capability-delivery

**Date** 2026-09-03 · **Host** GAVRIELLA. Every entry states its instrument; an unmeasured entry says so.

## R1 · Does the capability depend on the Dart runtime?

- **Decision:** NO. Treat `glp_gleam` as self-standing; do not migrate Dart (FR-005).
- **Instrument:** `glp_gleam/gleam.toml` dependency block + `grep` over `glp_gleam/src`.
- **Measured:** deps are `gleam_stdlib` + `gleam_erlang` (dev `gleeunit`). 88 files match `dart|glp_runtime`
  and **every match is a doc comment** of the form `Dart source of truth: glp_runtime/lib/...`.
- **Rationale:** provenance annotations are not edges. Documentation does not need to travel for code to build.
- **Refuter:** produce an import, FFI target, or build-graph reference from `glp_gleam` to `glp_runtime`.
- **Alternative rejected:** dragging the 1,675-file Dart/GLP core along (engineer ruling (a), later superseded by (b)+(c)).

## R2 · Is the port functionally complete enough to deliver?

- **Decision:** YES for the pinned corpus; NOT a claim of total equivalence.
- **Instrument:** `test/parity/run_gleam_corpus.sh`, run 2026-09-02.
- **Measured:** `agree=206 diverge=0 blocked=0 gap/fork=0`, exit 0. Denominator reconciles as
  44 blocks + 161 loadcases + 1 guardcase = 206 cases carrying 238 goals. `expected.list` is **empty**,
  so no case is excused — the 100% is over the whole pinned corpus, not a filtered remnant.
  Wall clock gleam 72,751ms vs dart 41,028ms, inside the suite's 10x bound.
- **Honest limit:** 206 pinned cases ≠ the 384-test unified REPL suite.
- **Defect found:** `gleam.toml` still self-describes as "8 placeholder modules / no ported runtime
  semantics yet" at v0.1.0. STALE by an order of magnitude. Fix in Phase 1.

## R3 · AtomVM supported subset — where does the refusal live?

- **Decision:** **BUILD time**, not runtime.
- **Rationale:** FR-004 requires a loud refusal naming the construct. A runtime rejection is a silent
  degrade until the offending path executes — the workaround shape Principle II forbids.
- **Seed evidence:** the F1 dossier already excludes `gleam_otp` because its `proc_lib` use is outside
  AtomVM's BEAM/OTP subset; `gleam.toml` records that exclusion and says it was verified transitively.
- **NOT MEASURED:** the full construct list. Enumerating it is Phase 1 work; it must not be guessed.
- **Alternative rejected:** allow-list by trial — untestable, and it cannot name what it rejected.

## R4 · Headful evidence for the app ring with no host present

- **Decision:** report the host-side as **UNREAD with a named reason**; never as pass, never as zero.
- **Measured:** glpnet holds `glp_gleam/src/atomvm_gated_probe.gleam` (a gated probe) plus 45 research
  files under `docs/research/gleam-atomvm` + `docs/research/fullscope-gleam`. `maui` appears **0** times
  in glpnet — the MAUI Blazor Hybrid host is target-side and absent here.
- **Alternative rejected:** synthesizing a stand-in host to make a suite green. That manufactures a
  check that cannot fail, which is this feature's own declared defect class.
