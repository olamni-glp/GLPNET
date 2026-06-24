# Implementation Plan: glp_gleam core terms + heap + unification (F4)

**Branch**: `034-glp-gleam-core-terms-and-heap` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/034-glp-gleam-core-terms-and-heap/spec.md`

## Summary

Port the **data + binding core** of GLP from the Dart source-of-truth (`glp_runtime/lib/runtime/`
— `terms.dart`, `heap_fcp.dart`, `suspension.dart`) into the F3 `glp_gleam/` subtree's `runtime`
subsystem: the **term model** (constants, structures, lists, variable refs), the **variable store
(heap)** (FCP bidirectional writer/reader pairs, tag-determined roles, path-compressing deref,
binding, WxW detection), **writer-MGU three-valued unification** (success / suspend / fail, binding
only writers), and **heap-level suspension storage + activation-list production**. No scheduler, no
runner, no compiler, no link layer, single-runtime (multiagent excluded).

**Technical approach** (from Phase 0): the WAM mutable heap is re-expressed as an **immutable
threaded binding store** — a pure Gleam heap value threaded through `deref`/`bind`/`unify`, each
returning an updated heap (R-001; process-cells deferred to F5). Faithful to the Dart's
single-`HeapFCP`-threaded-through-the-runner model; deterministic and synchronously testable on plain
BEAM. Observable-outcome parity to Dart is pinned by a hermetic, Dart-derived gleeunit corpus (R-010).

## Technical Context

**Language/Version**: Gleam **1.17.0** (compiles to Erlang/BEAM), Erlang/OTP **25.3.2.8**, rebar3 **3.19.0**
**Primary Dependencies**: `gleam_stdlib` 1.0.3, `gleam_erlang` 1.3.0 (pinned by F3, unused by F4's pure core); dev `gleeunit` 1.11.0. **`gleam_otp` intentionally absent** (AtomVM `proc_lib` subset — F1 §3)
**Storage**: N/A — the heap is an in-memory **immutable Gleam value** (no DB, no PGLite; constitution VI-a/VI-b N/A)
**Testing**: `gleam test` (gleeunit) on Erlang/BEAM under WSL Ubuntu; additive `glp_gleam/smoke.sh` gate (separate from `test/run_all_tests.sh`)
**Target Platform**: Erlang/BEAM (test runtime). AtomVM remains viable but is **not gated** in F4 (threaded store needs no spawn — spec Assumptions line 142)
**Project Type**: library — a language-runtime kernel inside the `glp_gleam/` repo-root Gleam subtree
**Performance Goals**: deref amortized **O(1)** after path compression (SC-002); no other hard perf gate (kernel, not hot-path runner)
**Constraints**: faithful port of Dart **observable** semantics (FR-009/FR-012, no language change); pure/immutable (no mutable state, no process-cells, no `gleam_otp`); **additive-only** — zero change to existing subtrees, zero build/output artifacts committed (FR-011)
**Scale/Scope**: one `runtime` subsystem ≈ 4 Gleam modules + 1 umbrella + test modules; single-runtime; ~the surface of `terms.dart` + `heap_fcp.dart` + `suspension.dart` minus the imported-reader/MutualRef/Module/scheduler exclusions (R-008)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Constitution v1.1.0 (`.specify/memory/constitution.md`). Evaluated against this feature's spec/plan.

| Principle | Gate-ability | Verdict | Evidence |
|---|---|---|---|
| **I. Spec-First** | judgement | **PASS** | Spec identified, quoted, consistency-checked; this plan derives from it (no code-led decisions). Bug-Protocol respected (any spec gap reported, not coded around — FR-012). |
| **II. Bug-Protocol / No-Workarounds** | judgement | **PASS** | No try/catch-robustness or race workarounds; WxW/double-bind are *defined reported conditions* (R-005), not masked. |
| **III. SRSW inviolable** | machine | **PASS** | Zero `skipSRSW` tokens in spec/plan/tasks. F4 *enforces* the writer-side SRSW invariants (single-assignment, WxW-prohibited — FR-004/FR-005). |
| **IV-a. Language Authority** | judgement | **PASS** | FR-012: no new primitives/guards/types — faithful port only. |
| **IV-b. Preserve Working Internals** | judgement | **PASS** | New subtree; nothing removed. `_ClauseVar`/`_TentativeStruct` are runner internals (F5), untouched. |
| **V. Claude-Only LM / No External API** | machine | **PASS** | Zero `OPENAI_API_KEY`/`litellm`/`openai` on any path. Parity corpus is Dart-derived + hand-encoded (R-010); no LM/API in build or test. |
| **VI-a. Additive/idempotent/single-head migrations** | machine | **PASS (N/A)** | F4 has no DB migrations. |
| **VI-b. Single PGLite cluster** | judgement | **PASS (N/A)** | F4 touches no PGLite; heap is an in-process value. |
| **VII. Test-gated, commit-scoped shipping** | advisory | **PASS** | Baseline-green (F3 subtree) → change → re-test green; commit only F4 files; ship via GitFlow (feature→develop→release→main). |
| **VIII. Single source of truth & traceability** | judgement | **PASS** | Dart `glp_runtime/runtime/` is the single SoT (F1 §2.3). Traceable roadmap F4 → spec → plan → tasks. |

**Result: no violations.** Complexity Tracking below is empty (nothing to justify).

## Project Structure

### Documentation (this feature)

```text
specs/034-glp-gleam-core-terms-and-heap/
├── plan.md              # This file (/bk-plan)
├── research.md          # Phase 0 — decisions R-001..R-010 (/bk-plan)
├── data-model.md        # Phase 1 — Term/Heap/Cell/Suspension/Outcome entities (/bk-plan)
├── quickstart.md        # Phase 1 — build/test/usage walkthrough (/bk-plan)
├── contracts/
│   └── runtime-api.md   # Phase 1 — the glp/runtime public API contract (/bk-plan)
├── checklists/          # (pre-existing)
└── tasks.md             # Phase 2 — (/bk-tasks, NOT created by /bk-plan)
```

### Source Code (repository root)

F4 lands entirely inside the existing F3 `glp_gleam/` subtree (additive). No other subtree changes.

```text
glp_gleam/
├── gleam.toml                       # unchanged (deps already pinned by F3)
├── manifest.toml                    # unchanged (stdlib 1.0.3 / erlang 1.3.0 / gleeunit 1.11.0; no gleam_otp)
├── smoke.sh                         # unchanged (WSL gate: gleam build + gleam test)
├── src/glp/
│   ├── runtime.gleam                # FILLED: subsystem umbrella — re-exports public API (was F3 doc-only placeholder)
│   └── runtime/                     # NEW dir mirroring glp_runtime/lib/runtime/
│       ├── terms.gleam              # Term + Constant ADTs; nil/cons helpers      (← terms.dart)
│       ├── suspension.gleam         # SuspensionRecord, GoalRef, armed/activation (← suspension.dart)
│       ├── heap.gleam               # threaded store: CellTag, allocate/deref/bind/suspend, WxW (← heap_fcp.dart)
│       └── unify.gleam              # writer-MGU three-valued unification         (← runner HEAD-phase unify)
│   └── (analysis|bytecode|compiler|engine|link|lint|multiagent).gleam   # F3 placeholders — UNTOUCHED
└── test/
    ├── glp_gleam_test.gleam         # F3 smoke — UNTOUCHED (stays green)
    └── glp/runtime/                 # NEW test modules
        ├── terms_test.gleam         # US1 term construction/inspection/equality (SC-001)
        ├── heap_test.gleam          # US1 allocate/deref/bind/path-compression (SC-002); WxW (SC-004)
        ├── unify_test.gleam         # US2 three-valued truth table (SC-003); writer-only binding
        ├── suspension_test.gleam    # US3 suspend → bind → activation list; var-bind forwarding
        └── parity_test.gleam        # US3 Dart-derived observable-outcome corpus (SC-005)
```

**Structure Decision**: single Gleam library subtree (`glp_gleam/`), `runtime` subsystem decomposed
into `glp/runtime/{terms,suspension,heap,unify}.gleam` under a `glp/runtime.gleam` umbrella (R-004),
preserving F3's 1:1-with-Dart-subsystem rule (the Dart `runtime/` is likewise one subsystem of many
files). Tests mirror the modules under `test/glp/runtime/`. No new dependency; no existing-subtree edits.

## Complexity Tracking

> No Constitution Check violations — this table is intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase Outputs

- **Phase 0 — `research.md`**: ✅ complete. Heap mechanism (immutable threaded store), term/list
  encoding, module layout, outcome/error model, path-compression-in-immutable, suspension/activation,
  scope exclusions, toolchain, parity mechanism — all decided; 0 `NEEDS CLARIFICATION` remain.
- **Phase 1 — `data-model.md`, `contracts/runtime-api.md`, `quickstart.md`**: ✅ generated below.
- **Phase 2 — `tasks.md`**: produced by `/bk-tasks` (next), not by `/bk-plan`.
