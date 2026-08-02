<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Wave 4 consolidated — parallel-safe fillers

**Branch**: `062-wave-4-consolidated-parallel-safe-fillers` | **Date**: 2026-07-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/062-wave-4-consolidated-parallel-safe-fillers/spec.md`

## Summary

Wave 4 clears eleven parallel-safe roadmap items as separable slices under one branch/marathon.
The slices span four runtimes and two research deliverables, plus two operator-approved GLP
language changes. Technical approach: deliver the lowest-risk, self-contained slices first
(depgraph tooling in Python; the GLP control program), the three feasibility questions as written
studies, the engine/transport slice as a hardened runtime capability, and the two §1.14 language
items each behind a written proposal sourced from FCP / the sibling GLP repo (authoritative) with
positive + negative REPL regression tests. Every slice is baseline-gated: no regression ships.

## Technical Context

**Language/Version**: Python 3.13 (codeconv depgraph, US1); GLP via the REPL pipeline (US4, US5
surface); Dart (glp_runtime — US5 §1.14 runtime implementation, US3 engine seams as applicable);
C#/.NET and/or Gleam for the distributed engine/transport line (US3) — see research R-3.
**Primary Dependencies**: existing codeconv catalog + PGLite bridge (US1); existing shipped
link/transport surface from prior waves (US3, US4); FCP source + sibling GLP repo as the
semantic authority for US5 (off-host — see R-5); ZMQ binding for `zmq-comm-base` (US3, R-4).
**Storage**: additive `roadmap_*`/codeconv catalog rows only (US1); no new schema head beyond
additive migrations (Constitution VI-a). N/A for research slices.
**Testing**: REPL suite `test/run_all_tests.sh` (GLP — US4, US5); codeconv pytest (US1); Dart
unit tests (US5 runtime); C#/Gleam suites where US3 touches them. Positive + negative controls
mandatory (DISCIPLINE §2.4).
**Target Platform**: Windows host (glpnet) primary; cross-runtime items validated on their
native suites.
**Project Type**: multi-component monorepo (compiler/runtime + Python tooling + GLP programs).
**Performance Goals**: no regression of existing baselines; US3 multi-accept serves ≥2 concurrent
clients with zero drops; depgraph recompute touches only the marked subgraph.
**Constraints**: SRSW inviolable (III); no core-internal removals (IV-b: `_ClauseVar`,
`_TentativeStruct`, fallbacks); §1.14 change discipline (IV-a); Claude-only LM (V); additive
persistence (VI-a/b); test-gated GitFlow shipping (VII).
**Scale/Scope**: 11 consolidated items; effort "large"; delivered as independent slices, wave
closes when each item is terminal (delivered / delivered-as-study).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Relevance & disposition | Status |
|---|---|---|
| I. Spec-First | spec.md + clarifications precede this plan; all code traces to an FR. | PASS |
| II. Bug-Protocol / No-Workarounds | Any baseline regression or semantic snag → stop-and-report, not workaround (spec Edge Cases, FR-009). | PASS |
| III. SRSW inviolable | US4/US5 GLP code type-checks via REPL; no `skipSRSW`. Enforced by the REPL pipeline. | PASS (verify at implement) |
| **IV-a. Language Authority** | US5 changes the language. Operator approval **recorded 2026-07-29** (clarify session); each item gets a written §1.14 proposal before implementation. | PASS (approval on record) |
| IV-b. Preserve Working Internals | US3 (compiler factor-out) and US5 (HEAD-phase matching) touch core runtime; no removal of `_ClauseVar`/`_TentativeStruct`/fallbacks without explicit approval. | PASS (constraint recorded) |
| V. Claude-Only LM | No external LM/API introduced by any slice. | PASS |
| VI-a. Additive/idempotent/single-head persistence | US1 depgraph uses additive catalog rows only; no new Alembic head unless additive+single-head. | PASS |
| VI-b. Single OS-lock PGLite cluster | US1 reuses the unified bridge; no second cluster. | PASS |
| VII. Test-gated, commit-scoped shipping | Baseline-before/after per slice; GitFlow ship; release coordinated through fleet lead. | PASS |
| VIII. Single source of truth & traceability | Each slice references its roadmap item + FR; no duplicated specs. | PASS |

No violations requiring Complexity Tracking. One **judgement gate carried forward**: IV-a is
satisfied by the recorded approval, but each §1.14 proposal is re-checked at `/bk-analyze` and a
semantic problem in a proposal is a stop-and-report (spec Edge Cases).

## Project Structure

### Documentation (this feature)

```text
specs/062-wave-4-consolidated-parallel-safe-fillers/
├── plan.md              # This file
├── research.md          # Phase 0 — R-1..R-6 decisions + external-source dependencies
├── data-model.md        # Phase 1 — entities (depgraph run, feasibility study, §1.14 proposal, IL envelope)
├── quickstart.md        # Phase 1 — how to validate each slice
├── contracts/           # Phase 1 — depgraph CLI, compiled-IL wire envelope, §1.14 proposal template
└── tasks.md             # Phase 2 — /bk-tasks (not created here)
```

### Source Code (repository root)

```text
codeconv/src/codeconv/tools/depgraph/        # US1 — mark-and-recompute + cross-run trends
codeconv/tests/                              # US1 — pytest fixtures + trend determinism tests
specs/062-.../research/                       # US2 — three feasibility studies (studies/ADRs)
glp_runtime/lib/{compiler,bytecode,runtime}/  # US3 (compiler factor-out), US5 (HEAD-phase matching, abandon op)
glp_runtime/test/                            # US5 — Dart unit tests
programs/tests/typed/                         # US4, US5 — GLP regression programs
test/run_all_tests.sh                         # US4, US5 — REPL regression cases (Sections A/B/C)
<engine/transport line>                       # US3 — C#/Gleam per R-3 (compiled-IL wire, multi-accept, zmq base)
specs/062-.../proposals/                       # US5 — written §1.14 proposals (abandon-op, nested-struct-head)
```

**Structure Decision**: No new top-level project. Each slice lands in its existing home
(codeconv for tooling, glp_runtime for GLP-language/runtime, programs/ + test/ for GLP programs,
the engine/transport line for US3). Feature-local `research/` and `proposals/` hold the study and
§1.14 artifacts.

## Complexity Tracking

> No Constitution violations. Table intentionally empty.

The wave is intrinsically large (11 items). This is operator-directed consolidation, not
accidental scope; each item stays an independently reviewable/shippable slice (spec FR-010) so
size is managed by slicing, not by relaxing any gate.
