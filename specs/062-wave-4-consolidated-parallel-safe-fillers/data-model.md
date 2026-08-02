# Phase 1 Data Model — Wave 4 consolidated parallel-safe fillers

Entities are per-slice; the wave introduces no shared schema head beyond additive rows.

## Depgraph run (US1) — existing, extended

- **Fields**: run_id, project_ref, computed_at, nodes[], edges[], per-node metrics.
- **Extension**: a per-node `dirty` mark used by mark-and-recompute; recompute updates only
  dirty nodes + transitive dependents. Additive column/row only (Constitution VI-a).
- **Rules**: unknown marked paths are reported, not fabricated (spec Edge Cases).

## Depgraph trend report (US1) — new, derived

- **Fields**: source run_ids (≥2), per-metric deltas, generated_from digest.
- **Rules**: deterministic + secret-redacted; byte-identical on unchanged inputs; <2 runs → refuse
  with "at least two runs required" (spec Edge Cases). Derived artifact, not persisted as truth.

## Feasibility study (US2) — new document

- **Fields**: question, options[], recommendation (go|no-go), staged_plan?, risks[].
- **Instances**: research-programme+LLVM; C++ engine+scheduler+compiler; many-instances
  shared-static-memory cooperative scheduling.
- **Rules**: a no-go is a complete deliverable; each is independently signed off.

## §1.14 language proposal (US5) — new document

- **Fields**: item (abandon-operation | nested-structure-head-matching), motivation, exact
  semantics, authoritative source ref (FCP file/section or sibling-GLP spec ref), type-system
  impact, SRSW/mode implications, test plan (positive + negative), approval_ref (2026-07-29).
- **State**: sourced → drafted → operator-approved(recorded) → implemented.
- **Rules**: semantics MUST cite the authoritative source (R-5); never invented. A semantic snag
  after drafting → stop-and-report (IV-a / Bug Protocol).

## Compiled-IL wire envelope (US3) — new

- **Fields**: il_version, compiled_form (opaque bytes), integrity digest, source metadata.
- **Rules**: receiver validates il_version compatibility + digest before execution; malformed or
  version-mismatch → safe reject with diagnostic, no engine-state mutation (FR-005a hardening);
  remote execution result == local execution (FR-005).

## ZMQ message (US3) — new, minimal

- **Fields**: payload bytes, endpoint address.
- **Rules**: sender→receiver round-trip delivered and covered by a test (FR-006).

## GLP multi-client control program (US4) — new program

- **Entities**: client identifiers, per-client channels (`Channel(In, Out)`), coordination state.
- **Rules**: type-checks + SRSW-valid via the REPL pipeline; runs to a documented
  succeeded/suspended outcome; added as a REPL regression case.
