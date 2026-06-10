# Marathon block 02 — `m57f4c46e:implement:2` (implement_session)

**Feature**: 027-refinement-verification-framework
**Block kind**: `implement_session` (second of the implement series)
**Scope**: Phase 2 Foundational — finalize the two methodology artifacts. **No fresh owner decision**: the four items the launch prompt flagged as owner-gated are ALREADY RATIFIED in `DECISIONS-LOG.md` (owner Gabi, 2026-06-09):
- prover choice → **R10/R11/R13** (Lean 4 primary via WSL2; proofs off the MVP critical path; Rocq = tracked alternative on full-bisimulation/coinductive seeds)
- Shapiro mandatory/advisory map → **R9** (mandatory for language/semantics/wire seeds; advisory + N/A-justification for host/infra #8/#10)
- MLIR dialect approach + primitives → **R13** (real-spike-validated; HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate)
- tactic-loop depth → **R13** (Lean attempt budget = 20, tuned start) + **R12** (binding depth-truncation = 32)

This block is therefore **doc-finalization to spec + alignment to ratified R1–R15**, not a re-decision.

## Units (tasks.md)
- **T004** — Finalize `REFINEMENT-METHOD.md` as the authoritative framework artifact (FR-001):
  - §1 loop ✔ (present), §2 pragmatic+formal metric model ✔ (present), §5 five-Shapiro map ✔ (present, matches R9), §6 seed table ✔ (present).
  - **§4 — expand from the current 3 layers to the required SIX formal-tooling slots** (FR-022, SC-004), each with name + threshold-shape + dependency-pointer. Proposed six (derivable from ratified decisions + existing content):
    1. **ANTLR4 grammar-as-verifier** (#12 Phase-A) — already present
    2. **MLIR GLP/FCP IL-dialect** (#1a/#4 → #11) — already present
    3. **Byte-parity round-trip oracle** (FR-060/061; #4/#5/#11) — already present
    4. **Lean 4 prover** (Lean-LSP-MCP/APOLLO/Lean Copilot; R10/R11/R13) — **add as explicit slot**
    5. **SMT (Z3/CVC5)** discriminant-uniqueness / finite-domain exhaustiveness (#4, #8) — **add as explicit slot**
    6. **Promela/SPIN + protocol-verification armoury** (R14/R15: TLA+/UPPAAL/nuXMV/mCRL2/FDR4/CADP) — **add as explicit slot**
  - Fold in **R13/R14/R15** (they post-date the current doc): the real-tool-spike mandate, SPIN as the required wire-protocol default, and the armoury.
  - Confirm the no-API rule (§1 ✔) + the budget-cap discipline (Lean budget 20; capped run → best-so-far).
- **T005** — Finalize `DECISIONS-FOR-OWNER.md` (FR-002): cross-link the ratified `DECISIONS-LOG.md` (R1–R15) against the relevant advisories (prover choice §3, Shapiro §5/U-M2, MLIR primitives §4, tactic-loop depth U-M3/R12), marking each accepted advisory as ratified. The doc is the synthesis Gabi reads; finalization = make its advisories consistent with the binding log.

## Out of scope (later blocks)
- T006–T008 (US1 metric-combination template + worked example) — block 3.
- T009–T011, T016, T021 (US2 loop seam + no-API grep + the three doc artifacts) — later blocks.
- T012/T017/T022 + downstream (real-tool installs) — HARD STOP, await Gabi's provisioning go.

## Boundary
Checkpoint == commit/push boundary. On completion: final checkpoint, then commit + push ONLY this block's files (`REFINEMENT-METHOD.md`, `DECISIONS-FOR-OWNER.md`, this plan, tasks.md) under the standing grant.

## Gate note
Awaiting Gabi's block approval. If he wants to amend any R-row or the proposed six-slot set, that is recorded as a `change` at this gate before execution.
