# Handoff — 020-trace-equivalence-fidelity → `/buildkit-implement` (safe restart)

**Date**: 2026-05-27 | **Branch**: `020-trace-equivalence-fidelity` | **Pipeline stage reached**: `analyze` (green)
**Next command (new session)**: `/buildkit-implement` | **MVP**: User Story 1 (the differential equivalence oracle)

## Start-of-session ritual (CLAUDE.md — do this FIRST)
1. Read `CLAUDE.md`, `docs/DISCIPLINE.md`, `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` → acknowledge each.
2. You are NOT emerging from compaction if you start fresh — but if you are, STOP and tell Gabi.
3. This is **Implementation Mode** only after Gabi's explicit go. The plan/tasks below are ready; do not start coding until told.

## What is done (this session)
- `/buildkit-plan` → `plan.md` (Constitution gates PASS w/ 2 justified deviations; (C)-hybrid LM containment preserved), `research.md` (R1–R12), `data-model.md` (`dart_equivalence` + migration `0008` linearization + tombstone keys + manifest), 8 contracts in `contracts/`, `quickstart.md`. Agent context (`CLAUDE.md` BUILDKIT block) repointed to the 020 plan.
- `/buildkit-tasks` → `tasks.md`, **50 tasks**, 8 phases: Setup(T001–04) → Foundational(T005–12) → US1 oracle MVP(T013–22) → US2 strict tier(T023–29) → US3 real-GEPA(T030–38) → US4 dynamic tier(T039–42) → US5 promotion(T043–45) → Polish(T046–50).
- `/buildkit-analyze` → 0 CRITICAL, 1 HIGH, 3 MEDIUM, 3 LOW; coverage 100% (FR-015 traced to skill orchestration). **Top 4 remedies applied** (see below).

## Remedies already applied (do not re-litigate)
- **I1 (HIGH)**: US1 `equiv compare` (T019) is now a **standalone deterministic** verdict-write with NO DBOS dependency; the durable `equiv` step (T024, US2) *wraps* it. US1 is independently testable as a conformance harness.
- **C1**: FR-015 co-evolution loop traced to T030+T028+T035 (orchestration, not one task).
- **C2**: tombstone `equiv_fidelity` clarified as a cached snapshot; `fidelity.py` is the sole authoritative scorer.
- **C3**: US4 reordered — T039 = verification-mode DECISION (gate, precedes bulk dynamic gen); T040 = relation branch implementing **both** modes behind the flag T039 sets.

## Resolve / watch BEFORE or DURING implement
1. **Bootstrapping gate (B1, by design)**: US1's `@needs_runtime` tasks (T017 C#-REPL trace instrumentation, T022 e2e) only fully run once US2 has produced a runnable converted C# REPL. The pure oracle modules (T013–T015, fidelity, relation, normalize) are runtime-free and testable immediately. Sequence accordingly: build the pure core + schema first; the C# REPL trace hooks land as the strict tier compiles.
2. **Migration single-head**: versions/ has a historical dual-`0003`; `0008` chains off the single `0007` head. T006 asserts one head — run it right after T005.
3. **Replay-safety HARD GATE (R12)**: never spawn a REPL or read wall-clock inside the durable `equiv` step — capture (nondeterministic) lives in the CLI/`/codeconv-equiv` skill; the step is a pure verdict ingest. This is the top analyze risk carried from 019 R3.
4. **LM containment (SC-008)**: `tools/equiv/`, `tools/codegen/`, `durable/` must import NO dspy/litellm/openai. T038 guards it — keep it green.
5. **Dynamic-tier mode decision (T039)** is deferred-by-design and needs empirical divergence data; do NOT bulk-generate `multiagent` before it is recorded in `contracts/subsystem_curriculum.md`.
6. **GLP authority**: the trace event kinds (unify outcome / suspend / reactivate / writer-bind / bytecode-op) are the GLP three-phase + SRSW + writer-MGU semantics — do not invent events; if a needed event is absent from Dart `:trace`, STOP & report (do not modify the Dart golden).

## Baseline discipline (CLAUDE.md Test Protocol)
- Before any change: T001 — `cd codeconv && .venv\Scripts\python -m pytest -q` must be green (019 baseline: 104 pure + 73 codegen suite, 2026-05-27).
- After: T049 — full 020+019 suite green; commit the green baseline.
- `--data-dir C:/pglite/research/glpnet` on every bridge-touching `codeconv` call. `--test-concurrency=1` (PGLite cold-init ~7s).

## Git state
- Uncommitted: `specs/020-trace-equivalence-fidelity/` (all artifacts) + `CLAUDE.md` (BUILDKIT block) + `.specify/feature.json`.
- Per CLAUDE.md: commit only files touched this session, by name; offer Gabi the merge template at end. Do NOT merge to `main` (only Gabi does).

## Suggested first implement slice
Foundational T005–T012 (migration `0008` + single-head test + `trace.py` + `fidelity.py` + `manifest.py` + tombstone keys), then US1 pure core (T013–T015) with their SC-005 unit tests (T020/T021) — all runtime-free, fully green before any C# REPL exists.
