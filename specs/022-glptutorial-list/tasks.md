# Tasks: /glptutorial-list — GLP tutorial browser

**Input**: Design documents from `specs/022-glptutorial-list/`
**Prerequisites**: plan.md, spec.md, research.md (D1–D9), data-model.md, contracts/tutorials_cli.md, quickstart.md

**Tests**: INCLUDED — DISCIPLINE.md §2.4 mandates test-first; each user story has an explicit Independent Test; the plan enumerates the test suite.

**Organization**: Tasks grouped by user story (US1 P1, US2 P2, US3 P3) for independent implementation/testing. Engine = pure, bridge-free `codeconv tutorials` sub-app (research D1).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different file, no dependency on an incomplete task)
- **[Story]**: US1/US2/US3 for story-phase tasks only
- All paths are repo-relative to `D:\bstdev\research\glp\glpnet`

## Path Conventions

- Engine package: `codeconv/src/codeconv/tutorials/`
- CLI wiring: `codeconv/src/codeconv/cli.py`
- Tests + fixtures: `codeconv/tests/`
- Vendored corpus: `tutorials/olamni/`
- Skill: `.claude/skills/glptutorial-list/SKILL.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold the pure package and bridge-free CLI wiring.

- [x] T001 Create the pure package `codeconv/src/codeconv/tutorials/` with `__init__.py` and empty module stubs `corpus.py`, `describe.py`, `match.py`, `render.py`, `sync.py`, `cli.py` (no bridge/DBOS imports anywhere in this package — research D1)
- [x] T002 Wire a bridge-free Typer sub-app into `codeconv/src/codeconv/cli.py` via `app.add_typer(tutorials_app, name="tutorials")` (direct, NOT through `runner.tool_registry()`); add a placeholder `tutorials list` command that errors "not yet implemented" so registration is verifiable
- [x] T003 [P] Build the shaped fixture corpus at `codeconv/tests/fixtures/tutorials_corpus/` reproducing: a multi-`.glp` exercise, a corrected/failing `.glp` pair, an empty (no-exercise) chapter, a non-standard dir (e.g. `spec-rev-eng-input/`), a script with no derivable description, a duplicate `exercise-MM` number across two chapters, plus a top-level `tutorial.md` carrying a "Chapter status" title table (covers FR-008/FR-011/SC-002/SC-004 fixtures)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Data model + corpus resolution + walk skeleton that ALL stories depend on.

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T004 Define in-memory dataclasses `Corpus`, `Tutorial`, `Exercise`, `Script` and the `description_source` enum (`exercise_md`/`glp_header`/`none`) in `codeconv/src/codeconv/tutorials/corpus.py` per data-model.md
- [x] T005 Implement corpus-root resolution (default `<repo-root>/tutorials/olamni`, `--corpus <path>` override) and the readability guard that raises a corpus-unreachable error naming the path tried (FR-006, exit 5) in `codeconv/src/codeconv/tutorials/corpus.py`
- [x] T006 Implement the filesystem-walk skeleton in `codeconv/src/codeconv/tutorials/corpus.py`: recognize `chNN` chapter dirs and `exercise-MM` dirs, collect `.glp` scripts, collect non-`chNN/exercise-MM` dirs into `Corpus.warnings`, and treat an `exercise-MM` dir with zero `.glp` files as a non-standard shape (warn, do not render empty) (FR-011 core; deterministic sort) — read-only (FR-010)

**Checkpoint**: Foundation ready — user stories can proceed.

---

## Phase 3: User Story 1 - Browse the whole tutorial catalog (Priority: P1) 🎯 MVP

**Goal**: `codeconv tutorials list` (no arg) prints every chapter → exercise → `.glp` script, grouped and indented, empty chapters marked, non-standard dirs warned (FR-001/FR-005/FR-008/FR-011).

**Independent Test**: Run `codeconv tutorials list --corpus codeconv/tests/fixtures/tutorials_corpus` and confirm every chapter and every `.glp` appears in one grouped listing, empty chapter shows `(no scripts)`, non-standard dir emits a stderr warning.

### Tests for User Story 1 ⚠️ (write first, must fail before implementation)

- [x] T007 [P] [US1] Test full-catalog discovery lists every chapter/exercise/script (100% coverage, SC-002) and deterministic order, in `codeconv/tests/test_tutorials_corpus.py`
- [x] T008 [US1] Test empty (no-exercise) chapter is included with an explicit empty indicator (FR-008), in `codeconv/tests/test_tutorials_corpus.py`
- [x] T009 [US1] Test a non-standard dir is skipped with a warning and the listing is NOT aborted (FR-011), in `codeconv/tests/test_tutorials_corpus.py`
- [x] T010 [P] [US1] Test the list path imports no bridge/DBOS modules (D1 invariant; mirror `test_no_lm_on_production_path`), in `codeconv/tests/test_tutorials_no_bridge.py`
- [x] T011 [P] [US1] Test human-readable AND `--json` full-catalog output shape + deterministic ordering, in `codeconv/tests/test_tutorials_render.py`

### Implementation for User Story 1

- [x] T012 [US1] Implement chapter title resolution (top-level `tutorial.md` status table → `chNN_tutorial.md` H1 → bare id, D6) in `codeconv/src/codeconv/tutorials/corpus.py`
- [x] T013 [US1] Complete the walk: group chapter→exercise→script, set `is_empty`, sort chapters by id / exercises by number / scripts by name (D4) in `codeconv/src/codeconv/tutorials/corpus.py`
- [x] T014 [P] [US1] Implement human-readable full-catalog rendering (grouped, indented, `(no scripts)` indicator) in `codeconv/src/codeconv/tutorials/render.py` (FR-005/FR-008)
- [x] T015 [US1] Implement `--json` serialization of the model (chapters→exercises→scripts + warnings) in `codeconv/src/codeconv/tutorials/render.py` (FR-009 parity)
- [x] T016 [US1] Implement the no-arg `tutorials list` command in `codeconv/src/codeconv/tutorials/cli.py`: corpus→render, emit warnings to stderr (suppressed by `--quiet`), exit 0 (FR-001)

**Checkpoint**: US1 fully functional and independently testable — MVP complete.

---

## Phase 4: User Story 2 - List a single named tutorial (Priority: P2)

**Goal**: `codeconv tutorials list <TUTORIAL>` lists only the matched chapter; unknown → no-match + available ids; ambiguous → candidates (FR-002/FR-006/SC-003).

**Independent Test**: `... list ch03` shows only ch03; `... list 3` and `... list core` resolve to ch03; `... list zzz` prints "no match" + ids and exits 3.

### Tests for User Story 2 ⚠️

- [x] T017 [P] [US2] Test identifier matching variants (exact id, zero-pad `3`/`ch3`, prefix, case-insensitive title substring) each return only the matched chapter (FR-002/D5), in `codeconv/tests/test_tutorials_match.py`
- [x] T018 [US2] Test unknown id → no-match message listing available ids (exit 3, SC-003) and ambiguous id → candidate list (exit 4), in `codeconv/tests/test_tutorials_match.py`

### Implementation for User Story 2

- [x] T019 [US2] Implement identifier matching (case-insensitive; exact → zero-pad-normalized → id-prefix → title-substring; detect ambiguity) in `codeconv/src/codeconv/tutorials/match.py` (D5)
- [x] T020 [US2] Wire the `TUTORIAL` argument into `tutorials list` in `codeconv/src/codeconv/tutorials/cli.py`: filter to the matched chapter; no-match (exit 3) and ambiguous (exit 4) reporting to stderr (FR-002/FR-006)

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 - Descriptions informative enough to choose from (Priority: P3)

**Goal**: Each script shows a meaningful one-line description sourced from the exercise `.md`, falling back to the `.glp` header, else `(no description)` (FR-003/FR-004/SC-004).

**Independent Test**: For fixture scripts with descriptive text, the listing shows a concise description; a script with none shows `(no description)`; ≥95% of described-able fixture scripts get a meaningful line.

### Tests for User Story 3 ⚠️

- [x] T021 [P] [US3] Test description precedence `exercise_md` → `glp_header` → `none` with correct `description_source` tagging (FR-004/D7), in `codeconv/tests/test_tutorials_describe.py`
- [x] T022 [US3] Test a script with no derivable description shows the `(no description)` indicator and is never omitted (US3 #2), in `codeconv/tests/test_tutorials_describe.py`
- [x] T023 [US3] Test ≥95% of fixture scripts with available text get a meaningful one-line description (SC-004), in `codeconv/tests/test_tutorials_describe.py`

### Implementation for User Story 3

- [x] T024 [P] [US3] Implement exercise `.md` extraction (H1 descriptive tail after `—` and/or first non-boilerplate paragraph, normalized to one trimmed line) in `codeconv/src/codeconv/tutorials/describe.py` (D7 step 1)
- [x] T025 [US3] Implement the `.glp` leading-comment fallback (first informative `%%`/`%` line, skipping a pure filename banner) and the `(no description)` sentinel in `codeconv/src/codeconv/tutorials/describe.py` (D7 steps 2–3)
- [x] T026 [US3] Populate `Script.description` / `Script.description_source` during the walk in `codeconv/src/codeconv/tutorials/corpus.py` and surface descriptions in both human and `--json` render (FR-003)

**Checkpoint**: All three user stories functional.

---

## Phase 6: Skill front-end + equivalence (FR-009)

- [x] T027 [P] Create `.claude/skills/glptutorial-list/SKILL.md` — thin front-end modeled on `codeconv-*` skills: resolve `codeconv/.venv` python, invoke `codeconv tutorials list <args verbatim>` from repo root, relay stdout/stderr, add no behavior; document read-only scope (per contracts/tutorials_cli.md)
- [x] T028 Test skill↔CLI equivalence: the documented skill invocation maps 1:1 to the CLI command and `--json` output is identical regardless of entry point (FR-009), in `codeconv/tests/test_tutorials_skill_parity.py`

---

## Phase 7: Supporting — corpus vendoring, sync, perf (research D3)

- [x] T029 [P] Implement `codeconv tutorials sync` in `codeconv/src/codeconv/tutorials/sync.py`: copy sibling `D:/bstdev/research/glp/GLP/olamni/tutorial/` → `tutorials/olamni/` and write `tutorials/olamni/SNAPSHOT.md` + `.snapshot.json` (`{relpath: sha256}` + source path/ref/date); build-time only (D3)
- [x] T030 [P] Implement `codeconv tutorials sync --check`: recompute vendored-tree hashes vs `.snapshot.json` (and diff vs sibling when present), exit non-zero on drift (D3)
- [x] T031 Test sync round-trip and `--check` drift detection in `codeconv/tests/test_tutorials_sync.py`
- [x] T032 Run `codeconv tutorials sync` once to vendor the real corpus into `tutorials/olamni/`; commit the snapshot + provenance manifest (FR-007) — requires the sibling repo present
- [x] T033 [P] Opt-in perf test: full-catalog listing < 3 s (SC-005) using a `perf` marker, in `codeconv/tests/test_tutorials_perf.py`

---

## Phase 8: Polish & Cross-Cutting

- [x] T034 Run the full tutorials pytest suite green AND a baseline regression check that pre-existing `codeconv/tests/` still pass (DISCIPLINE §2.2): `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/` — tutorials 48/48 green (incl. `--run-perf`); full-suite pure baseline 384 passed / 0 failed / 193 skipped (the live-bridge + runtime tests, unaffected by this bridge-free additive change).
- [x] T035 [P] Align docstrings + `quickstart.md` with the shipped surface; verify cross-platform path handling (POSIX repo-relative paths in `--json`) and the Windows venv invocation documented in `SKILL.md`

---

## Dependencies & Execution Order

- **Setup (T001–T003)** → **Foundational (T004–T006)** → user stories.
- **US1 (T007–T016)** is the MVP and depends only on Foundational.
- **US2 (T017–T020)** depends on Foundational + US1's `cli.py`/render (extends `tutorials list`).
- **US3 (T021–T026)** depends on Foundational + US1 (populates descriptions used by US1 render); independently testable via `describe.py`.
- **Skill+parity (T027–T028)** depends on a working `tutorials list` (US1).
- **Supporting (T029–T033)** is independent of US1–US3 logic (own modules); T032 needs T029 + the sibling repo.
- **Polish (T034–T035)** last.

Story independence: each of US1/US2/US3 can be demoed against the fixture corpus on its own; US1 alone is a shippable increment.

## Parallel Opportunities

- T003 (fixtures) ∥ T001/T002 scaffolding once package exists.
- Within US1: T007 ∥ T010 ∥ T011 (distinct test files); T014 (render) ∥ T012/T013 (corpus) up to the T016 wiring join.
- Across phases after Foundational: US3's `describe.py` (T024) and Supporting's `sync.py` (T029/T030) touch files disjoint from US1/US2 and can proceed in parallel with story work.

## Implementation Strategy

- **MVP = Phase 1 + 2 + US1** (T001–T016): a working full-catalog browser on the fixture corpus, bridge-free.
- Then US2 (filter), US3 (descriptions), skill+parity, then vendor the real corpus (T032) and add sync/perf, then polish.

## Task Summary

- **Total**: 35 tasks (T001–T035)
- **By story**: US1 = 10 (T007–T016) · US2 = 4 (T017–T020) · US3 = 6 (T021–T026)
- **Non-story**: Setup 3 · Foundational 3 · Skill+parity 2 · Supporting 5 · Polish 2
- **Tests**: 12 test tasks (T007–T011, T017–T018, T021–T023, T028, T031, T033)
- **MVP scope**: User Story 1 (T001–T016)
