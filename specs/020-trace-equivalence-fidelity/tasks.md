# Tasks: Trace-Equivalence-Driven Codegen Fidelity (020)

**Feature**: `020-trace-equivalence-fidelity` | **Plan**: [plan.md](./plan.md) | **Spec**: [spec.md](./spec.md)
**Input**: plan.md (Python 3.11 `codeconv/`; deterministic oracle `tools/equiv/`; offline `dspy.GEPA` in `tools/codegen_opt/`; migration `0008`; durable `equiv` step), spec.md (US1–US5), data-model.md, 8 contracts, research.md (R1–R12), quickstart.md.

**Test policy**: tests are generated only where the spec's Success Criteria / Independent Tests explicitly mandate them (SC-004 scorer, SC-005 divergence batteries, SC-003 mocked-GEPA, SC-006 budget cap, SC-008 no-LM-import, migration single-head). All `codeconv` calls use `--data-dir C:/pglite/research/glpnet`. The production/durable path stays LM-free; the optimizer is offline-only.

**MVP = User Story 1** (the differential equivalence oracle) — nothing downstream has a fidelity signal without it.

---

## Phase 1: Setup

- [X] T001 Run the 019 baseline and record green status (104 pure + 73 codegen suite) in `codeconv/`: `.venv\Scripts\python -m pytest -q` — checkpoint before any change (CLAUDE.md Test Protocol, FR-019). RESULT 2026-05-27: 401 passed / 3 skipped / 11 "failed" all = `bridge unreachable: timed out after 60.0s` (spawn timeout, NOT logic); all 11 reproduce GREEN on isolated re-run (concurrent-pytest collision + transient cold-spawn). Baseline accepted green. See current_plan.md "Bridge-test flakiness".
- [X] T002 Create the `tools/equiv/` subpackage skeleton with `__init__.py` (auto-discovered Typer app, bare = `status`) in `codeconv/src/codeconv/tools/equiv/__init__.py` — discovered by `tool_registry()`; bare `codeconv equiv`→status works (note: avoided the 019 codegen `ctx.invoke(status)` latent bug by delegating to `_run_status`)
- [X] T003 [P] Create the checked-in artifact directories with placeholder READMEs: `.codeconv/equiv-manifest/`, `.codeconv/codegen-prompt/`, `.codeconv/conversion-equiv/`
- [X] T004 [P] Add `@needs_runtime` pytest marker (Dart/C# REPL present) alongside the existing `@needs_bridge`, registered in `codeconv/pyproject.toml`/conftest, skipping where REPLs absent — `needs_runtime` skipif in conftest (Dart `glp_repl.exe` present; C# REPL gated on `CODECONV_CSHARP_REPL`/built `out/csharp/**/glp_repl*`, currently absent ⇒ e2e tests skip per B1)

---

## Phase 2: Foundational (blocking prerequisites for all stories)

- [X] T005 Author migration `0008_equivalence.py` (`revision='0008'`, `down_revision='0007'`, `CREATE TABLE IF NOT EXISTS codeconv.dart_equivalence` per data-model.md; indexes incl. partial `WHERE verdict='stale'`; no `public`/`dbos` DDL) in `codeconv/src/codeconv/db/migrations/versions/0008_equivalence.py` (contract: equiv_schema.md)
- [X] T006 Test: migration single-head — assert the runner reports exactly one head (`0008`) and no branch, in `codeconv/tests/test_migration_single_head.py` (contract: equiv_schema.md) — offline single-head/chain GREEN; added `test_migration_0008_single_head.py` (feature owner), demoted `test_migration_0007_single_head.py` to interior-node assertions
- [X] T007 Implement the normalized-trace model `Event`/`Trace`/`Outcome` (PURE) in `codeconv/src/codeconv/tools/equiv/trace.py` (contract: trace_normalization.md; FR-002)
- [X] T008 [P] Implement `tools/equiv/fidelity.py` — the SINGLE tiered scorer `score(FidelityInputs)->float` (`0.0`/`0.25`/`0.5+0.5·frac` clamped `<1.0`/`1.0`) (contract: fidelity_metric.md; FR-013) — shared prerequisite of the US1 gate AND the US3 GEPA metric
- [X] T009 [P] Implement `tools/equiv/manifest.py` — load+validate `.codeconv/equiv-manifest/subsystems.yml` (every in-scope source assigned once; ratios within tolerance; prefixes resolve in `dart_depgraph`) (contract: subsystem_curriculum.md; FR R8/R9) — longest-prefix classify (overlapping heap⊂runtime-core by design); validated 0 ties/0 unclassified vs real inventory
- [X] T010 Author the initial `.codeconv/equiv-manifest/subsystems.yml` (5 subsystems + tiers; corpus trace/outcome/back_test; deterministic train~70%/held-out~30% assignments) (data-model.md; R8/R9) — structure complete; per-source `assignments` deferred to the reviewed corpus step (T016/T032), scheme recorded
- [X] T011 Extend `_FIELD_ORDER` in `codeconv/src/codeconv/tools/discover/tombstone.py` with the equiv keys (append-only, AFTER 019 codegen keys): `equiv_subsystem, equiv_tier, equiv_verdict, equiv_fidelity, equiv_bytecode_diff_empty, equiv_stale, equiv_last_verified_at` (data-model.md; 012 stability) — `_FEATURE_020_KEYS` folded into `_PRESERVED_APPENDED_KEYS`; pure tombstone round-trip tests GREEN
- [X] T012 [P] Test: `fidelity.py` tier boundaries (non-compile→0.0; compile-no-evidence→0.25; partial→(0.5,1.0); frac=1.0→exactly 1.0; back-test+human-approved-not-equivalent→<1.0) in `codeconv/tests/test_fidelity_metric.py` (SC-004)

**Checkpoint**: schema + pure scorer + trace model + manifest exist and are unit-green; nothing yet spawns a REPL.

---

## Phase 3: User Story 1 — Differential equivalence oracle (P1, MVP)

**Goal**: take any GLP source, run it through Dart (golden) + C# (candidate) REPLs, emit equivalent|divergent + first divergence, under the causal/partial-order relation; bytecode-diff early checkpoint; bonds outcome-only.
**Independent test**: known-equivalent pair → equivalent; corrupted C# trace (eager writer bind) → divergent at that event; heap-relabel + independent-goal reorder → NOT divergent.

- [X] T013 [P] [US1] Implement heap→logical relabeling + causal-edge derivation in `codeconv/src/codeconv/tools/equiv/normalize.py` (PURE; `parse_dart`, `parse_csharp` → same model) (contract: trace_normalization.md; FR-002) — committed fe512bd9; pure green
- [X] T014 [P] [US1] Implement the equivalence relation `compare(golden, candidate, mode, tier)` — OUTCOME mode; TRACE mode REQUIRE outcomes+spine+dependent-events / ABSTRACT addresses+independent-order; STRICT total-order specialization; first-`DivergenceRecord` — in `codeconv/src/codeconv/tools/equiv/relation.py` (contract: equivalence_relation.md; FR-003/FR-008) — committed fe512bd9; pure green
- [X] T015 [P] [US1] Implement the bytecode-emission diff early checkpoint in `codeconv/src/codeconv/tools/equiv/bytecode_diff.py` (C#-emitted vs Dart-emitted; same opcodes@logical-PCs) (contract: equivalence_relation.md; FR-004) — committed fe512bd9; pure green
- [X] T016 [US1] Implement `tools/equiv/corpus.py` — enumerate the corpus (unified 384 + book 141 trace; bonds outcome-only), tag each source with compare_mode + subsystem; read `.glp` in place under `programs/` (no copy) (FR-005/FR-006) — RESULT 2026-05-28: g1=c (reviewed checked-in `.codeconv/equiv-manifest/corpus.yml`, seeded by parsing run_all_tests.sh + run_book_tests.sh) + g2=a (source-level tag = strict|dynamic ONLY; authoritative per-row subsystem stays file-derived via `manifest.classify`). 256 sources: book 141 (exact), unified 108 files + 6 projects, bonds_v2 outcome-only. Split materialized into `subsystems.yml` (27.3% held-out, within tolerance); `manifest.validate(glp_sources=...)` → 0 errors; corpus↔manifest split 0 mismatches. Sources read in place under `programs/`; TC_DIR/MODED + load-rejection fixtures excluded (FR-006). Entry = file | project (multi-module play loaded as a unit)
- [ ] T017 [US1] Add structured trace instrumentation to the converted C# REPL in `out/csharp/` emitting the R1 event kinds comparable to Dart `:trace` (candidate-side only; Dart golden untouched) (contract: trace_normalization.md R10; assumption)
- [ ] T018 [US1] Implement `equiv capture <key> <source>` (spawn both REPLs, normalize, write recorded trace artifacts, `phase=captured`) in `tools/equiv/__init__.py` — nondeterministic spawn lives HERE (agent/CLI), not in any DBOS step (contract: equiv_cli.md, dbos_equiv_stage.md R12)
- [ ] T019 [US1] Implement `equiv compare` + `equiv bytecode-diff` CLI subcommands as a **standalone deterministic** verdict-write (apply pure `relation.py` to recorded traces, write `dart_equivalence` directly — NO DBOS dependency, so US1 is independently testable as a conformance harness; exit 0/2/3) in `tools/equiv/__init__.py` (contract: equiv_cli.md). The durable `equiv` step that *wraps* this is added in US2 (T024).
- [X] T020 [US1] Test (SC-005): zero false divergences on heap-address relabeling AND on independent-goal reordering (constructed fixtures) in `codeconv/tests/test_equiv_false_divergence.py` — committed fe512bd9; green
- [X] T021 [US1] Test (SC-005): zero false equivalences on a seeded divergence battery incl. eager-writer-bind (must report `divergent` at the WRITER_BIND/REACTIVATE event) in `codeconv/tests/test_equiv_divergence_battery.py` — committed fe512bd9; green
- [ ] T022 [US1] Test [US1 independent-test, `@needs_runtime`]: known-equivalent pair → exit 0 equivalent; bonds source → outcome-only verdict, no interleaving diff, in `codeconv/tests/test_equiv_oracle_e2e.py`

**Checkpoint**: the oracle is a usable conformance harness on its own (verdict + first divergence), before any optimization.

---

## Phase 4: User Story 2 — Strict-tier conversion verified by exact equivalence (P1)

**Goal**: deterministic subsystems gated by build AND exact equivalence (empty bytecode-diff AND total-order trace equality); convert in curriculum/dependency order; escalate-don't-guess.
**Independent test**: convert `heap_fcp` + bytecode runner; run unified+book through C# REPL; empty bytecode diff + total-order traces equal Dart; non-faithful conversion rejected.

- [ ] T023 [US2] Implement `tools/equiv/readiness.py` — equiv-readiness predicate (deps converted+equivalent; subsystem/tier from manifest; curriculum order from `dart_depgraph`, read-only) (contract: subsystem_curriculum.md; FR-007)
- [ ] T024 [US2] Implement `tools/equiv/workflow.py:register()` durable `equiv` step that **wraps the standalone US1 `compare`** (deterministic verdict ingest of recorded traces; two-phase `dart_equivalence` write; typed `needs_agent_work` when traces absent — never raises). The step adds DBOS durability/resumability over the US1 verdict; it does not re-implement the relation (contract: dbos_equiv_stage.md; R12)
- [ ] T025 [US2] Wire the `equiv` stage AFTER `codegen` in `codeconv/src/codeconv/durable/workflows.py` + register the step in `durable/steps.py` (contract: dbos_equiv_stage.md; FR-018)
- [ ] T026 [US2] Implement `equiv next` / `equiv status` / `equiv ingest` / `equiv retry` CLI (frontier in curriculum order; per-subsystem fidelity rollup ≤5 s warm; one bounded re-verify then escalate) in `tools/equiv/__init__.py` (contract: equiv_cli.md; FR-008)
- [ ] T027 [US2] Implement `equiv aggregate-escalations` → `.codeconv/conversion-equiv/_escalations-report.md` in `tools/equiv/__init__.py` (contract: equiv_cli.md)
- [ ] T028 [US2] Create the `/codeconv-equiv` skill (`.claude/skills/codeconv-equiv/SKILL.md`) — drive capture→compare→record across the frontier, loop escalations, bounded retry-then-escalate (justified orchestration deviation, plan Complexity Tracking)
- [ ] T029 [US2] Test [`@needs_runtime`]: strict-tier gate accepts only on build+empty-bytecode-diff+total-order-equality; a trace-divergent strict file is NOT marked converted and escalates after one bounded repair, in `codeconv/tests/test_strict_tier_gate.py` (FR-008; US2 acceptance 1/3)

**Checkpoint**: strict subsystems convert under an exact-equivalence gate; a viable behaviourally-faithful C# core; the corpus GEPA learns idioms on first.

---

## Phase 5: User Story 3 — Real `dspy.GEPA` per-subsystem optimization (P2, OFFLINE)

**Goal**: replace 019's hand-rolled loop with real `dspy.GEPA`; metric returns score + textual divergence feedback; per-subsystem prompts on a shared base, carried forward; budget-capped, offline-only.
**Independent test**: on a held-out eval set with a MOCKED LM, optimized prompt ≥ baseline; budget cap halts with best-so-far; production path imports no LM/dspy.

- [ ] T030 [US3] Rewire `tools/codegen_opt/optimize.py` to real `dspy.GEPA` (per-subsystem prompt; shared `_base.md` carry-forward seed; hard `--budget`, best-so-far on cap) (contract: gepa_optimizer.md; FR-010/FR-011/FR-012)
- [ ] T031 [US3] Rewrite `tools/codegen_opt/metric.py` to return `dspy.Prediction(score=fidelity.py score, feedback=DivergenceRecord-as-text)` — score IDENTICAL to the production gate via `import tools.equiv.fidelity` (contract: gepa_optimizer.md, fidelity_metric.md; FR-010/SC-004)
- [ ] T032 [P] [US3] Update `tools/codegen_opt/dataset.py` to build per-subsystem train/held-out datasets from the manifest (contract: gepa_optimizer.md; R9)
- [ ] T033 [P] [US3] Update `tools/codegen_opt/program.py` signature to consume `subsystem` + reflective feedback (shape unchanged from 019) (contract: gepa_optimizer.md)
- [ ] T034 [US3] Implement production prompt selection `tools/codegen/prompt.py:load(subsystem)` reading `.codeconv/codegen-prompt/<subsystem>.md` — NO LM/dspy import (contract: gepa_optimizer.md; FR-011)
- [ ] T035 [US3] Extend the `/codeconv-codegen-opt` skill (`.claude/skills/codeconv-codegen-opt/SKILL.md`) for real-GEPA per-subsystem optimization + `_base.md` carry-forward (plan Complexity Tracking)
- [ ] T036 [US3] Author per-subsystem prompt artifacts (`_base.md` + `heap/bytecode/compiler/runtime-core/multiagent.md`) with provenance front-matter in `.codeconv/codegen-prompt/` (data-model.md)
- [ ] T037 [US3] Test (SC-003/SC-006): MOCKED-LM GEPA run — optimized prompt scores ≥ baseline on the held-out split; budget cap halts with usable best-so-far, in `codeconv/tests/test_codegen_opt_gepa_mocked.py`
- [ ] T038 [US3] Test (SC-008): `tools/equiv/`, `tools/codegen/`, `codeconv/src/codeconv/durable/` import NO dspy/litellm/openai, in `codeconv/tests/test_no_lm_on_production_path.py`

**Checkpoint**: GEPA actively drives fidelity up per subsystem; production path provably LM-free.

---

## Phase 6: User Story 4 — Dynamic multiagent tier under causal equivalence (P2)

**Goal**: convert `lib/multiagent/` LAST with the matured prompt; gate = build + causal/partial-order + outcome-equivalence; record the verification-mode decision (pinned vs accept-any-causal) with rationale BEFORE bulk generation.
**Independent test**: convert `isolate_manager`; multiagent/CSSN plays through both REPLs; cross-agent causal events match + outcomes match; independent interleaving NOT flagged.

- [ ] T039 [US4] Record the dynamic-tier verification-mode DECISION (pinned-schedule | accept-any-causal) with the motivating divergence data (from initial, non-bulk multiagent conversion) + the `relation.py` implication, in `specs/020-trace-equivalence-fidelity/contracts/subsystem_curriculum.md` — **gate task, MUST precede bulk dynamic generation** (US4 acceptance 3)
- [ ] T040 [US4] Implement the DYNAMIC-tier branch in `relation.py` — full partial-order isomorphism on dependent events + outcome-equivalence, with **both** verification modes selectable by a flag the T039 decision sets (contract: equivalence_relation.md; FR-009; depends on T039)
- [ ] T041 [US4] Implement strict→dynamic reclassification handling (a strict source showing scheduling nondeterminism → manifest update + partial-order verify, with rationale; never force exact equality) in `tools/equiv/readiness.py` + manifest (spec edge case)
- [ ] T042 [US4] Test [`@needs_runtime`]: multiagent run where independent agents interleave differently → equivalent iff data-dependent events + outcomes match; causal cross-agent event (writer-bind→reader-reactivation) mismatch → divergent, in `codeconv/tests/test_dynamic_tier_equiv.py` (US4 acceptance 1/2)

**Checkpoint**: the only nondeterministic tier verified under the relaxed-but-sound relation, mode decided from data.

---

## Phase 7: User Story 5 — Tiered metric + corpus-wide promotion (P3)

**Goal**: formalize the promotion gate the other stories feed; promote a subsystem/runtime only at full trace-equivalence (outcome-equivalent for bonds).
**Independent test**: scoring across all tiers; compiling-but-not-equivalent → high band <1.0; only a fully-equivalent corpus promotes.

- [ ] T043 [US5] Implement `equiv fidelity <key>` + `equiv promote <subsystem>` CLI — promote ⇔ every in-scope source equivalent (outcome-equivalent for bonds); compile/human/back-test alone do NOT promote — in `tools/equiv/__init__.py` (contract: equiv_cli.md, fidelity_metric.md; FR-014)
- [ ] T044 [US5] Implement `equiv mark-stale` + `tools/equiv/stale.py` Dart source-drift detection (hash mismatch → `verdict=stale`; stale rows excluded from `frac`) (contract: equiv_cli.md; FR-016)
- [ ] T045 [P] [US5] Test: promotion gate promotes only at corpus full-equivalence and not before; a compiling+back-test-passing+human-approved-not-equivalent file scores <1.0, in `codeconv/tests/test_promotion_gate.py` (SC-004; US5 acceptance 1/2)

---

## Phase 8: Polish & Cross-Cutting

- [ ] T046 [P] Implement FR-017 Dart-spec-violation surfacing: when divergence traces to a Dart original violating the GLP spec, emit the CLAUDE.md Bug-Protocol report (do NOT alter C# to match a wrong oracle) — in `tools/equiv/__init__.py` divergence handling (FR-017)
- [ ] T047 [P] Round-trip test: equiv tombstone keys survive a DB rebuild (012 contract) in `codeconv/tests/test_equiv_tombstone_roundtrip.py`
- [ ] T048 Update `docs/known-issues.md` + the 020 quickstart with any REPL-instrumentation quirks discovered; verify `equiv status` ≤5 s warm
- [ ] T049 Re-run the full suite (020 + 019 baseline) in `codeconv/`: `.venv\Scripts\python -m pytest -q` — confirm green (FR-019, SC-008); commit the green baseline
- [ ] T050 Verify SC roll-up via `equiv status` (SC-001 trace/outcome verdicts; SC-002 ≥95% no-manual-edit; SC-007 empty bytecode diff for deterministic tier) and record in the escalations report

---

## Dependencies & Story Completion Order

- **Setup (P1: T001–T004)** → **Foundational (T005–T012)** block everything.
- **US1 (T013–T022)** — MVP; depends only on Foundational. Independently testable as a conformance harness.
- **US2 (T023–T029)** — depends on US1 (the oracle) + Foundational (durable/migration). The strict-tier gate.
- **US3 (T030–T038)** — depends on US1 (divergence feedback) + the `fidelity.py` scorer (T008); benefits from US2's clean signal. OFFLINE; does not block the production path.
- **US4 (T039–T042)** — depends on US1+US2 (harness proven on strict tier) and the matured prompt from US3; sequenced LAST. T039 (verification-mode decision) is a gate before any bulk dynamic generation; T040 implements both modes behind the flag it sets.
- **US5 (T043–T045)** — pure gate over US1 verdicts + the scorer; can be built once US1 lands.
- **FR-015 (optimizer-first co-evolution loop)** is realized by orchestration, not a single task: T030 (GEPA optimize-before-generate) + T028 (`/codeconv-equiv` drives generate→gate→reflect→regenerate→freeze) + T035 (`/codeconv-codegen-opt` carry-forward). Tracked here so it is not read as uncovered.
- **Polish (T046–T050)** — after the stories it touches.

## Parallel Opportunities

- Foundational: T008, T009, T012 in parallel (different files) after T007.
- US1: T013, T014, T015 in parallel (normalize / relation / bytecode_diff are independent pure modules); T020, T021 tests in parallel.
- US3: T032, T033 in parallel; T037, T038 tests in parallel.
- Polish: T046, T047 in parallel.

## Implementation Strategy

**MVP = US1** (T001–T022): a working differential equivalence oracle + the pure scorer + schema. Demonstrable as a conformance harness with zero optimization. Then layer US2 (strict gate) → US3 (GEPA, offline) → US5 (promotion) → US4 (dynamic tier, last, mode decided from data) → Polish. Baseline pytest before (T001) and after (T049); the 019 suite must stay green throughout (FR-019).
