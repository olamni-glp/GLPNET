# Implementation Plan: codeconv-discover resolves `package:glp_runtime/...` self-imports as in-subtree edges

**Branch**: `014-package-self-import-resolution` | **Date**: 2026-05-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/014-package-self-import-resolution/spec.md`

## Summary

Feature 012 shipped `/codeconv-discover` with research note R12 unconditionally skipping every `package:` and `dart:` import target. That was correct for genuinely external dependencies but wrong for self-references: `glp_runtime_net/pubspec.yaml` declares `name: glp_runtime`, and the subtree's own files use `import 'package:glp_runtime/runtime/X.dart';` form — so 70 of 128 inventoried files (55%) currently appear as graph-isolated.

This feature layers a **self-package rewrite rule** on top of R12: at discover-start, read the subtree's `pubspec.yaml` once, cache the `name:` value, and rewrite every `package:<name>/<rest>` import target to `lib/<rest>` before the existing in-subtree resolution runs. The same rewrite is applied inside `workflow._scan_outside_callers` so outside-subtree files using the package form against the inside subtree raise proper `outside_caller` warnings instead of being silently dropped. Truly external `package:` and all `dart:` / `dart-ext:` targets continue to be skipped, with no warning.

**Technical approach** (validated against `codeconv/src/codeconv/tools/discover/parse.py` lines 132-181 and `workflow.py::_scan_outside_callers` lines 465-510):

1. Add a `package_name: str | None` parameter to `extract_imports(file_path, subtree_root, package_name)`. When non-None, prefix-match `package:<package_name>/` and rewrite to `lib/<rest>` BEFORE the existing relative-path resolution. When None, current behaviour preserved (every `package:` skipped).
2. Add a thin loader `read_package_name(subtree_root: Path) -> tuple[str | None, dict | None]` returning `(name, warning_or_None)`. Per-discover-run cached at `workflow.run_discover` entry.
3. Pass the cached `package_name` through `_process_one_file` → `extract_imports` and through `_scan_outside_callers`'s inline `_IMPORT_RE` loop.
4. On `pubspec.yaml` absent / unparseable / no `name:`: emit one summary warning `{"kind": "pubspec_missing", "path": "<expected>", "reason": "absent" | "unparseable" | "no_name_field"}`, fall back to feature-012 behaviour.

Net code touched: `parse.py` (~25 lines added), `workflow.py` (~30 lines added — one cache step + threading + the `_scan_outside_callers` rewrite), one new helper module `pubspec.py` (~30 lines). No DB schema change. No tombstone shape change. No new entities.

## Technical Context

**Language/Version**: Python 3.11+ (matches existing `codeconv/pyproject.toml` from feature 012)
**Primary Dependencies**: stdlib only for the rewrite logic (regex + `pathlib`); `pyyaml` (already pinned for tombstone read/write) for parsing `pubspec.yaml`
**Storage**: PGLite via the unified bridge — `codeconv.dart_files`, `codeconv.dart_imports`, `codeconv.dart_callers`, `codeconv.dart_files_orphaned`, `codeconv.discover_runs` — schema unchanged from feature 012
**Testing**: `pytest codeconv/tests/`. Bridge-needing tests gated by `@needs_bridge` (feature 012 contract). All tests serialised via `--test-concurrency=1` (PGLite cold-init ~7 s on Windows; per memory `project_pglite_cold_init_windows.md`)
**Target Platform**: Windows 11 primary (this checkout); cross-platform-portable Python (no Windows-only APIs in this delta)
**Project Type**: Python library + CLI inside the `codeconv/` subtree of a polyglot monorepo (Dart, Python, .NET, Node bridge)
**Performance Goals**: SC-006 carries forward feature 012's SC-013 — `/codeconv-discover` ≤ 60 s wall-clock on a fresh inventory of 128 files, ≤ 5 s on idempotent re-run. The added per-import work is one `dict[str].startswith(prefix)` check + one slice — sub-millisecond per import directive
**Constraints**: `--data-dir` override required on this checkout (D: is exFAT; per memory `project_012_codeconv_runner_status.md` and `docs/known-issues.md` Issue 8); FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching) must stay green — this feature touches only parser logic, not SQL/connection-string code
**Scale/Scope**: 128 `.dart` files in `glp_runtime_net/`; ~3-5 imports per file → expected ~400-600 in-subtree edges post-feature (vs 146 today); 1 `pubspec.yaml` per discover run

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` contains only template placeholders (`[PRINCIPLE_1_NAME]`, `[GOVERNANCE_RULES]`, etc.) — no concrete project principles have been ratified. Per the spec-first discipline in `CLAUDE.md` and `docs/DISCIPLINE.md` (which is the operative authority for this repo), the relevant gates for this feature are:

| Gate (from CLAUDE.md / DISCIPLINE.md) | Pass? | Note |
|---|---|---|
| §"Spec-First Development — No Implementation Without Spec" | PASS | spec.md present, fully clarified (3 Q&A entries 2026-05-11), checklist all green |
| DISCIPLINE.md §1.1 "Specification-First Development" | PASS | this plan derives entirely from spec FRs; no new behaviour invented here |
| DISCIPLINE.md §1.4 "Traceability" | PASS | each artefact below cites the spec FR and (where relevant) the feature-012 research note it supersedes/extends |
| DISCIPLINE.md §1.7 "Errors, not 'limitations' or 'issues'" | PASS | the 55%-isolated graph is named in the spec as an error to fix, not a "known limitation" |
| DISCIPLINE.md §2.2 "Test baseline before/after" | PASS by design | tasks.md will sequence baseline-pytest BEFORE code change and re-run AFTER each step |
| Feature 012 spec contract preserved (per spec.md "Assumptions" line 6) | PASS | R12 stays as written; this feature ADDS a layered rule with its own research notes (R14-R18 below) |

**Result**: GATE PASSED with no violations to justify; the Complexity Tracking table at the end is empty.

## Project Structure

### Documentation (this feature)

```text
specs/014-package-self-import-resolution/
├── plan.md                                  # This file (/speckit-plan output)
├── spec.md                                  # Feature spec (already written via /speckit-specify)
├── checklists/requirements.md               # Spec quality checklist (already passing)
├── research.md                              # Phase 0 output — R14-R18 (this run)
├── data-model.md                            # Phase 1 output — delta vs feature 012 (this run)
├── quickstart.md                            # Phase 1 output — Flow G self-package smoke (this run)
├── contracts/
│   ├── parser_contract.md                   # Phase 1 — extract_imports signature delta (this run)
│   └── workflow_contract.md                 # Phase 1 — _scan_outside_callers + pubspec cache contract (this run)
└── tasks.md                                 # Phase 2 output — /speckit-tasks (next chained command)
```

### Source Code (repository root)

This feature touches only the `codeconv/` Python package and writes one tombstone-refresh commit. No Dart, .NET, Node, or `glp_runtime/` change.

```text
codeconv/                                            # Python package — feature 012 surface
├── src/codeconv/tools/discover/
│   ├── parse.py                                     # MODIFY — add package_name kwarg + rewrite logic
│   ├── workflow.py                                  # MODIFY — read+cache pubspec; thread package_name; rewrite in _scan_outside_callers
│   ├── pubspec.py                                   # NEW — read_package_name(subtree_root) loader + warning shape
│   ├── walker.py                                    # UNCHANGED
│   └── tombstone.py                                 # UNCHANGED
└── tests/
    ├── test_parse.py                                # MODIFY — add resolves_self_package + external_package_still_skipped
    ├── test_pubspec.py                              # NEW — unit tests for read_package_name (absent / malformed / no name / happy)
    ├── test_outside_subtree_warning.py              # MODIFY — add a self-package outside-caller case
    ├── test_discover_self_package_e2e.py            # NEW — integration: heap_fcp.dart-style file → 4 deps via package: form
    └── conftest.py                                  # UNCHANGED

glp_runtime_net/                                     # Dart subtree — read-only by this feature
└── pubspec.yaml                                     # READ ONLY — name: glp_runtime (verified live 2026-05-11)

.codeconv/tombstones/                                # REFRESHED via the SC-007 single tombstone-refresh commit
```

**Structure Decision**: Single-project Python additions inside the existing `codeconv/` package. The new `pubspec.py` module isolates the YAML read so unit tests can cover it without spinning up the bridge. No new top-level directory introduced; no other language touched.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

(empty — Constitution Check passed without violations)

## Phase 0: Research outputs

See [research.md](./research.md) for:

- **R14**: Self-package rewrite as a layered rule on top of feature-012's R12 (decision, why, what it supersedes)
- **R15**: pubspec.yaml caching shape (per-run, in-memory, lifetime = `run_discover()` invocation)
- **R16**: Warning shape `{"kind": "pubspec_missing", "path", "reason"}` and the three reason values
- **R17**: Idempotence preservation under the new rewrite (FR-008 / SC-004)
- **R18**: Performance under the new rewrite (SC-006 carry-forward)

All NEEDS CLARIFICATION items raised by the plan template (now moot since the spec is fully clarified) are closed in research.md.

## Phase 1: Design artefacts

- **[data-model.md](./data-model.md)** — explicit delta against feature 012's `data-model.md`. **No new entities, no new columns, no new tombstone fields.** This document exists to be exhaustive about what does NOT change, so the analyzer can verify the spec's "no new entities" claim against the Phase 1 design.
- **[contracts/parser_contract.md](./contracts/parser_contract.md)** — `extract_imports(file_path, subtree_root, package_name=None)` signature delta vs feature 012; rewrite-rule pseudocode; ordering relative to existing `package:`/`dart:` skip; preservation of dedup + sort + relative-resolve fall-through.
- **[contracts/workflow_contract.md](./contracts/workflow_contract.md)** — pubspec read-and-cache contract at `run_discover` entry; threading of `package_name` through `_process_one_file`; rewrite inside `_scan_outside_callers`'s inline `_IMPORT_RE` loop; warning emission shape; idempotence guarantee.
- **[quickstart.md](./quickstart.md)** — Flow G (self-package smoke) added to the feature-012 quickstart's flows. Verifies SC-001 (`isolated < 20`), SC-002 (heap_fcp tombstone has 4 deps), SC-005 (missing-pubspec fallback warning), and the SC-007 single tombstone-refresh commit recipe.

The agent context file (`CLAUDE.md`) was updated this run to reference this plan between the existing `<!-- SPECKIT START -->` / `<!-- SPECKIT END -->` markers at `CLAUDE.md` lines ~536-540, replacing the prior reference to feature 012's plan.
