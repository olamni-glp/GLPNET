---
description: "Tasks for feature 014 — codeconv-discover resolves package:glp_runtime/... self-imports as in-subtree edges"
---

# Tasks: codeconv-discover resolves `package:glp_runtime/...` self-imports

**Input**: Design documents from `specs/014-package-self-import-resolution/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Tests are REQUIRED for this feature. The spec mandates them (SC-003: "at least 3 new tests cover the rewrite path") and the contracts (`contracts/parser_contract.md` and `contracts/workflow_contract.md`) enumerate the test obligations explicitly.

**Organization**: Tasks are grouped by user story (US1 = P1 self-package rewrite; US2 = P2 outside-caller rewrite parity) so each story can be implemented and verified independently. US1 alone is a viable MVP: it delivers the entire dependency-graph correctness fix; US2 prevents future false-negatives in cross-tree warnings.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different files, no dependencies on incomplete tasks → can run in parallel
- **[Story]**: US1 (P1 — self-package rewrite) or US2 (P2 — outside-caller parity)
- File paths are absolute-relative to repo root `D:\BSTDEV\research\GLP\GLPNET\`

## Path Conventions

- Python source: `codeconv/src/codeconv/tools/discover/`
- Python tests: `codeconv/tests/`
- Tombstone artefacts: `.codeconv/tombstones/` (refreshed once at end of feature)
- Spec artefacts: `specs/014-package-self-import-resolution/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green test baseline and confirm the working environment supports the rest of the work.

- [ ] T001 Confirm `glp_runtime_net/pubspec.yaml` exists and `name:` field reads `glp_runtime` (one-line `Get-Content`); record value in plan.md if it ever drifts
- [ ] T002 Run baseline `pytest codeconv/tests/ --test-concurrency=1` and confirm it is green per memory `project_012_codeconv_runner_status.md`; if reds appear that are not in the known-skip list (perf opt-in, Windows symlinks), STOP and report
- [ ] T003 Confirm `--data-dir` override is wired (run `codeconv discover --help | findstr data-dir`); record any divergence
- [ ] T004 Snapshot the current `isolated` count + `imports` count from a baseline `/codeconv-discover` run against `glp_runtime_net/` (one-time, before any code change), record in `specs/014-package-self-import-resolution/baseline.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None. This feature has no schema migration, no new database table, no shared infrastructure to bootstrap. All prerequisites are covered by feature 012's already-merged surface (PGLite bridge, codeconv runner, discover tool). Foundational phase is intentionally empty.

**Checkpoint**: Setup green → US1 can begin immediately.

---

## Phase 3: User Story 1 — Self-package rewrite resolves in-subtree edges (Priority: P1) 🎯 MVP

**Goal**: `extract_imports` accepts an optional `package_name` argument and rewrites `package:<name>/<rest>` to `lib/<rest>` before in-subtree resolution. `workflow.run_discover` reads pubspec once, caches the name, threads it through `_process_one_file`. Result: `heap_fcp.dart`'s tombstone shows the four expected dependencies; `isolated` count drops below 20.

**Independent Test**: Run `pytest codeconv/tests/test_parse.py codeconv/tests/test_pubspec.py codeconv/tests/test_discover_self_package_e2e.py --test-concurrency=1`; all green. Run `codeconv discover --data-dir .pgdb --root glp_runtime_net`; inspect `.codeconv/tombstones/lib/runtime/heap_fcp.dart.md` and verify the four expected dependencies appear in the `dependencies:` field.

### Tests for User Story 1 (REQUIRED — write FIRST, ensure they FAIL before implementation)

- [ ] T005 [P] [US1] Add `test_resolves_self_package_imports` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` § "Test obligations" item 1
- [ ] T006 [P] [US1] Add `test_external_package_imports_still_skipped` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` item 2
- [ ] T007 [P] [US1] Add `test_self_package_when_package_name_none` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` item 3
- [ ] T008 [P] [US1] Add `test_self_package_dedup_against_relative` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` item 4 (covers FR-007)
- [ ] T009 [P] [US1] Add `test_malformed_self_package_skipped` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` item 5
- [ ] T010 [P] [US1] Add `test_self_package_outside_lib_skipped` to `codeconv/tests/test_parse.py` per `contracts/parser_contract.md` item 6
- [ ] T011 [P] [US1] Create `codeconv/tests/test_pubspec.py` with the seven unit tests enumerated in `contracts/workflow_contract.md` § "test_pubspec.py (NEW)"
- [ ] T012 [P] [US1] Create `codeconv/tests/test_discover_self_package_e2e.py` with `test_heap_fcp_style_fanin`, `test_external_package_still_skipped_e2e`, `test_pubspec_absent_falls_back_to_isolated` per `contracts/workflow_contract.md` § "test_discover_self_package_e2e.py (NEW)" — gate with `@needs_bridge`

### Implementation for User Story 1

- [ ] T013 [US1] Create `codeconv/src/codeconv/tools/discover/pubspec.py` with the `read_package_name` function exactly as specified in `contracts/workflow_contract.md` § "New module"; export via `__all__`
- [ ] T014 [US1] Modify `codeconv/src/codeconv/tools/discover/parse.py::extract_imports` to accept `package_name: Optional[str] = None`; insert the rewrite branch BEFORE the `package:` / `dart:` / `dart-ext:` skip per `contracts/parser_contract.md` § "Behaviour" steps 1-2; resolve rewritten targets against `subtree_root / "lib"` per the implementation note in that section
- [ ] T015 [US1] Modify `codeconv/src/codeconv/tools/discover/workflow.py::run_discover` to call `read_package_name(subtree, repo_root=repo_root)` immediately after the `subtree`/`tombstones_root` setup (before `acquire_or_discover`); bind `package_name`, `pubspec_warning`; pass both to `_run_normal` via new keyword args per `contracts/workflow_contract.md` § "run_discover — call sequence"
- [ ] T016 [US1] Modify `_run_normal` to accept the new keyword args; if `pubspec_warning is not None`, append it to `warnings_list` exactly once at function start (BEFORE the per-file loop); pass `package_name` to `_process_one_file` per `contracts/workflow_contract.md` § "_run_normal — propagation"
- [ ] T017 [US1] Modify `_process_one_file` to accept `package_name: Optional[str] = None` keyword arg and forward to `extract_imports(abs_path, subtree, package_name)` per `contracts/workflow_contract.md` § "_process_one_file — propagation only"
- [ ] T018 [US1] Run `pytest codeconv/tests/test_parse.py codeconv/tests/test_pubspec.py --test-concurrency=1`; verify T005-T011 pass. Run `pytest codeconv/tests/test_discover_self_package_e2e.py --test-concurrency=1` (requires bridge); verify T012 passes. If any fail, return to T013-T017 and fix; do NOT touch tests
- [ ] T019 [US1] Run the full `pytest codeconv/tests/ --test-concurrency=1`; verify the existing 39+ tests still pass (regression-free per SC-003) — the existing 5 tests in `test_parse.py` (lines 30-148) call with two positional args and rely on the `package_name=None` default

**Checkpoint**: US1 complete → MVP shippable. Acceptance scenario 1 from spec.md (heap_fcp.dart's four edges) verified.

---

## Phase 4: User Story 2 — Outside-subtree warnings catch self-package cross-tree references (Priority: P2)

**Goal**: `_scan_outside_callers` applies the same self-package rewrite so an outside-subtree file with `import 'package:glp_runtime/...';` raises an `outside_caller` warning naming both files. No caller edge is ever recorded for outside files (FR-023 preserved).

**Independent Test**: Run `pytest codeconv/tests/test_outside_subtree_warning.py --test-concurrency=1`; both the existing test and the new self-package case pass.

### Tests for User Story 2 (REQUIRED)

- [ ] T020 [P] [US2] Add `test_outside_caller_via_package_form_warns` to `codeconv/tests/test_outside_subtree_warning.py` per `contracts/workflow_contract.md` § "test_outside_subtree_warning.py (MODIFIED)" — synthesises an outside-subtree file with a self-package import; verifies the `outside_caller` warning shape and that no caller edge is recorded

### Implementation for User Story 2

- [ ] T021 [US2] Modify `codeconv/src/codeconv/tools/discover/workflow.py::_scan_outside_callers` to accept `package_name: Optional[str] = None` keyword arg; insert the self-package rewrite branch BEFORE the existing `target.startswith(("package:", "dart:", "dart-ext:"))` skip per `contracts/workflow_contract.md` § "_scan_outside_callers — rewrite parity (FR-006)"; emit the same `outside_caller` warning shape used in the existing relative-path branch
- [ ] T022 [US2] Modify `_run_normal`'s call to `_scan_outside_callers` to pass `package_name=package_name` (already in scope from T016)
- [ ] T023 [US2] Run `pytest codeconv/tests/test_outside_subtree_warning.py --test-concurrency=1`; verify T020 passes. Run the full suite again; verify zero regression

**Checkpoint**: US2 complete. Acceptance scenarios for US2 (spec.md US2 § Acceptance Scenarios) verified.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Run the canonical quickstart smoke; refresh tombstones once on the feature branch (SC-007); confirm performance budgets; finalise documentation.

- [ ] T024 Run all of `quickstart.md` Flow G steps 1-4 against the live `glp_runtime_net/` checkout; capture results inline in a temp scratch file (do NOT commit the scratch). Verify SC-001 (`isolated < 20`) and SC-002 (heap_fcp.dart's four deps)
- [ ] T025 Run `quickstart.md` Flow G step 6 (missing-pubspec fallback). Verify SC-005: exactly one `pubspec_missing` warning, `reason: "absent"`, `path: "glp_runtime_net/pubspec.yaml"`. Restore the file before continuing
- [ ] T026 Run `pytest codeconv/tests/test_discover_perf.py --run-perf --test-concurrency=1`; verify SC-006 (carry-forward of feature-012 SC-013): fresh ≤ 60 s, idempotent ≤ 5 s
- [ ] T027 Run `pytest codeconv/tests/test_discover_idempotence.py --test-concurrency=1`; verify SC-004 (zero diff in DB rows + tombstones on second consecutive run)
- [ ] T028 Run `quickstart.md` Flow G step 8: SC-007 single tombstone-refresh commit. After all tests are green: (a) run `codeconv doctor --help` and verify `--truncate-codeconv` is listed; if absent, fall back to the manual `psql -c "TRUNCATE codeconv.dart_files, codeconv.dart_imports, codeconv.dart_callers, codeconv.dart_files_orphaned;"` recipe documented in `quickstart.md` Flow G step 2 "Notes on the helper invocations". (b) Run `codeconv discover --data-dir .pgdb --root glp_runtime_net`. (c) Commit the resulting `.codeconv/tombstones/` diff with message `"Refresh tombstones after feature 014 self-package rewrite (SC-007)"`
- [ ] T029 [P] Verify FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching) greps stay clean per `specs/012-codeconv-runner` Phase 7 verifications: `pytest codeconv/tests/test_phase7_verifications.py --test-concurrency=1`
- [ ] T030 [P] Update `docs/known-issues.md` if any new edge case surfaced during T024-T028 (likely: none; this feature is small)
- [ ] T031 Final full suite: `pytest codeconv/tests/ --test-concurrency=1`; record pass/skip count in PR description; if not at least the 39 baseline + 12 new tests (3 e2e + 7 pubspec + 6 parse + 1 outside) all green, STOP and triage

**Checkpoint**: All success criteria SC-001 through SC-007 verified. Feature ready for `/speckit-implement` to merge into `main` per the CalVer release flow.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Empty (feature is purely additive Python; no schema or infra).
- **US1 (Phase 3)**: Depends on Setup completion. Internally: tests (T005-T012) before implementation (T013-T017); validation (T018-T019) after implementation.
- **US2 (Phase 4)**: Depends on US1 completion (US2 reuses the `package_name` cached by US1's T015 in `_run_normal`). Test before implementation; validation after.
- **Polish (Phase 5)**: Depends on US1 + US2 complete. T028's tombstone refresh runs LAST so it captures both stories' edges.

### Within Each User Story

- **TDD ordering**: tests in T005-T012, T020 are written FIRST and MUST FAIL against the unmodified codebase before implementation tasks (T013-T017, T021-T022) are touched. Re-running a test after implementation makes it green; this is the required state-transition signal for marking the test task complete.
- **Implementation ordering**: Within US1 — `pubspec.py` (T013) is independent and can be done first or in parallel; `parse.py` (T014) is independent of `workflow.py` (T015-T017); within `workflow.py` itself, T015 → T016 → T017 are sequential (each function references the next-lower one's signature).
- **Validation ordering**: T018 (story-scoped tests) before T019 (full suite). Both before US1 checkpoint.

### Parallel Opportunities

- All T005-T012 (story 1 tests) — different test files OR different test functions in the same file (test_parse.py); marked [P] within the same phase.
- T013 (`pubspec.py`) parallel to T014 (`parse.py`) — different files.
- T029, T030 in Phase 5 — different concerns, different files.
- US1 and US2 implementation cannot run in parallel because both modify `workflow.py`'s `_run_normal` signature (T016, T022).

---

## Parallel Example: User Story 1 tests

```bash
# Launch all US1 tests together (different test functions, can run in parallel):
Task: "Add test_resolves_self_package_imports to codeconv/tests/test_parse.py"
Task: "Add test_external_package_imports_still_skipped to codeconv/tests/test_parse.py"
Task: "Add test_self_package_when_package_name_none to codeconv/tests/test_parse.py"
Task: "Add test_self_package_dedup_against_relative to codeconv/tests/test_parse.py"
Task: "Add test_malformed_self_package_skipped to codeconv/tests/test_parse.py"
Task: "Add test_self_package_outside_lib_skipped to codeconv/tests/test_parse.py"
Task: "Create codeconv/tests/test_pubspec.py with seven unit tests"
Task: "Create codeconv/tests/test_discover_self_package_e2e.py with three e2e tests (gated by @needs_bridge)"
```

(In practice these are all written by one engineer in one sitting since each is ~10-20 lines; the parallelism is conceptual — they share no dependencies.)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1: Setup baseline (T001-T004).
2. Phase 2: skip (empty).
3. Phase 3: US1 (T005-T019).
4. **STOP and VALIDATE**: SC-001 (`isolated < 20`) and SC-002 (heap_fcp.dart's four deps) verified by hand running quickstart Flow G steps 1-4. Acceptance scenario 1 of spec.md US1 verified.
5. If only US1 is shipped (US2 deferred), the feature still delivers the full graph-correctness fix; outside-subtree false-negatives remain a future concern.

### Full Delivery

1. US1 → US2 → Polish, sequential per the Phase Dependencies above.
2. T028's tombstone refresh runs LAST — it captures all post-rewrite edges in one commit per SC-007.
3. PR contains exactly two logical commits:
   - Code commit (`codeconv/src/codeconv/tools/discover/{parse,workflow,pubspec}.py` + the test files)
   - Tombstone refresh commit (`.codeconv/tombstones/...` only)
4. Single PR onto `main` per `docs/BRANCHING.md`. Same-day CalVer tag minted on merge per `docs/VERSIONING.md`.

---

## Notes

- [P] tasks = different files (or different test functions in the same file with no shared fixture state), no dependencies on incomplete tasks
- [Story] label maps task to its user story for traceability and independent verification
- Tests MUST be written and verified-failing BEFORE the corresponding implementation task starts (TDD per DISCIPLINE.md §2.4)
- Commit after each logical group (e.g. one commit for "all US1 tests added (failing)"; one commit for "extract_imports rewrite implementation"; one commit for "workflow threading"; etc.). Pure additions to test files can share a commit; mixed code+test commits are discouraged
- The tombstone refresh commit (T028) is the ONLY commit that touches `.codeconv/tombstones/`; do NOT let interim test runs accidentally rewrite tombstones into the commit-staged set
- Avoid: vague tasks, edits to feature 012's surfaces beyond the contract delta (parse.py, workflow.py, the new pubspec.py, and the four test files only)
