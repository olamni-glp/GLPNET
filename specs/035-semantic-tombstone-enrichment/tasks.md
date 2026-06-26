---
description: "Task list for Semantic Tombstone Enrichment (feature 035)"
---

# Tasks: Semantic Tombstone Enrichment

**Input**: Design documents from `/specs/035-semantic-tombstone-enrichment/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓

**Tests**: INCLUDED — the spec's "User Scenarios & Testing" is mandatory and
SC-002/003/004/007 each demand a verifying test; every contract names its tests.

**Organization**: by user story (US1 P1 MVP → US2 P2 → US3 P3), each
independently testable.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: different files, no incomplete-task dependency → parallelizable.
- File paths are repo-relative.

## Path Conventions
codeconv harness (single project): tool at
`codeconv/src/codeconv/tools/enrich/`, edits in
`codeconv/src/codeconv/tools/discover/`, migration in
`codeconv/src/codeconv/db/migrations/versions/`, tests in `codeconv/tests/`.
Run tests via `codeconv/.venv/Scripts/python.exe -m pytest`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the auto-discovered tool skeleton + the no-API seam + shared test doubles.

- [X] T001 [P] Create the enrich tool package skeleton `codeconv/src/codeconv/tools/enrich/__init__.py` — export `app: typer.Typer` (help "Enrich blank tombstone purpose/key_idea via the Claude seam") with a `run` command that delegates to `run_enrich`, plus a no-op `register_workflows(dbos_app)` and `__all__ = ["app","register_workflows"]` (per contracts/enrich_cli.md). Confirm `tool_registry()` auto-discovers it with NO edit to `runner.py`/`cli.py`.
- [X] T002 [P] Define the inference seam in `codeconv/src/codeconv/tools/enrich/seam.py` — frozen dataclasses `InferRequest(rel_path, source_text)` / `InferResult(purpose, key_idea, grounded, reason)`, the `InferFn` alias, and `_require_fn(fn, name)` that raises `RuntimeError` (the "drive me through /codeconv-enrich; NO external-API default" message) when `fn is None` (mirror `tools/codegen_opt/optimize.py:100-117`; per contracts/infer_seam.md).
- [X] T003 [P] Add shared enrich test doubles to `codeconv/tests/conftest.py` — a deterministic fake `infer_fn` (returns grounded, distinct purpose/key_idea from the source; NO network) and a forced-raise `infer_fn`, reusing the existing `@needs_bridge` marker + `run_codeconv()` helper.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: DB columns + tombstone frontmatter keys must exist before any provenance is written.

**⚠️ CRITICAL**: Blocks all user-story persistence. The new `0011` migration makes `heads == ["0011"]`, which would immediately fail the existing `0010` head test — so T005 ships WITH T004.

- [X] T004 Create additive migration `codeconv/src/codeconv/db/migrations/versions/0011_enrich_provenance.py` (revision `0011`, down_revision `0010`): add `purpose_source` + `key_idea_source` `text NOT NULL DEFAULT 'absent'` to `codeconv.dart_files` (`IF NOT EXISTS`), backfill `CASE WHEN <value>='' THEN 'absent' ELSE 'doc'`, `downgrade()` drops both — per contracts/migration_0011.md.
- [X] T005 Add `codeconv/tests/test_migration_0011_single_head.py` (assert `get_heads() == ["0011"]` + linear chain `0011→0010→…→0001`). Neutralize the now-stale head assertion in `codeconv/tests/test_migration_0010_single_head.py` — do NOT leave a `0010`-named file asserting `["0011"]`: either delete it (superseded by the 0011 test) or repurpose it to assert only that `0010` is a non-head link in the chain. Update `test_migration_single_head.py` too if it hardcodes a head number. (Depends T004) (analyze D1)
- [X] T006 Extend the tombstone frontmatter contract in `codeconv/src/codeconv/tools/discover/tombstone.py`: append `purpose_source`, `key_idea_source` to the END of `_FIELD_ORDER`, define `_FEATURE_035_KEYS = ("purpose_source","key_idea_source")`, and add it to `_PRESERVED_APPENDED_KEYS` (per data-model.md §2). Existing `_FIELD_ORDER` positions unchanged.

**Checkpoint**: schema + frontmatter keys exist; provenance can be persisted.

---

## Phase 3: User Story 1 — Fill blank tombstones with inferred semantics (Priority: P1) 🎯 MVP

**Goal**: For each in-scope blank-doc Dart file, infer a non-blank `purpose` and a DISTINCT `key_idea` via the Claude seam and persist them (tombstone + DB) marked `inferred`, sha256 unchanged.

**Independent Test**: Run enrich on `lib/compiler/codegen.dart`; its tombstone gains non-blank `purpose`, a distinct source-grounded `key_idea`, `*_source: inferred`, checksum unchanged; a doc'd file is untouched.

### Tests for User Story 1 ⚠️ (write first, ensure they fail)

- [X] T007 [P] [US1] Integration test `codeconv/tests/test_enrich_blank_inference.py` — blank candidate → tombstone+DB gain non-blank `purpose`, `key_idea` ≠ `purpose` (SC-005), `*_source: inferred`, `sha256` unchanged (Acceptance 1); doc'd file left unchanged (Acceptance 2); provenance distinguishes inferred vs doc (Acceptance 3 / SC-006). Uses the fake `infer_fn`.
- [X] T008 [P] [US1] Test `codeconv/tests/test_enrich_no_api_seam.py` — bare `codeconv enrich run` (no injected `infer_fn`) exits 2 with the skill-drive message; assert no `openai`/`litellm`/`OPENAI_API_KEY` import reachable on the enrich path (SC-004).

### Implementation for User Story 1

- [X] T009 [US1] Implement candidate enumeration in `codeconv/src/codeconv/tools/enrich/workflow.py` `run_enrich(repo_root, *, infer_fn=None, paths=None, dry_run=False, …)`: acquire the SHARED bridge via `acquire_or_discover(...)` + `build_engine(...)`; select in-scope, non-orphan tombstones whose `purpose`/`key_idea` is blank (`*_source == absent`). (Depends T002, T006)
- [X] T010 [US1] Implement per-candidate infer+write in `workflow.py`: read the file's CURRENT source, `infer = _require_fn(infer_fn, "infer_fn")`, call it, accept a grounded result, and write `purpose`/`key_idea` + `purpose_source/key_idea_source = inferred` to BOTH the tombstone (`write_tombstone`, preserving `_FIELD_ORDER` + appended keys) and `UPDATE codeconv.dart_files` — one `engine.begin()` transaction per file (FR-002/004/015). (Depends T009)
- [X] T011 [US1] Implement in-scope non-candidate provenance stamping in `workflow.py`: write `purpose_source`/`key_idea_source` (`doc`/`absent` from blank-ness) into doc'd/absent tombstones WITHOUT altering their `purpose`/`key_idea` text (research R-008 — markdown⇔DB agreement, FR-004; does not violate FR-006). (Depends T010)
- [X] T012 [US1] Wire the `run` command in `enrich/__init__.py` to `run_enrich` (thread `infer_fn` through; `--dry-run` mutates nothing) and emit a basic summary; the CLI catches the seam `RuntimeError` → exit 2 (mirror `codegen_opt/__init__.py:120-129`). (Depends T010)

**Checkpoint**: blank tombstones filled with provenance-marked, source-grounded semantics — MVP demoable.

---

## Phase 4: User Story 2 — Idempotent, change-aware, non-clobbering re-runs (Priority: P2)

**Goal**: A no-change re-run does zero inference and is byte-identical; a changed file re-infers; a later `discover` never erases inferred values.

**Independent Test**: Run enrich twice (no change) → 2nd run 0 inferences, byte-identical tombstones; then `discover` → inferred values survive.

### Tests for User Story 2 ⚠️

- [ ] T013 [P] [US2] Test `codeconv/tests/test_enrich_idempotence.py` — enrich twice with no source change: 2nd run performs zero `infer_fn` calls and the tombstone set is byte-identical (SC-002); a file whose source changed is re-inferred (Acceptance 2).
- [ ] T014 [P] [US2] Test `codeconv/tests/test_discover_preserves_inferred.py` — (a) `discover` re-run on an unchanged enriched file preserves `purpose`/`key_idea`/`*_source: inferred` (SC-003, 100%); (b) source change → `discover` re-seeds + resets `*_source` (FR-007); (c) drop the `dart_files` row (rebuilt inventory) → `discover` restores inferred values from the tombstone, not blanks them.

### Implementation for User Story 2

- [ ] T015 [US2] Implement enrich idempotence + stale guard in `workflow.py`: a non-blank (`inferred`/`doc`) field is skipped (no inference); if a tombstone's recorded `sha256` ≠ current file hash → skip-and-warn (do NOT infer from stale metadata) (FR-007 + edge case). (Depends T010)
- [ ] T016 [US2] discover seed sets provenance in `codeconv/src/codeconv/tools/discover/workflow.py`: on the mechanical seed (`workflow.py:527-528`), set `purpose_source`/`key_idea_source` = `doc` if `extract_leading_doc` non-empty else `absent`; extend the UPSERT column list + `ON CONFLICT DO UPDATE SET` (`workflow.py:547-569`) with the two source columns. (Depends T006)
- [ ] T017 [US2] discover conditional inferred-preservation on re-write in `discover/workflow.py` (per contracts/discover_preservation.md): before seeding, read the existing tombstone's `*_source` + recorded `sha256`; when `*_source == inferred` AND sha unchanged → carry forward existing value + `inferred`; when the `dart_files` row is absent but the tombstone holds inferred + unchanged sha → restore inferred into the new row. (Depends T016)

**Checkpoint**: re-runs are idempotent and never clobber inferred values.

---

## Phase 5: User Story 3 — Bounded, observable, fault-isolated runs (Priority: P3)

**Goal**: Scope by path, report candidate/enriched/skipped/failed counts + durable log, and isolate per-file failures (a failure never corrupts a tombstone).

**Independent Test**: Enrich scoped to one subdir with one file forced to fail → scoped subset processed, failing file's tombstone unchanged, failure reported, counts accurate.

### Tests for User Story 3 ⚠️

- [ ] T018 [P] [US3] Test `codeconv/tests/test_enrich_scope_and_faults.py` — `--path` narrows candidates + counts (Acceptance 1); a forced-raise `infer_fn` leaves that file's tombstone + `dart_files` row unchanged and lists the failure while others still enrich (SC-007 / Acceptance 2); summary emits candidates/enriched/skipped/failed (+ low_confidence) and a durable log (Acceptance 3 / FR-011).

### Implementation for User Story 3

- [ ] T019 [US3] Implement the `--path` scope filter (repeatable) in `workflow.py` + `enrich/__init__.py`; default = all blank candidates excl. `.orphaned/` (FR-012/013). (Depends T009)
- [ ] T020 [US3] Implement per-file fault isolation + low-confidence handling in `workflow.py`: wrap each file's infer+write in try/except → `failed` outcome with reason, tombstone unchanged (FR-010); reject `grounded == False`/whitespace-only/over the `MAX_PURPOSE_CHARS`(200)/`MAX_KEY_IDEA_CHARS`(320) caps from `seam.py` → `low_confidence`, tombstone unchanged (FR-009, analyze B1); continue with remaining candidates. (Depends T010)
- [ ] T021 [US3] Implement the run summary + `--json` + durable run log in `workflow.py`/`enrich/__init__.py` per data-model.md §6: emit the FR-011 four counts `candidates/enriched/skipped/failed` (+ sub-counts `low_confidence`, `skipped_non_candidate`) and a `failures[]` list, with `candidates == enriched + skipped + low_confidence + failed` (SC-001); write the full summary + per-file outcomes to a durable file `.codeconv/enrich-runs/<run-id>.json` (NO new DB table — analyze C1). (Depends T020)

**Checkpoint**: scoping, observability, and fault isolation complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T022 [P] Run quickstart.md end-to-end (migrate → `--dry-run` → scoped enrich → verify `lib/compiler/codegen.dart` tombstone gains inferred provenance + DB agreement), and confirm the inferred `purpose`/`key_idea` + `*_source` appear in `git diff` of the `.codeconv/tombstones/` tree (FR-014 git-reviewability — analyze E1).
- [ ] T023 Full green: `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/test_enrich_*.py codeconv/tests/test_discover_*.py codeconv/tests/test_migration_0011_single_head.py -q`, then a baseline regression check of the wider codeconv suite (track known pre-existing reds separately).
- [ ] T024 [P] Belt-and-suspenders SC-004 guard: grep `codeconv/src/codeconv/tools/enrich/` for `openai`/`litellm`/`OPENAI_API_KEY` → must be zero (Constitution V machine-check).

---

## Dependencies & Execution Order

### Phase dependencies
- **Setup (P1)**: no deps — start immediately.
- **Foundational (P2)**: after Setup — **BLOCKS all user stories** (schema + frontmatter keys).
- **US1 (P3 phase)**: after Foundational. **MVP.**
- **US2 / US3**: after Foundational; build on US1's `workflow.py` core (T010) so in practice US1 → US2 → US3, but each is independently testable.
- **Polish (P6)**: after the desired stories.

### Story dependencies
- US1: independent (needs only Foundational).
- US2: extends discover + enrich idempotence; shares `enrich/workflow.py` (T010) — not file-parallel with US1 impl.
- US3: scope/fault/summary over the same `enrich/workflow.py` core.

### Within a story
- Tests written first and failing → implementation. Models/contracts (seam, migration, frontmatter) before services (workflow) before CLI wiring.

### Parallel opportunities
- T001/T002/T003 (different files) in parallel.
- T007/T008 (different test files) in parallel; T013/T014 in parallel; (T018 alone).
- Across stories, `enrich/workflow.py` is a serialization point (T009→T010→T011/T015/T019/T020/T021 touch it) — these are NOT [P] with each other. `discover/workflow.py` edits (T016→T017) serialize too.

## Parallel Example: User Story 1
```text
# Tests together (different files):
Task: T007 test_enrich_blank_inference.py
Task: T008 test_enrich_no_api_seam.py
```

## Implementation Strategy
- **MVP = US1** (T001–T012): blank tombstones filled, provenance-marked, no-API seam enforced. Stop & validate (independent test) → demoable.
- **Incremental**: + US2 (idempotence + discover preservation) → + US3 (scope/observability/faults) → Polish.
- **Test cadence**: baseline-green before, re-test after each task/logical group (Constitution VII); add regressions to the codeconv suite.

## Notes
- [P] = different files, no incomplete-task dependency.
- The ONLY edits to existing code are the scoped, provenance-aware `discover` changes (T006/T016/T017) + the migration head-test update (T005); the runner/CLI registry is untouched (FR-016).
- All LM inference is the injected Claude `infer_fn` — there is no in-process/external LM backend (Constitution V / SC-004).
- Commit per task or logical group; commit only feature files (Constitution VII).
