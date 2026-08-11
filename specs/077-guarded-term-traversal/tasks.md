<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Tasks: Guarded term-traversal utilities (cycle-tolerant compiler walkers + PE/analyzer dedup)

**Feature**: `077-guarded-term-traversal` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Design inputs**: research.md (walker inventory + 5 divergences + guard strategy), data-model.md, contracts/guarded-traversal.md, quickstart.md

## Ordering note (why US3 is foundational, before US1/US2)

The two resolved escalations mandate **dedup NOW, foundational, first**: the cycle guard (US1/US2) lands **on** the consolidated substitution/resolve module (US3). So US3's consolidation is the Foundational phase; US1+US2 build the guard on top of it. All three stories are P1 and tightly coupled — this is the correct build order, not a priority downgrade of US3.

All source paths are under `out/csharp/lib/compiler/` unless noted. The REPL suite (`test/run_all_tests.sh`, 547/547 baseline) + `out/csharp` `dotnet build`/`dotnet test` are the parity gate after every phase (FR-005, SC-005).

---

## Phase 1: Setup

- [ ] T001 Capture the green baseline: run `DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe bash test/run_all_tests.sh` (expect 547/547) and `cd out/csharp && dotnet build && dotnet test` (0 errors, green); record counts in the commit message (DISCIPLINE §2.2).
- [ ] T002 [P] Record the pre-fix crash: add a disabled/skipped xUnit test in `out/csharp` that documents the F-069-1 repro (`p(X,s(X))` called `p(Y,Y)` → self-referential subst) and its current `StackOverflowException`, referencing feature 069 SC-003; keep skipped until the guard exists (SC-002 baseline).

## Phase 2: Foundational — US3 consolidation (dedup NOW) — creates the shared module

**Goal**: One shared module holds the unify/substitution/resolve primitives; `analyzer.cs` and `partial_evaluator.cs` delegate to it; no second copy remains (SC-004). Behaviour-preserving (FR-005) — the 5 divergences (research.md Decision 3) are preserved, not flattened.

**Independent test**: after this phase the full REPL + engine suites are byte-identical to baseline, and the duplicated methods exist in exactly one place.

- [ ] T003 [US3] Create the shared module file `out/csharp/lib/compiler/term_traversal.cs` (namespace `GlpRuntime.Compiler`) with a static/`internal` class hosting the shared primitives; no logic yet, just the skeleton + file-id header.
- [ ] T004 [US3] Move the identical primitives into the shared module (one copy each): `GlpUnifyForPE`, `SubstSet`, `CheckCompatible`, `ResolveTerm` (keep its `HashSet<string> visited`), `ApplySubstitution`, `ApplySubstitutionToAtom/Guard/Goal`, `CollectVarNames`, `ApplyRenaming`. Source of truth = the two identical copies (analyzer `_`-prefixed 1290–1377, PE 636–712).
- [ ] T005 [US3] Parameterise divergence #1/#3 (anonymous-var semantics): add a `Func<Term,bool> isAnonymous` parameter to `UnifyTerms` and the renaming filter; analyzer callers pass `StartsWith('_')` semantics (`_IsAnonymous`, 1283), PE callers pass `== "_"` semantics (`IsUnderscore`, 628). Do NOT pick a winner — preserve both (research.md; §II no-silent-behaviour-change).
- [ ] T006 [US3] Parameterise divergence #2 (fresh-var prefix): `RenameUnitClauseVars` takes a `string freshVarPrefix` (analyzer `"PE_"`, PE `"PE"`); unify the counter to `long`.
- [ ] T007 [US3] Normalise divergence #5: shared `ResolveSubstitution` returns `IReadOnlyDictionary<string,Term>`; give PE-internal mutating callers a local copy.
- [ ] T008 [US3] Move the shared `UnifyTerms` into the module (with the `isAnonymous` param from T005); leave divergence #4 (`TransformClause`/`TransformDefinedGuards` orchestration + PE's `BuiltinProcedures`/`allProcedures` arms) IN their owning classes — only the primitives move.
- [ ] T009 [US3] Repoint `analyzer.cs` `DefinedGuardEvaluator` to call the shared module (delete its `_`-prefixed copies 1066–1377 that moved); keep analyzer-only orchestration (`_TransformClause`, `_CollectUnitClauses`).
- [ ] T010 [US3] Repoint `partial_evaluator.cs` `PartialEvaluator` to call the shared module (delete its moved copies 383–712); keep PE-only Stage-2 (`UnfoldReduceCalls`, `RenameClauseVars`, `SimplifyGuards`, etc.) and the divergence-#4 orchestration.
- [ ] T011 [US3] Parity gate: rebuild `out/csharp` (0 errors) and run the full REPL suite — MUST be 547/547 identical to T001 baseline. Any diff ⇒ STOP (a divergence was flattened; the §II trap — fix before proceeding). Commit the consolidation.

## Phase 3: US1+US2 — cycle guard on the substitution/resolve family (var-name keyed)

**Goal**: The consolidated substitution/resolve walkers detect substitution-closure cycles (var-name keyed, the F-069-1 path) and hard-fail with `CompileError` (FR-004). Delivers SC-001/SC-002 for this family and part of SC-003.

**Independent test**: the F-069-1 repro compiles to a caught `CompileError`, not a `StackOverflowException`; acyclic parity holds.

- [ ] T012 [US1] In `term_traversal.cs`, add the shared var-name cycle guard for the substitution/resolve family: generalise the `ResolveTerm` `visited` mechanism into one guard entry point used by `ApplySubstitution`, `UnifyTerms`, `ResolveTerm`, `ApplyRenaming`, `CollectVarNames` (contracts C1). One implementation only (SC-003).
- [ ] T013 [US1] On a declared unbreakable cycle, raise `new CompileError(message, line, column, phase: "analyzer")` naming the offending variable (contracts C3; error.cs:13). Reconcile with `ResolveTerm`'s existing inner return-revisited-node short-circuit: inner benign self-ref may still terminate the closure; an unresolvable structural cycle surfaces as `CompileError` (research.md Open item 2).
- [ ] T014 [P] [US2] Route analyzer's remaining **substitution-closure** walkers through the shared var-name guard: `_ApplySubstitutionToGoal` (1377). *(Analyze F1: the mark/ground walkers `_ExtractAndMarkGroundedVars` (781), `_MarkVarsInTermAsTypeGrounded` (800), `_AnalyzeTerm` (823) walk AST structure and do NOT follow the substitution map — they are routed through the structural guard in Phase 4 (T019a), not here.)*
- [ ] T015 [P] [US2] Route PE's remaining substitution-closure walkers through the shared var-name guard: `ApplySubstitutionToGoal` (712). *(`IsGround` (770) walks AST structure → structural guard, T019a.)*
- [ ] T016 [US1] xUnit tests: feed a cyclic `Term` (F-069-1 shape) to each substitution/resolve walker → assert `CompileError`, bounded time, no `StackOverflowException` (SC-001). Un-skip the T002 repro; assert it now compiles to a diagnostic (SC-002).
- [ ] T017 [US1] Parity gate: rebuild + full REPL suite = 547/547; commit.

## Phase 4: US1+US2 — cycle guard on the structural family (fuel / identity keyed)

**Goal**: The codegen + linker structural walkers bound traversal of a programmatically-cyclic AST node (no var-name to key on) via fuel/identity and hard-fail with `CompileError`. Completes SC-001 coverage and SC-003.

**Independent test**: a constructed cyclic `Term` fed to each codegen/linker walker → `CompileError`; a deep acyclic + DAG term traverse OK (SC-006).

- [ ] T018 [US1] In `term_traversal.cs`, add the shared structural guard (fuel bound and/or `HashSet<Term>` under `ReferenceEqualityComparer.Instance`) — contracts C2. One implementation (SC-003). Fuel sizing basis (Analyze A1): derive the bound from the maximum legitimate term depth observed across the REPL corpus with a safety multiplier, so a genuine deep acyclic term (FR-006) never trips it while a self-referential node is caught quickly.
- [ ] T019 [P] [US2] Route the six `codegen.cs` walkers through the structural guard: `_GenerateStructureElement` (369), `_IsGroundTerm` (670), `_GroundTermToValue` (687), `_GenerateArgumentStructureElement` (710), `_GenerateStructureElementInBody` (792), `_GenerateListTailInBody` (849); raise `CompileError(phase:"codegen")` on cycle.
- [ ] T019a [P] [US2] Route the AST-structure walkers (reassigned from Phase 3 per Analyze F1) through the structural guard: analyzer `_ExtractAndMarkGroundedVars` (781), `_MarkVarsInTermAsTypeGrounded` (800), `_AnalyzeTerm` (823); PE `IsGround` (770). These walk `Term` structure, not the substitution map, so they take the fuel/identity guard.
- [ ] T020 [P] [US2] Route `project_linker.cs` `ResolveGoal` (378) through the structural guard (Goal-graph, identity-preserving); raise `CompileError` on cycle (note: linker currently throws plain `Exception` — use `CompileError` for the cyclic signal per contracts C3).
- [ ] T021 [US1] xUnit tests: constructed cyclic `Term` → each codegen/linker walker → `CompileError`, bounded, no overflow (SC-001). Deep-acyclic + DAG-shared term → both traverse OK, NOT falsely rejected (SC-006).
- [ ] T022 [US1] Parity gate: rebuild + full REPL suite = 547/547; commit.

## Phase 5: Polish & Cross-Cutting

- [ ] T023 [P] Optionally add a `"term_traversal"` phase mapping in `CategoryFromPhase` (`error.cs:33`) if the cyclic errors warrant their own category (mapping addition, not a language change — FR-007).
- [ ] T024 [P] Verify SC-002 end-to-end: run the feature-069 SC-003 fuzz corpus with cyclic-`=` inputs directly (no non-cyclic-scoping workaround); confirm diagnostics, no crash.
- [ ] T025 Final parity + inventory audit: confirm exactly ONE guard impl per family and ONE shared primitives module (grep for residual duplicated method names in analyzer/PE); full REPL 547/547 + `dotnet test` green (SC-003/004/005). Update spec Status → Implemented.
- [ ] T026 [P] Update `docs/known-issues.md` if the F-069-1 entry exists there; cross-reference the sibling occurs-check feature (which now lands its single change on this consolidated module).

---

## Dependencies & build order

- **Phase 1 (Setup)** → **Phase 2 (US3 dedup, FOUNDATIONAL)** → **Phase 3 (US1/US2 subst-family guard)** → **Phase 4 (US1/US2 structural guard)** → **Phase 5 (Polish)**.
- US3 (Phase 2) blocks everything: the guard lands on the module it creates.
- Phase 3 and Phase 4 are independent of each other (different walker families) and could run in parallel *after* Phase 2, but each ends with a parity gate; sequential is safer given shared-file edits.
- Within a phase, `[P]` tasks touch different methods/files and may parallelise.

## Parallel opportunities

- T014 / T015 (analyzer vs PE remaining subst walkers) — different files.
- T019 / T020 (codegen vs linker structural walkers) — different files.
- T023 / T026 (error.cs mapping vs docs) — independent.

## Implementation strategy (MVP first)

- **MVP = Phase 1 + Phase 2 + Phase 3**: the dedup (SC-004) plus the substitution-family cycle guard closes the actual F-069-1 crash path (SC-002) and unblocks the sibling occurs-check feature. Phase 4 completes the defense-in-depth invariant across the structural walkers; Phase 5 polishes.
- Every phase ends green (547/547 + dotnet test) and is committed — safe restart points throughout.

## Task count

27 tasks — Setup 2, US3/Foundational 9, US1+US2 subst-family 6, US1+US2 structural 6 (incl. T019a reassigned per Analyze F1), Polish 4. Test tasks included (SC-001/002/005/006 demand them).
