# Phase 0 Research — Wave 4 consolidated parallel-safe fillers

Each decision: **Decision / Rationale / Alternatives**. Items marked ⛔ carry an external-source
dependency that must be satisfied before that slice's implementation (no guessing — CLAUDE.md
"never program based on ignorance of GLP"; DISCIPLINE §1.13 FCP authority).

## R-1 — Depgraph mark-and-recompute (US1)

- **Decision**: Add a `mark-and-recompute` subcommand to the existing codeconv depgraph tool that
  accepts a set of file paths, marks them + their transitive dependents dirty, and recomputes only
  that subgraph, preserving unmarked node/edge results in the catalog.
- **Rationale**: Reuses the existing depgraph computation and the unified PGLite bridge; additive
  rows only (Constitution VI-a). Matches the roadmap item's "convenience subcommand" intent.
- **Alternatives**: Full recompute every run (rejected — defeats the convenience/perf goal);
  separate tool (rejected — infrastructure duplication, DISCIPLINE §1.3).

## R-2 — Cross-run trend reporting (US1)

- **Decision**: Add a `trends` view that reads ≥2 recorded runs and emits a deterministic,
  secret-redacted per-metric delta report; byte-identical on unchanged inputs (timestamp only in
  filename, mirroring roadmap `export`).
- **Rationale**: Consistent with existing deterministic-export discipline; trivially testable.
- **Alternatives**: Live dashboard (rejected — out of scope, adds a service); single-run snapshot
  (rejected — not a trend).

## R-3 — US3 target runtime for engine/transport/compiled-IL ⛔(scoping)

- **Decision**: Target the **C#/.NET distributed engine line** for `multi-accept-transport-extension`
  and `compiled-il-on-the-wire + factor-out-compiler`, since these roadmap items live in the
  "separation-of-repl-front-end-from-engine" epic whose transport/engine work is C#-centric; the
  Gleam link layer is a separate spine (full-gleam epic) and is out of this wave.
- **Rationale**: Keeps the wave's engine work on one runtime line; avoids entangling the Gleam spine.
- **Alternatives**: Dart glp_runtime (rejected — its transport is REPL-local, not the distributed
  line); Gleam (rejected — separate spine, wave-boundary). **Confirm with operator at analyze if
  the intended engine line differs.**

## R-4 — ZMQ base comm primitives (US3)

- **Decision**: Implement `zmq-receiver-base` + `zmq-sender-base` as thin transport primitives on
  the same engine line as R-3, behind the existing transport seam, with a round-trip test.
- **Rationale**: Roadmap groups them as "base comm primitives"; smallest viable primitive pair.
- **Alternatives**: Full pub/sub mesh (rejected — beyond "base"); reuse QUIC/WS only (rejected —
  the item explicitly asks for a ZMQ base).

## R-5 — §1.14 language semantics (US5) ⛔EXTERNAL SOURCE REQUIRED

- **Decision**: Author a written §1.14 proposal for **abandon-operation** (FCP-exact) and for
  **nested-structure-head-matching** BEFORE implementing either. Semantics are drawn from the
  authoritative sources, NOT invented:
  - `abandon-operation`: **FCP source** (`kernels.c`, `emulate.c`) — the DISCIPLINE §1.13 FCP
    reference architecture. FCP source lives in the **sibling GLP repo on Mac/Linux**, not on this
    Windows host. This is a hard prerequisite: the exact abandon semantics + reader/writer cell
    behaviour must be read from FCP before the proposal is finalized.
  - `nested-structure-head-matching`: HEAD-phase matching of nested structures in the Dart runner
    (`_TentativeStruct` / `_ClauseVar` — must be extended, never removed per IV-b). The exact
    intended semantics come from the typed-GLP manual + the sibling GLP runtime spec.
- **Rationale**: IV-a (Language Authority) + the "never program based on ignorance of GLP" rule
  forbid guessing language semantics. Operator approval to implement is recorded (2026-07-29); the
  proposal documents the sourced semantics and references that approval.
- **Open dependency**: FCP / sibling-repo access from this host, OR the operator/peer supplying the
  relevant FCP excerpts. Flagged on the scheduler board; does not block US1–US4.
- **Alternatives**: Guess semantics from behaviour (rejected — violates IV-a and DISCIPLINE §1.10
  "code is never the source of truth"); defer US5 (rejected — operator directed implement).

## R-6 — Feasibility study format (US2)

- **Decision**: Each of the three feasibility items (research-programme/LLVM, C++ engine,
  many-instances shared-static-memory scheduling) is delivered as a written ADR-style study under
  `specs/062-.../research/`: question, options considered, recommendation (go/no-go), staged plan
  (if go), risks.
- **Rationale**: These are feasibility questions; a decision-ready document is the correct artifact
  (spec Assumptions). No runtime risk.
- **Alternatives**: Prototype builds (rejected — that is a follow-on feature, not a "filler" study).

## Summary of external dependencies

| Item | Dependency | Blocks |
|---|---|---|
| R-5 abandon-operation | FCP source (sibling repo, off-host) | US5 abandon proposal + impl |
| R-5 nested-struct-head | sibling GLP runtime spec / typed-GLP manual | US5 nested-struct proposal + impl |
| R-3 engine line | operator confirm engine line (C# assumed) | US3 impl start |

All NEEDS CLARIFICATION from the spec are resolved (clarify session) or converted to the tracked
external dependencies above; no unresolved in-spec clarification remains.
