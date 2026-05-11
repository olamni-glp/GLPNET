# Feature Specification: codeconv-discover resolves `package:glp_runtime/...` self-imports as in-subtree edges

**Feature Branch**: `014-package-self-import-resolution`
**Created**: 2026-05-11
**Status**: Draft
**Input**: User description: "Feature: codeconv-discover resolves `package:glp_runtime/...` imports as in-subtree edges. ... (full prompt at `docs/future/014-speckit-specify-prompt.md`; background at `docs/future/codeconv-discover-package-self-import-resolution.md`)"

## Clarifications

### Session 2026-05-11

- Q: Does feature 012's perf SLO (SC-013) still apply after this feature lands? → A: Carry SC-013 forward unchanged (≤ 60 s fresh / ≤ 5 s idempotent on the 128-file `glp_runtime_net/`).
- Q: What `kind` string should the missing/malformed-pubspec warning use in the JSON summary? → A: Single kind `"pubspec_missing"`; the warning carries `path` and `reason` fields (`reason` is one of `"absent" | "unparseable" | "no_name_field"`).
- Q: When does this feature's PR refresh the 128 tombstones on `main`? → A: Single one-time refresh commit included in the feature PR. After the implementation lands on the branch, run `/codeconv-discover` once and commit the resulting tombstone diff so reviewers see the actual data delta and `main` is consistent immediately post-merge.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Developer sees the real import graph after running /codeconv-discover (Priority: P1)

A developer working in this repo runs `/codeconv-discover` and reviews the resulting `codeconv.dart_imports` table (or the per-file `dependencies:` field in tombstones) to understand who depends on whom. The graph today is misleading: 55% of files show as isolated because they use `package:glp_runtime/...` self-references that the discover tool unconditionally discards. After this feature, the graph faithfully reflects the source — files that import sibling `lib/...` modules via the package form are recorded as in-subtree edges.

**Why this priority**: This is the entire feature. Without it the inventoried import graph is unusable for any downstream analysis (call-graph queries, refactor planning, dead-code detection, the eventual Dart→C# translation). The current 55%-isolated state means most consumer queries against `codeconv.dart_imports` return nothing useful.

**Independent Test**: Pre-condition: run `/codeconv-discover` once, note the `isolated` count in the summary. Post-condition: re-run after this feature lands, verify the `isolated` count drops dramatically (target: < 20 for the current `glp_runtime_net/` checkout) and that the tombstone for `lib/runtime/heap_fcp.dart` lists its four real dependencies.

**Acceptance Scenarios**:

1. **Given** `glp_runtime_net/lib/runtime/heap_fcp.dart` contains `import 'package:glp_runtime/runtime/terms.dart';`, **When** `/codeconv-discover` runs, **Then** an edge `(lib/runtime/heap_fcp.dart, lib/runtime/terms.dart)` appears in `codeconv.dart_imports` AND `lib/runtime/terms.dart` appears in the tombstone's `dependencies:` list.
2. **Given** any file contains `import 'package:meta/meta.dart';` (truly external pub package), **When** `/codeconv-discover` runs, **Then** no edge is recorded — silently, with no warning.
3. **Given** `glp_runtime_net/pubspec.yaml` does not exist or cannot be parsed, **When** `/codeconv-discover` runs, **Then** discover emits exactly one warning, completes successfully, and falls back to the feature-012 behaviour (every `package:` import skipped, including self-references).

---

### User Story 2 - Outside-subtree warnings catch real cross-tree references (Priority: P2)

When a `.dart` file outside `glp_runtime_net/` (e.g. somewhere in `glp_runtime/` or `glp_multiagent/`) imports INTO `glp_runtime_net/`, today the discover tool's outside-subtree scanner only catches relative-path imports. Anything written as `import 'package:glp_runtime/X';` from outside is silently dropped — even though it IS an outside→inside dependency that violates the caller-graph-scope contract (FR-023 from feature 012). After this feature, the outside-subtree scan applies the same self-package rewrite, so these references generate proper `outside_caller` warnings.

**Why this priority**: Necessary for correctness of the cross-tree dependency picture — without it the warning channel under-reports real coupling. Lower than P1 because the repo currently has zero outside-subtree consumers of `glp_runtime_net/`; this prevents future false-negatives rather than fixing today's pain.

**Independent Test**: Create a synthetic outside-subtree file `glp_runtime/legacy.dart` with `import 'package:glp_runtime/runtime/heap_fcp.dart';`. Run discover. Verify the summary's warnings list contains an `outside_caller` entry naming both files. Verify no caller edge is recorded for the outside file.

**Acceptance Scenarios**:

1. **Given** an outside-subtree file imports `package:glp_runtime/X` into an inside-subtree file, **When** discover runs, **Then** the summary emits an `outside_caller` warning naming both files AND no row is added to `codeconv.dart_callers` for the outside file.
2. **Given** an outside-subtree file imports `package:meta/meta.dart` (genuinely external), **When** discover runs, **Then** no warning is emitted (matches feature-012 behaviour for external packages).

---

### Edge Cases

- **`pubspec.yaml` is missing**: Emit one warning naming the expected path; behave exactly as feature 012 (skip every `package:` import including self-references). Inventory still builds, just with the 55%-isolated graph.
- **`pubspec.yaml` exists but lacks `name:` field**: Treat as malformed; same fallback as missing.
- **`pubspec.yaml` `name:` field collides with a real external pub package name**: Out of scope. Self-package wins by definition since the rewrite is applied first; external `glp_runtime` is not currently used in this repo.
- **Import path is malformed**: `import 'package:glp_runtime/';` (no rest), `import 'package:glp_runtime';` (no slash), etc. — treat as unresolvable, skip silently (mirrors feature-012's behaviour for unparseable relative imports).
- **Import resolves outside the package's `lib/` directory**: For `package:glp_runtime/test/foo.dart`-style paths (only `lib/` is the package root per Dart convention). Per Dart's own rules these are invalid; we treat them as skippable and record nothing. No warning needed.
- **A file uses BOTH `package:glp_runtime/X` AND the equivalent relative path `../X` in different lines**: After rewriting, both resolve to the same `(from_path, to_path)` pair; deduplication (FR-007) keeps one row and emits one duplicate-import warning per FR-019 (feature 012).
- **`pubspec.yaml` is modified mid-discover-run**: The discover run that reads it caches the parsed name; if the file changes during the same run, the cached value wins for that run. Next run picks up the new value.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST resolve imports of the form `package:<PKG>/<rest>` where `<PKG>` matches the `name:` field of the subtree's `pubspec.yaml` by rewriting them to `lib/<rest>` (POSIX path relative to the subtree root) and recording the result as an in-subtree edge in `codeconv.dart_imports` and `codeconv.dart_callers`.
- **FR-002**: System MUST continue to skip `package:<other>/...` imports where `<other>` is any package name OTHER than the subtree's own — these are genuinely external dependencies (pub packages, the Dart SDK pub mirror, etc.).
- **FR-003**: System MUST continue to skip `dart:...` and `dart-ext:...` import targets in their entirety.
- **FR-004**: System MUST read each subtree's `pubspec.yaml` at most once per `/codeconv-discover` invocation (cached per-run); per-file resolution MUST NOT re-read the file.
- **FR-005**: When `pubspec.yaml` is absent OR unparseable OR lacks a `name:` field, system MUST emit exactly one warning to the discover summary with shape `{"kind": "pubspec_missing", "path": "<expected-pubspec-path>", "reason": "<absent|unparseable|no_name_field>"}` AND fall back to the feature-012 behaviour (skip every `package:` import, including would-be self-references). The discover run MUST still complete successfully and report the resulting inventory.
- **FR-006**: System MUST apply the same self-package rewrite to imports observed during the outside-subtree-caller scan. An outside-subtree file importing `package:<PKG>/X` (where `<PKG>` is the inside subtree's package name) MUST generate an `outside_caller` warning naming both files, exactly as relative-path outside callers do today (FR-023 from feature 012).
- **FR-007**: System MUST preserve the `UNIQUE (from_path, to_path)` constraint on `dart_imports` and `dart_callers`. If a single file imports the same target via both the package form AND a relative path, the rewrite collapses them to one row AND emits a duplicate-import warning (per FR-019 from feature 012).
- **FR-008**: System MUST preserve idempotence (FR-024 from feature 012). Re-running `/codeconv-discover` against an unchanged source state — INCLUDING after this feature lands and refreshes past tombstones — MUST produce zero diff in `codeconv.dart_files`, `dart_imports`, `dart_callers`, and `.codeconv/tombstones/` on the second consecutive run.
- **FR-009**: System MUST NOT change the on-disk encoding of `dart_imports.to_path` or tombstone `dependencies:` / `callers:` entries. Resolved package-form imports MUST appear as `lib/<rest>` (the same POSIX path relative to subtree root that relative-path-resolved imports use today). Downstream consumers see one uniform path shape regardless of how the import was written in the source.

### Key Entities *(no new entities introduced)*

This feature does NOT introduce new tables, new tombstone fields, or new on-disk artefacts. It changes the *content* of existing rows in `codeconv.dart_imports` / `codeconv.dart_callers` and the corresponding `dependencies:` / `callers:` lists in tombstones. The schema (from feature 012's `data-model.md`) is unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After this feature lands, re-running `/codeconv-discover` against the current `glp_runtime_net/` checkout drops the `isolated` count (files with no in-subtree imports AND no in-subtree callers) from 70 (current state, 55% of 128 files) to under 20 (target: under 16% of 128 files).
- **SC-002**: After this feature lands, the tombstone for `lib/runtime/heap_fcp.dart` lists exactly these four `dependencies:` entries: `lib/multiagent/variable_table.dart`, `lib/runtime/machine_state.dart`, `lib/runtime/suspension.dart`, `lib/runtime/terms.dart`. Verifiable by inspecting `.codeconv/tombstones/lib/runtime/heap_fcp.dart.md`.
- **SC-003**: After this feature lands, every test in the existing `pytest codeconv/tests/` suite still passes without modification. The suite count remains at least 39 passed (the three Phase 6/7 skips for perf opt-in and Windows symlinks remain). Net gain: at least 3 new tests cover the rewrite path (one positive parser unit test, one external-package-still-skipped negative parser unit test, one end-to-end integration test covering the workflow path).
- **SC-004**: Running `/codeconv-discover` twice in succession (after this feature lands) on unchanged source produces zero diff in `codeconv.dart_files`, `dart_imports`, `dart_callers`, AND `.codeconv/tombstones/`. Specifically, the second run's summary reports `files_skipped_idempotent == files_walked` and `files_processed == 0`.
- **SC-005**: When `glp_runtime_net/pubspec.yaml` is renamed or removed before a discover run, the run still completes successfully (exit 0) with exactly one warning in the summary's warnings list and the same isolated-graph behaviour as feature 012 (i.e. all `package:` imports skipped). Restoring the file and re-running yields the SC-001 isolated count again.
- **SC-006** (carried forward from feature 012 SC-013): `/codeconv-discover` against the current `glp_runtime_net/` checkout completes within **60 seconds wall-clock** on a fresh inventory AND within **5 seconds** on an idempotent re-run. The added per-import resolution work (one regex match + dict lookup per `import` directive) must not breach either bound. Gated by `pytest --run-perf`.
- **SC-007** (release workflow): The feature PR includes a single tombstone-refresh commit on the feature branch — run `/codeconv-discover` once after the implementation lands, commit the resulting `.codeconv/tombstones/` diff alongside the code, and let reviewers see the actual data delta in the PR. Post-merge `main` is immediately consistent; no follow-up manual step required.

## Assumptions

- The repo contains exactly one Dart package at `glp_runtime_net/` — i.e. exactly one `pubspec.yaml` whose declared package root maps to the `glp_runtime_net/lib/` directory. No pub workspace, no multi-package monorepo layout.
- The `pubspec.yaml`'s `name:` field is `glp_runtime` today and is stable across discover runs. If it changes, the next discover run picks up the new value.
- Dart's standard `package:<NAME>/X.dart` ↔ `<package_root>/lib/X.dart` mapping holds (it is part of the Dart pub specification, not a project convention).
- The existing `--data-dir` override from feature 013 is required when working on this repo (since `D:\` is exFAT — see `docs/known-issues.md` Issue 8). All tests and acceptance runs assume the override is supplied.
- This feature does NOT reopen the feature-012 contract. Research note R12 stays as written; this feature adds a layered "self-package rewrite" rule with its own research note in `specs/014-package-self-import-resolution/research.md` once `/speckit-plan` produces it.
- Per `docs/BRANCHING.md`, the feature merges to `main` via a single PR. Per `docs/VERSIONING.md`, the merge mints a same-day CalVer tag if applicable (e.g. `v2026.05.11-3` if landed today; otherwise `vYYYY.MM.DD`).
- Existing greps for FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching) stay clean — this feature only changes parser logic, not SQL or connection-string handling.
