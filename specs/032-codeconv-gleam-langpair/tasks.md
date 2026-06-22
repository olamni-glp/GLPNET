---
description: "Task list — codeconv Gleam langpair (Dart→Gleam)"
---

# Tasks: codeconv Gleam langpair (Dart→Gleam)

**Input**: Design documents from `/specs/032-codeconv-gleam-langpair/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/dart_gleam_hooks.md, quickstart.md

**Tests**: INCLUDED — FR-011 + the 016 contract's "Test obligations" require unit
coverage of the new pair. Pure unit (no `@needs_bridge`); written before
implementation per story.

**Organization**: by user story (US1 P1 MVP → US2 P2 → US3 P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable (different file, no incomplete-task dependency)
- Paths are repo-relative. Python: `codeconv/.venv` (`--test-concurrency=1`, `PYTHONUTF8=1`).

> **🔴 OWNER DECISION GATE (R-003) — precondition for `/bk-implement`.** The
> FR-005↔FR-008↔FR-003 collision tension (plan.md Complexity Tracking) must be
> ruled by the owner BEFORE implement: **R3-a** (default — zero stage edit,
> corpus collision-guarantee test) or **R3-b** (one generic uniqueness seam in
> `scaffold/planner.py` + a narrow SC-003 carve-out). The ruling affects **only**
> the collision-guard tail (T016 vs T017b); the rest (T001–T015, T018) is
> ruling-independent. Surfaced via `/bk-analyze`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: create the package + test scaffolding.

- [ ] T001 Create the `dart_gleam/` package skeleton — `__init__.py`, `source_dart.py`, `target_gleam.py`, `mirror_gleam.py` with module docstrings + `__all__` placeholders (no behavior) in codeconv/src/codeconv/langpairs/dart_gleam/
- [ ] T002 Create the pure-unit test module skeleton codeconv/tests/test_langpair_dart_gleam.py (imports `from codeconv import langpairs`, `REPO_ROOT`, no bridge marker; placeholder collected by pytest)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: implement all hooks + register the pair. **⚠️ No user story works until the pair is registered.**

- [ ] T003 [P] Implement source side — delegate all five source hooks (`source_extensions`, `tool_exclusion_globs`, `read_package_name`, `extract_imports`, `extract_leading_doc`) to `codeconv.tools.discover.{parse,pubspec,walker}` (R-001/FR-002; mirror `dart_csharp/source_dart.py` structure) in codeconv/src/codeconv/langpairs/dart_gleam/source_dart.py
- [ ] T004 Implement target side — `target_extension()==".gleam"`, `normalize_segment()` (data-model rule: identity on legal, lowercase/`_`/`g_`-prefix/reserved-suffix otherwise), `target_for()` (POSIX, verbatim dir mirror, ext swap, per-segment normalize), `workdir_name()=="__"+stem` in codeconv/src/codeconv/langpairs/dart_gleam/target_gleam.py
- [ ] T005 Implement mirror side — `mirror_prune_segments()` (Dart-tree set), `preserved_source_suffix()==""`, `companion_extensions()` (nine, `.gleam` for `.cs`, order fixed), `companion_stub_comment()` (`//` + `Gleam source` category), `tracker_filename()=="codeconv-gleam-tracker.json"` in codeconv/src/codeconv/langpairs/dart_gleam/mirror_gleam.py
- [ ] T006 Wire `class DartGleam(LangPair)` delegating to the three modules + `register(DartGleam())` at import (depends T003,T004,T005) in codeconv/src/codeconv/langpairs/dart_gleam/__init__.py
- [ ] T007 Add the single auto-import line `"codeconv.langpairs.dart_gleam"` to `_PRODUCTION_PAIR_MODULES` (the ONLY edit outside the new package — FR-005) (depends T006) in codeconv/src/codeconv/langpairs/__init__.py

**Checkpoint**: `langpairs.get("dart","gleam")` returns the registered pair; `list_pairs()==[("dart","csharp"),("dart","gleam")]`.

---

## Phase 3: User Story 1 - Run codeconv stages targeting Gleam (Priority: P1) 🎯 MVP

**Goal**: the pipeline runs end-to-end for `(dart, gleam)`; default `(dart, csharp)` unaffected.
**Independent Test**: bind workspace to `(dart, gleam)`, run scaffold+mirror over a small Dart subtree → complete Gleam-targeted tree; pair not selected → unchanged Dart→C# output.

- [ ] T008 [P] [US1] Registry/selectability + identity + refusal tests (`list_pairs()` has both; `get` returns it; `UnknownLangPair` for absent names BOTH pairs; **`PairMismatch` when bound to `(dart,gleam)` with a disagreeing override** — FR-007/SC-005 mismatch half; `key()`, `source_extensions()`, `target_extension()`) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T009 [P] [US1] Source-parity tests — `extract_imports`/`extract_leading_doc`/`tool_exclusion_globs` equal `dart_csharp` AND `tools/discover` on a tmp fixture (FR-002) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T010 [US1] End-to-end structure smoke (quickstart §4, bridge): throwaway workspace bound to `(dart, gleam)` over a small Dart subtree → scaffold+mirror emit `.gleam` targets + companion set + `codeconv-gleam-tracker.json`; confirm default `(dart, csharp)` output unchanged (SC-001/SC-002). Manual/quickstart verification, `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`.

**Checkpoint**: MVP — pipeline runs for the new target; US1 tests green.

---

## Phase 4: User Story 2 - Faithful Gleam target conventions (Priority: P2)

**Goal**: output follows Gleam conventions — `.gleam` ext, legal module segments, `//` companion comments, pair-defined tracker.
**Independent Test**: inspect output — every target uses `.gleam`, every module segment is a legal Gleam identifier, stubs use Gleam comment syntax, root tracker present.

- [ ] T011 [P] [US2] `target_for` positive tests — legal paths (identity + ext swap), nested dirs, Windows-sep input → POSIX (FR-003 AS-2) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T012 [P] [US2] Segment-normalization tests — uppercase, leading-digit, hyphen/punctuation, reserved-word: each → matches `^[a-z][a-z0-9_]*$` AND non-reserved (SC-004); already-legal basename preserved unchanged (FR-003 AS-2/FR-008) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T013 [P] [US2] Mirror-hook exact-value tests — prune set, `""` suffix, nine companions (order), `//` stub + `Gleam source` category, tracker literal `codeconv-gleam-tracker.json` (FR-004) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T014 [US2] Pin `_GLEAM_RESERVED` to the Gleam 1.17.0 keyword set (cite the 1.17.0 language reference in a comment) + finalize `normalize_segment` edge handling so T011–T013 pass (depends T004) in codeconv/src/codeconv/langpairs/dart_gleam/target_gleam.py

**Checkpoint**: conventions verified; 100% legal segments (SC-004).

---

## Phase 5: User Story 3 - Extensibility proof, zero stage-tool change (Priority: P3)

**Goal**: adding the pair touches only the new package + one registry line; suite stays green; pair covered.
**Independent Test**: diff shows only langpair-area + one registry line; full codeconv suite green with Dart→C# unchanged.

- [ ] T015 [P] [US3] SC-003 structural-proxy test — `tools/{init,discover,depgraph,scaffold}` still import + expose `app`; `discover.workflow` still resolves source hooks via the registry (mirror `test_langpair_registry.py` obligation 5) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T016 [US3] Corpus no-collision test (R3-a default) — over the authoritative `glp_runtime/` source set, `target_for` produces no two equal targets (FR-008 guarantee for the production corpus) in codeconv/tests/test_langpair_dart_gleam.py
- [ ] T017a [US3] Run the FULL codeconv suite green incl. unchanged `test_langpair_registry.py` (FR-006/SC-002), then SC-003 diff check `git diff --name-only` shows ONLY `langpairs/dart_gleam/**` + one `langpairs/__init__.py` line + the new test + `specs/032/**`
- [ ] T017b [US3] **IF owner rules R3-b**: add ONE generic target-uniqueness assertion to `plan_target_tree` (raise actionable error on duplicate `target_rel`/`workdir_rel`, write nothing) + a planner-collision unit test, and restate SC-003 as "zero *pair-specific* stage edits" in codeconv/src/codeconv/tools/scaffold/planner.py (BLOCKED on R-003 ruling = R3-b; SKIP under R3-a)

**Checkpoint**: extensibility proven; regression-free.

---

## Phase 6: Polish & Cross-Cutting

- [ ] T018 Record the R-003 ruling outcome: under R3-a, add the normalization-collision limitation to docs/known-issues.md; under R3-b, confirm the SC-003 wording amendment is reflected in spec.md/contract (depends owner ruling)
- [ ] T019 [P] Run quickstart.md §1–§5 validation end-to-end; confirm SC-001..SC-005 in specs/032-codeconv-gleam-langpair/quickstart.md
- [ ] T020 Ship-prep: baseline-green confirmation + commit-scope check (only this feature's files), ready for buildkit GitFlow (NOT a hand-merge to main)

---

## Dependencies & Execution Order

- **Setup (T001–T002)**: no deps.
- **Foundational (T003–T007)**: T003/T004/T005 [P] → T006 → T007. BLOCKS all stories.
- **US1 (T008–T010)**: after Foundational. T008/T009 [P]; T010 after the pipeline wiring.
- **US2 (T011–T014)**: after Foundational. T011/T012/T013 [P] (tests) → T014 (impl makes them pass). Independent of US1.
- **US3 (T015–T017)**: after Foundational + US2 (normalization in place for the corpus test). T015 [P]; T016 after T014; T017a last; T017b only if R3-b.
- **Polish (T018–T020)**: after all desired stories.

### Within each story

- Tests written first and FAIL before implementation (T008/T009 fail until T006/T007; T011–T013 fail until T014).
- Source/target/mirror modules (T003–T005) before wiring (T006) before registry line (T007).

### Parallel opportunities

- T003 ∥ T004 ∥ T005 (different files).
- T008 ∥ T009 (US1 tests); T011 ∥ T012 ∥ T013 (US2 tests); T015 standalone.

---

## Parallel Example: Foundational

```bash
# After T001/T002, the three hook modules are independent files:
Task: "Implement source_dart.py (delegate to tools/discover)"        # T003
Task: "Implement target_gleam.py (.gleam ext + normalize + target_for)"  # T004
Task: "Implement mirror_gleam.py (prune/suffix/companions/stub/tracker)"  # T005
```

---

## Implementation Strategy

### MVP First (US1)
1. Setup (T001–T002) → 2. Foundational (T003–T007) → 3. US1 (T008–T010) → **STOP & VALIDATE**: pipeline runs for `(dart, gleam)`, Dart→C# unchanged.

### Incremental
US1 (pipeline runs) → US2 (faithful conventions + normalization legality) → US3 (extensibility/regression proof). Each increment is independently testable; the suite stays green throughout (Principle VII).

### R-003 gate
Resolve the owner ruling (R3-a vs R3-b) at the `/bk-analyze` remediation gate BEFORE `/bk-implement`. T001–T015 + T018 are ruling-independent; only T016/T017b depend on it.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- All hook tests are pure unit — NO `@needs_bridge` (FR-009). T010 is the only bridge-touching step (a quickstart smoke, not a unit test).
- Commit per task/logical group, files-by-name only (Principle VII / commit-scope discipline).
- Baseline the suite green before starting and re-run after each change.
