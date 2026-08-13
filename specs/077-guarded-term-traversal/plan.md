<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Guarded term-traversal utilities (cycle-tolerant compiler walkers + PE/analyzer dedup)

**Branch**: `077-guarded-term-traversal` | **Date**: 2026-08-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/077-guarded-term-traversal/spec.md`

## Summary

Harden the GLP C# compiler back-end against cyclic `Term` graphs and eliminate a maintenance hazard, in two coupled moves mandated by the two resolved 3rtask escalations:

1. **Consolidate (dedup NOW)** the compile-time unifier/substitution/resolve machinery that is currently maintained as **two divergent copies** — `DefinedGuardEvaluator` in `analyzer.cs` and `PartialEvaluator` in `partial_evaluator.cs` — into ONE shared module. Re-verification found the copies are **not** byte-identical (5 material divergences, §Research), so this is a behaviour-sensitive merge gated by the full test suite, not a mechanical cut-paste.
2. **Guard every recursive `Term` walker** (~21 unguarded, re-verified against source) so a cyclic `Term` produces a catchable `CompileError` (FR-004, clarified) in bounded time instead of an uncatchable `StackOverflowException`. The two existing guarded walkers (`ResolveTerm` ×2, visited-set keyed on `VarTerm.Name`) are the reference; the shared consolidated module is where the guard lands once for the substitution/resolve family, and a shared traversal helper covers the structural (codegen/linker) family.

The occurs-check feature (F-069-1) is blocked-by this feature and lands its single bind-time occurs-check on the consolidated module produced here.

## Technical Context

**Language/Version**: C# (.NET) — the `out/csharp` engine/compiler tree (GlpRuntime.Compiler namespace).
**Primary Dependencies**: none new. Reuses existing `CompileError` (`error.cs:13`), `UnifyResult` union (`unify_result.cs`), `Term` AST (`ast.cs:49`).
**Storage**: N/A.
**Testing**: REPL suite `test/run_all_tests.sh` (547 tests, authoritative regression signal) + C# engine build/tests under `out/csharp`. New xUnit tests for cyclic-term inputs and the shared utility.
**Target Platform**: Windows/.NET (the C# REPL/engine); behaviour-parity is cross-runtime-neutral (compiler only).
**Project Type**: compiler back-end (single tree, `out/csharp/lib/compiler/`).
**Performance Goals**: no regression on the acyclic common path; the guard adds at most O(depth) bookkeeping (a visited-set of var-names or a fuel counter). Compiler throughput on real programs must be unchanged within noise.
**Constraints**: change confined to `out/csharp/lib/compiler/` (FR-008). No runtime/kernel/`self.glp`/language-surface edits. Behaviour-preserving on all currently-valid (acyclic) programs (FR-005). NOT a language change (FR-007).
**Scale/Scope**: ~21 walkers routed; 2 duplicated classes merged into 1 shared module; ~5 divergences resolved; new error path + tests.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Verdict | Evidence |
|---|---|---|
| **I. Spec-First** | PASS | spec.md written + clarified before this plan; plan traces to FR-001…FR-008, SC-001…SC-006. |
| **II. Bug-Protocol / No-Workarounds** | PASS | This IS the root-cause fix (shared guard + dedup), not a try/catch band-aid. The F-069-1 crash class is addressed at infrastructure level per DISCIPLINE §1.3 (fix infrastructure, not symptoms). No `StackOverflow` swallowing. |
| **III. SRSW** | N/A | C# compiler internals, not GLP clauses. No `skipSRSW`. |
| **IV-a. Language Authority** | PASS | FR-007: no new guard/predicate/directive/type-system feature; the cyclic-`Term` outcome is defined compiler behaviour replacing undefined-behaviour (a crash), not a language change. No §1.14 gate needed (contrast: sibling occurs-check feature carries the §1.14 reject-vs-accept question). |
| **IV-b. Preserve Working Internals** | PASS (with care) | The consolidation MUST preserve the divergent behaviours that are load-bearing (PE's extra `TransformClause` arms, the guarded `ResolveTerm` visited-set). The plan treats the 5 divergences explicitly (Research) rather than flattening them. No removal of working branches without test proof. |
| **V. Claude-Only LM** | PASS | No external API; no `openai`/`litellm`. |
| **VI. Persistence** | N/A | No PGLite schema change. |
| **VII. Test-Gated, Commit-Scoped** | PASS | Baseline suite green (547/547 captured this session); re-run after each change; scoped commits by filename. |
| **VIII. Single Source of Truth** | PASS | Consolidation REMOVES a duplicate (advances the principle); spec is the one authority, plan references it. |

**Result: PASS. No violations; Complexity Tracking not required.**

## Project Structure

### Documentation (this feature)

```text
specs/077-guarded-term-traversal/
├── plan.md              # This file
├── research.md          # Phase 0 — grounded walker inventory + 5 divergences + guard-strategy decision
├── data-model.md        # Phase 1 — entities: Term graph, shared traversal guard, consolidated module
├── quickstart.md        # Phase 1 — how to verify (repro cyclic term, run suites)
├── contracts/
│   └── guarded-traversal.md   # Phase 1 — the shared utility's contract (inputs, cycle-detection, CompileError)
└── tasks.md             # Phase 2 — /bk-tasks output (NOT created here)
```

### Source Code (repository root)

```text
out/csharp/lib/compiler/
├── analyzer.cs           # DefinedGuardEvaluator: 8 unguarded walkers + duplicated machinery → route + delegate to shared module
├── partial_evaluator.cs  # PartialEvaluator: 6 unguarded walkers + duplicated machinery → route + delegate to shared module
├── codegen.cs            # 6 structure/ground walkers → route through shared structural-traversal guard
├── project_linker.cs     # ResolveGoal → route through shared guard
├── error.cs              # CompileError (reuse; optionally add a phase mapping for the traversal utility)
├── unify_result.cs       # UnifyResult union (reuse; cyclic signalled via CompileError, not a new UnifyResult subtype — see research)
├── ast.cs                # Term AST (read-only; no change)
└── term_traversal.cs     # NEW — the ONE shared guarded-traversal utility + consolidated substitution/resolve/unify machinery
```

**Structure Decision**: Single new file `term_traversal.cs` (name TBD at implementation) under the existing compiler tree holds (a) the shared cycle-guard traversal helper and (b) the consolidated unify/substitution/resolve machinery. `analyzer.cs` and `partial_evaluator.cs` retain their orchestration but delegate the shared primitives to it. This keeps FR-008's "confined to `out/csharp/lib/compiler/`" and satisfies FR-003's "one shared module, no second copy".

## Phase 0 — Research

See [research.md](./research.md). Resolves: (a) the true walker inventory; (b) the 5 consolidation divergences and how each is reconciled; (c) the cycle-guard strategy (var-name visited-set for the substitution/resolve family — extending the existing `ResolveTerm` pattern; fuel/identity bound for the structural codegen/linker family); (d) how FR-004's `CompileError` is raised and with what `phase`.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — Term graph (acyclic/DAG/cyclic), the shared traversal guard (visited-set + fuel), the consolidated module surface, the cyclic-`CompileError` signal.
- [contracts/guarded-traversal.md](./contracts/guarded-traversal.md) — the shared utility's contract: what a walker passes in, when a cycle is declared, the exact `CompileError` shape, and the acyclic-parity guarantee.
- [quickstart.md](./quickstart.md) — verify: build the C# engine, run the cyclic-term repro (F-069-1 shape), confirm `CompileError` not `StackOverflow`, run REPL + engine suites for parity.

**Agent context update**: the `<!-- BUILDKIT ... -->` block in `CLAUDE.md` is refreshed to point at this plan.

**Post-design Constitution re-check: PASS** — design introduces one shared module (reduces duplication), no language surface, no new dependency, test-gated.

## Complexity Tracking

*No constitution violations — section intentionally empty.*
