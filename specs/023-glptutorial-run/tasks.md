---

description: "Task list for 023-glptutorial-run"
---

# Tasks: /glptutorial-run — run & explain a single GLP tutorial example

**Input**: Design documents from `specs/023-glptutorial-run/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: INCLUDED. The spec's user scenarios carry "Independent Test" criteria,
research D11 specifies a test strategy, and CLAUDE.md mandates test-first +
baseline-before/after discipline. Test tasks precede the implementation they cover.

**Organization**: By user story. US1 and US2 are **co-equal P1** (US2 — the unified
run-model across both chapter shapes — is the spec's hard requirement). US3/US4 are
P2; US5 is P3. Restructuring proposals (FR-013/019) are a requirement-driven phase
(the spec invited but did not formalise a user story for them — kept low/last).

**All paths are repo-relative to** `D:\bstdev\research\glp\glpnet`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1–US5 (user-story phases only)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the run layer inside the existing bridge-free `tutorials` sub-app.

- [ ] T001 Create empty module stubs with bridge-free docstrings in `codeconv/src/codeconv/tutorials/`: `resolve.py`, `backends.py`, `outcome.py`, `explain.py`, `propose.py` (each notes the no-bridge/no-DBOS invariant, D1)
- [ ] T002 [P] Build the shaped fixture corpus under `codeconv/tests/fixtures/tutorials_run/`: section-driven single-script, section-driven multi-script, use-case guide (ch07-shaped, `exercise-MM`→`fplayMM`), stub chapter, no-goal exercise, multi-goal exercise, superseded exercise, and matching `ex-MM-tutorial.md` + `ex-MM-repl-trace.md` golden samples
- [ ] T003 [P] Record green baseline of the codeconv pytest suite (`codeconv/.venv` python `-m pytest codeconv/tests -p no:cacheprovider`), per CLAUDE.md Test Protocol

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared model, CLI wiring, execution-root resolution, and the bridge-free
guard that ALL stories depend on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [ ] T004 Extend `codeconv/tests/test_tutorials_no_bridge.py` to cover the new modules (`resolve`, `backends`, `outcome`, `explain`, `propose`) in BOTH the AST import-surface check and the clean-subprocess `sys.modules` check (D1, D11)
- [ ] T005 Define the run-layer enums + core dataclasses in `codeconv/src/codeconv/tutorials/resolve.py`: `Shape`, `LoadKind`, `GoalSource`, `RunnableExample`, `LoadTarget`, `Goal` (frozen dataclasses, per data-model.md §1–3)
- [ ] T006 Implement execution-root + drift-guard plumbing in `codeconv/src/codeconv/tutorials/resolve.py`: resolve `--sibling-corpus` (default `D:/bstdev/research/glp/GLP/olamni/tutorial`) and `--sibling-glp-root` (default `D:/bstdev/research/glp/GLP`); call `sync.check` and refuse-on-drift (D4)
- [ ] T007 Add run-layer exit-code constants (6–11) and wire the four new verbs into `tutorials_app` in `codeconv/src/codeconv/tutorials/cli.py`: replace the reserved `run` stub; register `preview`/`run`/`explain`/`propose` with the shared selector + options (selection via existing `load_corpus`/`match_tutorial`; still bridge-free) (D10)

**Checkpoint**: Foundation ready — bridge-free guard green; CLI verbs present (no-op internals).

---

## Phase 3: User Story 1 - Run a section-driven example end-to-end (Priority: P1) 🎯 MVP

**Goal**: One command turns a discovered ch01–ch06 exercise into an executed
outcome (bindings + `→ succeeds`/`→ suspended`) on the C# REPL — no hand-loading.

**Independent Test**: `codeconv tutorials run ch01 01` loads the single `.glp`, runs
the documented goal, and reports the actual outcome matching the known-good golden.

### Tests for User Story 1

- [ ] T008 [P] [US1] Resolver unit test (section-driven): shape detection from `Exercise.scripts`, `SINGLE_FILE` load target with sibling-corpus `exec_path`, goal extraction from `ex-MM-tutorial.md` — in `codeconv/tests/test_tutorials_resolve.py`
- [ ] T009 [P] [US1] Outcome-parse unit test: bindings + `→ status` from REPL stdout AND golden parse from `ex-MM-repl-trace.md`, with fresh-var (`X\d+`) normalization (D7) — in `codeconv/tests/test_tutorials_outcome.py`
- [ ] T010 [P] [US1] Backend-driver unit test against a fake/echo backend: stdin script grammar (`<load>`/`:limit`/`<goal>.`/`:quit`) + stdout capture (D6) — in `codeconv/tests/test_tutorials_backends.py`

### Implementation for User Story 1

- [ ] T011 [US1] Implement the section-driven resolver path in `codeconv/src/codeconv/tutorials/resolve.py`: shape detect, `LoadTarget(SINGLE_FILE)`, goal(s) from the guide's `GLP>` blocks (D2, D3)
- [ ] T012 [US1] Implement `codeconv/src/codeconv/tutorials/outcome.py`: parse stdout → `Outcome(bindings, status)`; parse golden from `ex-MM-repl-trace.md`; fresh-var normalization (D7)
- [ ] T013 [US1] Implement the C# backend driver (default) in `codeconv/src/codeconv/tutorials/backends.py`: **FIRST verify the C# REPL's non-interactive driving contract** — confirm `out/csharp/glp_repl` accepts a piped stdin script (`<load>`/`:limit`/`<goal>.`/`:quit`) and prints the outcome grammar to stdout; if it does not accept piped stdin, establish the non-interactive invocation (e.g. a `--script`/`-` mode) before proceeding (a non-driveable C# default is a P1, FR-007/018). Then locate/launch via `dotnet run`, feed the stdin script, capture stdout, enforce `--timeout` (D6)
- [ ] T014 [US1] Implement the `run` verb (section-driven) in `codeconv/src/codeconv/tutorials/cli.py`: resolve → C# backend → outcome → human report + brief verdict line + `--json`; support repeatable `--goal "<text>"` for choosing among / supplying goals (`source=USER_SUPPLIED`, FR-004); exit 6/7/9 on no-target/no-goal-and-none-supplied/not-implemented (FR-006/008/016, SC-001)
- [ ] T015 [US1] Real-backend gated test in `codeconv/tests/test_tutorials_backends.py`: run `ch01/exercise-01` end-to-end and assert the outcome matches its golden; skip-with-report if the C# build is absent (never silent)

**Checkpoint**: `run` works for ch01–ch06 on the C# backend (MVP).

---

## Phase 4: User Story 2 - Run a use-case example with the SAME model (Priority: P1)

**Goal**: The identical `run` command executes a ch07 play (a multi-file
module-project) with no shape-specific step — making runnable the examples the 022
lister shows as "(no scripts)".

**Independent Test**: `codeconv tutorials run ch07 01` resolves the project
`programs/cssg_modules/`, loads its modules in order, runs `fplay1`, and reports the
actual outcome (a documented `→ suspended`).

### Tests for User Story 2

- [ ] T016 [P] [US2] Resolver unit test (use-case): empty-`scripts` shape detection, `PROJECT_DIR` target = `<sibling-glp-root>/programs/cssg_modules`, `exercise-MM`→`fplayMM` primary goal + `needs_limit`, and missing-module path handling (D5, FR-017) — in `codeconv/tests/test_tutorials_resolve.py`

### Implementation for User Story 2

- [ ] T017 [US2] Implement the use-case resolver path in `codeconv/src/codeconv/tutorials/resolve.py`: `LoadTarget(PROJECT_DIR)` → canonical sibling `programs/cssg_modules/` (NOT the stale in-corpus `ch07/cssg-modules/`), primary play goal `fplayMM`, `needs_limit` from the guide (D5)
- [ ] T018 [US2] Extend `codeconv/src/codeconv/tutorials/backends.py` to load a project directory (REPL `loadProject`) and surface a missing/failed module clearly (which module + why) (FR-017)
- [ ] T019 [US2] Make `run` shape-agnostic in `codeconv/src/codeconv/tutorials/cli.py`: same command/flow for ch07; report stub chapters (ch08–ch13) as "not yet available" (exit 9) (FR-002/011, SC-002, SC-007)
- [ ] T020 [US2] Real-backend gated test in `codeconv/tests/test_tutorials_backends.py`: run `ch07/exercise-01` (`fplay1`) and assert `→ suspended` outcome matches the golden; skip-with-report if backend absent

**Checkpoint**: One unified `run` covers both shapes across ch01–ch07.

---

## Phase 5: User Story 3 - Preview before running (Priority: P2)

**Goal**: Show the goal(s) and expected outcome from the tutorial `.md` without
executing anything.

**Independent Test**: `codeconv tutorials preview ch01 01` shows the documented
goal(s) + expected outcome attributed to the `.md`, with no execution.

### Tests for User Story 3

- [ ] T021 [P] [US3] Preview unit test: renders all documented goals + expected golden outcome, attributes them to the `.md`, and performs NO subprocess/exec (assert no backend launch) — in `codeconv/tests/test_tutorials_run_cli.py`

### Implementation for User Story 3

- [ ] T022 [US3] Implement the `preview` verb in `codeconv/src/codeconv/tutorials/cli.py`: resolve → list goal(s) (multi-goal selectable) + expected outcome from golden, attributed to the guide; clear "supply a goal" message when none resolvable; `--json` (FR-005, SC-004)

**Checkpoint**: Select → preview → run flow complete (read-only preview).

---

## Phase 6: User Story 4 - Explain the actual outcome vs the tutorial (Priority: P2)

**Goal**: After a run, compare the actual outcome to the golden and explain it with
reference to the `.md`; a difference is always surfaced, never a silent pass.

**Independent Test**: `explain` on a matching example reports a match with `.md`
reference; on a differing example surfaces + explains the difference.

### Tests for User Story 4

- [ ] T023 [P] [US4] Explain unit test: `MATCH` / `DIFFERENCE` / suspended-is-valid / `NO_GOLDEN` verdicts against fixtures (D8) — in `codeconv/tests/test_tutorials_explain.py`

### Implementation for User Story 4

- [ ] T024 [US4] Implement `codeconv/src/codeconv/tutorials/explain.py`: compare normalized actual vs golden → `Verdict(kind, diffs, explanation)`; difference always surfaced; `→ suspended` valid where documented (FR-009/010)
- [ ] T025 [US4] Implement the `explain` verb in `codeconv/src/codeconv/tutorials/cli.py`: run + verdict + guide-referenced explanation; human + `--json`; carries `p1_notice` when applicable (SC-005)
- [ ] T026 [US4] SC-003 coverage check in `codeconv/tests/test_tutorials_explain.py`: across implemented ch01–ch07 examples, ≥90% report match-or-explained-difference; gated on backend availability with explicit skip-report

**Checkpoint**: Full select → preview → run → explain flow complete.

---

## Phase 7: User Story 5 - Choose the run backend (Priority: P3)

**Goal**: C# default, Dart on demand; an unavailable/wrong C# backend is a loud P1,
never a silent hang/crash/pass.

**Independent Test**: Run on each backend and confirm the report names the backend;
request an unavailable backend and confirm the explained P1 + alternative.

### Tests for User Story 5

- [ ] T027 [P] [US5] Backend-selection + P1 unit test (fake backends): `--backend dart` path, C# unavailable → exit 8 P1, optional flagged Dart fallback emits a prominent `p1_notice` — in `codeconv/tests/test_tutorials_backends.py`

### Implementation for User Story 5

- [ ] T028 [US5] Implement the Dart backend driver in `codeconv/src/codeconv/tutorials/backends.py`: `dart run bin/glp_repl.dart` / sibling `glp_repl.exe`, resolved under `--sibling-glp-root` (D6)
- [ ] T029 [US5] Implement `--backend cs|dart` selection + C# P1 surfacing (exit 8) + optional Dart fallback with prominent `p1_notice` in `codeconv/src/codeconv/tutorials/cli.py` (FR-007/018, SC-006)

**Checkpoint**: Backend selection + mandated-C#-default P1 policy in place.

---

## Phase 8: Restructuring Proposals (Requirement-driven — FR-013/019)

**Purpose**: Read-only normalization proposals; approval-gated apply. (Spec invited
but did not formalise a user story; kept low/last.)

- [ ] T030 [P] Proposal unit test: read-only report generation (`RUN_MANIFEST`, `DRIFT_GAP`, `STALE_ARTEFACT`, `LAYOUT_NORMALISE`) AND apply-guards that refuse without `--approve` + `--rationale` — in `codeconv/tests/test_tutorials_propose.py`
- [ ] T031 Implement `codeconv/src/codeconv/tutorials/propose.py`: generate the read-only proposal report (D9 classes, incl. the ch07 `programs/cssg_modules` drift-gap and stale exercise-08…12 flags)
- [ ] T032 Implement the `propose` verb in `codeconv/src/codeconv/tutorials/cli.py`: read-only default; approval-gated `--apply --approve <EXERCISE> --rationale "<why>"` that targets the sibling source of truth, re-vendors via `tutorials sync`, preserves semantics/clause-text, and is revertible (FR-013/015/019)

**Checkpoint**: Proposals available; corpus never mutated without explicit approval.

---

## Phase 9: Skill front-end & parity (FR-014)

- [ ] T033 [P] Create `.claude/skills/glptutorial-run/SKILL.md`: thin forwarder — resolve the codeconv venv, run `codeconv tutorials <verb> <args…>` verbatim from repo root, relay stdout/stderr + exit code (mirrors `/glptutorial-list`)
- [ ] T034 Skill≡CLI parity test in `codeconv/tests/test_tutorials_run_parity.py`: representative `preview`/`run`/`explain` invocations produce identical output + exit code via skill and CLI (FR-014)

**Checkpoint**: Both entry points produce equivalent behaviour.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T035 [P] Exit-code + actionable-message tests for codes 6–11 (corpus unreachable, no-target, no-goal, backend P1, not-implemented, REPL-limitation, drift) in `codeconv/tests/test_tutorials_run_cli.py` (FR-016)
- [ ] T036 [P] JSON-schema tests for `run`/`explain`/`preview`/`propose` against `contracts/tutorials_run_cli.md` in `codeconv/tests/test_tutorials_run_cli.py`
- [ ] T037 Run the full codeconv pytest suite green and confirm the extended no-bridge guard passes; record before/after vs the T003 baseline (CLAUDE.md Test Protocol)
- [ ] T038 [P] Validate `quickstart.md` end-to-end on both shapes (ch01/exercise-01 + ch07/exercise-01) on the C# backend
- [ ] T039 [P] Documentation: note any newly-surfaced REPL-limitation handling in `docs/known-issues.md`; update the CLAUDE.md directory/skill map if needed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all stories.
- **US1 (Phase 3)** & **US2 (Phase 4)**: both depend only on Foundational; co-equal P1. US2's resolver/backend work reuses US1's `outcome.py` + C# driver, so US1 lands first by default; US2 is independently testable.
- **US3 (Phase 5)**, **US4 (Phase 6)**: depend on Foundational; US4's `explain` reuses US1 `outcome.py` + a run; US3 needs only the resolver.
- **US5 (Phase 7)**: depends on Foundational + US1's backend abstraction.
- **Proposals (Phase 8)**: depends only on Foundational (resolver + corpus model).
- **Skill/parity (Phase 9)**: depends on at least one verb (US1) existing.
- **Polish (Phase 10)**: depends on all desired stories.

### Within Each User Story

- Tests written first and FAIL before implementation (TDD, DISCIPLINE.md §2.4).
- Resolver/outcome models before backend; backend before the verb; verb before the gated real-run.

### Parallel Opportunities

- Setup: T002, T003 in parallel.
- Foundational: T004 parallel to T005 (different files).
- US1 tests T008/T009/T010 in parallel (different test files).
- Implementation across stories targets distinct files (`resolve`/`backends`/`outcome`/`explain`/`propose`) — different developers can take US3/US4/US5/Proposals in parallel once Foundational + US1's shared `outcome.py`/backend land.
- Polish: T035/T036/T038/T039 in parallel.

---

## Parallel Example: User Story 1

```bash
# Tests first (parallel — different files):
Task: "T008 Resolver section-driven unit test in codeconv/tests/test_tutorials_resolve.py"
Task: "T009 Outcome + golden parse unit test in codeconv/tests/test_tutorials_outcome.py"
Task: "T010 Fake-backend driver unit test in codeconv/tests/test_tutorials_backends.py"
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE**:
`run ch01 01` matches golden on the C# backend. Demo.

### Incremental Delivery

US1 (section-driven run, MVP) → US2 (the unification — ch07 plays) → US3 (preview) →
US4 (explain) → US5 (backend choice) → Proposals → Skill/parity → Polish. Each adds
value without breaking prior stories.

---

## Notes

- [P] = different files, no dependency on incomplete tasks.
- The whole feature stays **bridge-free**: every new module is covered by the extended
  `test_tutorials_no_bridge.py` (T004) — re-run after each module lands.
- Real-backend tasks (T015, T020, T026, T038) are **gated** on a built C# solution /
  Dart REPL and MUST skip-with-report (never silent) when unavailable.
- A non-working C# backend is a **critical P1 defect** (FR-007/018) — surface loudly,
  never mask; optional Dart fallback only with a prominent `p1_notice`.
- **Verify-early prerequisite (T013):** the C# REPL's *non-interactive* (piped-stdin)
  driving contract is assumed by the whole design but not yet empirically confirmed;
  confirm it (or establish a `--script`/`-` mode) at the start of T013 before building
  the driver on it — a non-driveable C# default is itself a P1.
- Commit after each task or logical group; baseline before / re-test after (CLAUDE.md).
