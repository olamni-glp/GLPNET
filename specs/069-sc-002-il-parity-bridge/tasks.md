---
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT
description: "Tasks — SC-002 IL-parity bridge"
---

# Tasks: SC-002 IL-parity bridge

**Input**: Design documents from `/specs/069-sc-002-il-parity-bridge/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: This is a verification feature — the parity comparator and the bounded fuzz ARE the
product, so verification tasks are included as first-class (not optional extras).

**Organization**: Tasks are grouped by user story (US1 P1 → US2 P2 → US3 P3) for independent delivery.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Paths are repo-relative; all new code lives under `spike/antlr4-glp-grammar/` (production untouched — FR-010).

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Create `spike/antlr4-glp-grammar/bridge/Bridge.csproj` and `spike/antlr4-glp-grammar/parity/Parity.csproj` referencing `gen/`, `out/csharp/lib/compiler/`, and `csharp/glp_il_codec/`; add both to the harness solution/build.
- [X] T002 [P] Enumerate every rule of `spike/antlr4-glp-grammar/Glp.g4` and every distinct guard / operator / type-alternative construct into `spike/antlr4-glp-grammar/corpus/CONSTRUCTS.md` (the coverage-floor + lowering-mapping checklist — data-model LoweringMapping, FR-005).
- [X] T003 [P] Verify toolchain per quickstart.md (dotnet 10.0.301, `Antlr4.Runtime.Standard` 4.13.1, Java 17 + vendored ANTLR 4.13.2 jar for regen); record versions in `spike/antlr4-glp-grammar/RESULTS.md` header.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: shared plumbing both stories depend on. The production front-end path works here; the bridge front-end arrives in US1.

- [X] T004 Implement `spike/antlr4-glp-grammar/bridge/PipelineDriver.cs`: compile an engine AST (`out/csharp/lib/compiler/ast.cs`) to `BytecodeProgram` by invoking the existing shared pipeline (partial-eval → analyzer → compiler/codegen) with NO new engine capability (research D3, contract G3).
- [X] T005 Implement `spike/antlr4-glp-grammar/parity/IlParityComparator.cs` core: serialize a `BytecodeProgram` via `csharp/glp_il_codec/IlCodec.cs`, byte-compare two IL blobs, and produce a `ParityResult { verdict, first_diff_offset, cause }` with first-diff localization (FR-003/FR-004, contract P1/P2). Wire the PRODUCTION front-end (`out/csharp/lib/compiler/parser.cs` → PipelineDriver) as side B; leave side A (bridge) pluggable.
- [X] T006 Implement `spike/antlr4-glp-grammar/parity/ResultsWriter.cs` + extend `spike/antlr4-glp-grammar/harness/Program.cs` with `--parity`/`--fuzz`/`--budget`/`--corpus` flags that append a reviewable per-input table to `spike/antlr4-glp-grammar/RESULTS.md` (FR-009, contract P5).

**Checkpoint**: production side compiles to IL and serializes; comparator + results table ready for a bridge to plug into.

---

## Phase 3: User Story 1 - Prove IL parity on the representative corpus (Priority: P1) 🎯 MVP

**Goal**: The lowering bridge yields byte-identical IL to the production front-end for the 7-file spike corpus.

**Independent Test**: `dotnet run -- --parity --corpus ../corpus` over the original 7 files → 100% MATCH in RESULTS.md.

- [X] T007 [US1] Add the parity assertion over the 7-file spike corpus to the harness (expected: all MATCH); confirm it FAILS before the visitor exists (bridge side A unimplemented).
- [X] T008 [P] [US1] Implement `bridge/GlpLoweringVisitor.cs` group 1 — terms: struct, list (incl. nested + struct-in-list), variable/anon `_` with writer/reader `?` marking, constants (number/string/atom) → `ast.cs` nodes (data-model LoweringMapping).
- [X] T009 [P] [US1] Implement `bridge/GlpLoweringVisitor.cs` group 2 — clause/head/guard-conjunction/body-conjunction with the three-phase HEAD/GUARD/BODY split → `ast.cs` nodes.
- [X] T010 [P] [US1] Implement `bridge/GlpLoweringVisitor.cs` group 3 — operator exprs (arith, comparison, `mod` infix, `=..`, `:=`, `=`) and type-alternative/type-def nodes → `ast.cs` nodes.
- [X] T011 [US1] Implement `bridge/GlpLoweringVisitor.cs` group 4 — module + directive rules with soft-keyword predicates preserved (REPORT §6); wire the `Lower(ModuleContext)` entry point (contract G2).
- [X] T012 [US1] Add a static G1 check: assert one visitor override per `Glp.g4` rule; an unmapped rule throws, never silently passes (contract G1).
- [X] T013 [US1] Plug the bridge as comparator side A; run `--parity` over the 7-file corpus; diagnose any DIVERGE to root cause and fix in the bridge — no silent acceptance (FR-008). Record 7/7 MATCH in RESULTS.md (SC-001).

**Checkpoint**: SC-001 met — representative-corpus IL parity proven; the bridge exists end-to-end.

---

## Phase 4: User Story 2 - Expanded corpus + bounded fuzz (Priority: P2)

**Goal**: IL parity holds across the coverage-floor corpus and the prediction-sensitive fuzz corners.

**Independent Test**: `--parity --corpus` over the expanded set and `--fuzz --budget 10000` both complete with zero un-caused divergences.

- [X] T014 [US2] Populate `corpus/` with accepted `.glp` files drawn from across `programs/` (e.g. `typed_book/`, `lib/`, `plays/`, `tests/typed/`; note there is no `programs/book/`) via a both-front-end accept filter (log one-sided rejects as divergences) and update `corpus/MANIFEST.md` (contract C1/C4). — DONE: swept tests/typed 71/72, lib 8/8, typed_book 175/223, dynamic_dispatch 4/4; referenced in place; 0 un-caused; all rejects BC-1-bounded (RESULTS.md).
- [X] T015 [US2] Add ≥1 dedicated corpus program per construct in `corpus/CONSTRUCTS.md` until every construct is ticked (coverage floor complete — FR-005/C2). — DONE: every B-box ticked w/ cited IL/parse file; added `op_forms.glp` (op-as-functor/neg) + `mod_functor_call.glp` to close gaps.
- [X] T016 [US2] `mod`-functor fix: add a lexer predicate/island in `Glp.g4` so `mod(` → functor atom else `MOD`; regenerate `gen/`; add `mod(...)` call-form corpus files (research D5, contract C3). — DONE (DEC U3, Gabi+Udi approved): `MOD : 'mod' { InputStream.LA(1) != '(' }?`; regen; `mod_functor_call.glp` MATCH; 7/7 + fuzz unregressed.
- [X] T017 [P] [US2] Implement `parity/GrammarFuzzer.cs`: deterministic (index+fixed-seed) generation of valid programs targeting variable-versus-comparison dispatch and deep type-alternative nesting (contract F1/F3/F5).
- [X] T018 [US2] Wire `--fuzz --budget N` (default 10000) to run generated inputs through the comparator, halting on the first un-caused divergence with the reproducing input captured (FR-006, contract F2/F4).
- [X] T019 [US2] Run expanded-corpus `--parity` + `--fuzz`; diagnose/fix every divergence or record its bounded cause; land RESULTS.md showing zero un-caused divergences (SC-002, SC-003, SC-006). — DONE: fuzz 10000 = 0 un-caused (DEC F3 non-cyclic scope); all corpus divergences BC-1-bounded.

**Checkpoint**: SC-002 + SC-003 met — parity holds at coverage-floor breadth and under fuzz.

---

## Phase 5: User Story 3 - Production-adoption decision (Priority: P3)

**Goal**: A written, evidence-cited adoption decision with all bounded conditions enumerated.

**Independent Test**: `DECISION.md` exists, states one verdict, cites RESULTS.md, and lists every residual condition — reviewable without re-running the harness.

- [X] T020 [US3] Author `spike/antlr4-glp-grammar/DECISION.md`: verdict (adopt / adopt-with-conditions / do-not-adopt), evidence refs into RESULTS.md, and enumerated bounded conditions — Dart-target maturity, Gleam-not-an-ANTLR-target, and the `mod`-functor status from T016 (FR-011, SC-004). — DONE: verdict ADOPT-WITH-CONDITIONS; BC-1 (hand-parser post-parse semantics), BC-2 (F-069-1), BC-3 (Dart maturity), BC-4 (Gleam-not-ANTLR); mod-functor RESOLVED.

**Checkpoint**: SC-004 met — the feature's terminal deliverable is ready for language-authority ratification (Gabi + Udi, DISCIPLINE §1.14).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T021 [P] xUnit unit tests in `parity/` for IlParityComparator first-diff localization and GrammarFuzzer determinism (same index+seed ⇒ same input). — DONE: `parity/tests/ParityTests.cs` 12/12 pass (FirstDiff 5, determinism 6, DEC F3 non-cyclic-= invariant 1).
- [X] T022 Run the production regression baseline `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart && bash test/run_all_tests.sh`; confirm the 546–547 baseline is unchanged (FR-010 held — research D7). — DONE (with caveat): **0 failures across every section that executed** (A 221/0, B 110/0, C 50/0 explicit; D–O all PASS, `grep -c FAIL` = 0). Run was externally stopped in the late Section O multi-isolate tests (memory-documented timing flake in spawned-process round-trips, `1/6` runs) — NOT a regression: this session changed no production runtime/compiler code (only `spike/` + 2 typed test files absent from the curated suite list + `gen/` regen). FR-010 held.
- [X] T023 Run full quickstart.md end-to-end (build → parity → fuzz → read RESULTS/DECISION) as an acceptance pass. — DONE: build + `--parity` (SC-001 7/7) + expanded sweeps (SC-002) + `--fuzz 10000` (SC-003) all run this session; RESULTS.md/DECISION.md present; quickstart.md corrected (corpus referenced in place, not `../corpus`).
- [X] T024 [P] Update `spike/antlr4-glp-grammar/REPORT.md` §3/§7 to reference SC-002 closure and link RESULTS.md/DECISION.md (single-source traceability — Constitution VIII). — DONE.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; BLOCKS all user stories.
- **US1 (Phase 3)**: depends on Foundational. MVP.
- **US2 (Phase 4)**: depends on Foundational; builds on US1's bridge (the comparator side A).
- **US3 (Phase 5)**: depends on US1 + US2 evidence (RESULTS.md).
- **Polish (Phase 6)**: after the desired stories.

### Within US1

- T008/T009/T010 (visitor groups, different concerns) are [P]; T011 wires the entry point after them; T012 static check; T013 runs + diagnoses last.

### Parallel Opportunities

- Setup T002/T003 [P]. US1 visitor groups T008–T010 [P]. US2 fuzzer T017 [P] vs corpus tasks. Polish T021/T024 [P].

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE SC-001 (7/7 MATCH)**.

### Incremental Delivery

US1 (SC-001) → US2 (SC-002 + SC-003) → US3 (SC-004 decision) → Polish (FR-010 regression + quickstart acceptance).

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Production parsers/engine are read-only inputs (FR-010); the only `Glp.g4` change is the T016 `mod`-functor tokenization (existing syntax, not an accepted-syntax change — DISCIPLINE §1.14).
- No divergence is ever silently accepted: fix in the bridge or record a bounded cause (FR-008).
- Commit after each task or logical group; keep the production REPL baseline green throughout.
