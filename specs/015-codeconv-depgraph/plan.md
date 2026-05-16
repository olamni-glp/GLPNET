# Implementation Plan: codeconv-depgraph — topologically sorted Dart dependency graph and conversion-readiness oracle

**Branch**: `015-codeconv-depgraph` | **Date**: 2026-05-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/015-codeconv-depgraph/spec.md` (fully clarified, Session 2026-05-11)

## Summary

Add a Python tool `codeconv depgraph` (auto-discovered by feature-012's runner registry) and a thin slash-command wrapper `/codeconv-depgraph` that consume the existing in-subtree import graph at `codeconv.dart_imports` / `codeconv.dart_files` (populated by `/codeconv-discover`, completed by feature 014's self-package fix) and emit a Tarjan-SCC-condensed topological ordering plus a four-state per-file readiness flag (`pending` / `ready` / `in_progress` / `converted`). The ordering is persisted to a new schema table `codeconv.dart_depgraph` and a JSON artefact `.codeconv/depgraph.json`. A second new table `codeconv.dart_conversions` (two-phase: `started_at`, `completed_at`) tracks conversion lifecycle; two write subcommands `mark-started` and `mark-completed` populate it. A third subcommand `stamp-tombstones` round-trips depgraph + conversion state into `.codeconv/tombstones/*.dart.md` YAML frontmatter for rebuild-from-tombstones parity with feature 012's FR-022.

**Technical approach** (validated against `codeconv/src/codeconv/db/migrations/versions/0001_codeconv_schema.py`, `codeconv/src/codeconv/runner.py::tool_registry`, `codeconv/src/codeconv/tools/discover/{__init__.py,workflow.py,tombstone.py}`, and `.claude/skills/codeconv-discover/SKILL.md`):

1. **New Alembic revision** `0002_dart_depgraph.py` adds two tables under the existing `codeconv` schema: `dart_depgraph` (per-file ordering row, `path` PK FK) and `dart_conversions` (per-file two-phase state, `path` PK FK). Optionally adds `depgraph_runs` (mirrors `discover_runs`). All migration content is `CREATE TABLE IF NOT EXISTS` and a single `DROP TABLE IF EXISTS … CASCADE` on downgrade — schema isolation (FR-007 / SC-007) preserved.
2. **New tool subpackage** `codeconv/src/codeconv/tools/depgraph/` registered automatically by the runner's `pkgutil.iter_modules` scan (feature 012 FR-006 / `codeconv/src/codeconv/runner.py:85-133`). Modules: `__init__.py` (Typer app + `register_workflows`), `algorithm.py` (Tarjan SCC + condensation topo sort), `workflow.py` (compute, mark, stamp orchestration; bridge acquire), `tombstone_writer.py` (read–modify–write of `.codeconv/tombstones/*.dart.md` preserving feature-012 field order and YAML emitter settings).
3. **CLI surface**: `codeconv depgraph [compute|mark-started|mark-completed|stamp-tombstones|rebuild-conversions-from-tombstones]`. `compute` is the default (no-arg `codeconv depgraph` invokes it). Read-only flags (`--json`, `--quiet`, `--dry-run`, `--json-out`) mirror `/codeconv-discover`'s shape (FR-012 / FR-013).
4. **Slash skill** `.claude/skills/codeconv-depgraph/SKILL.md` is a thin wrapper that forwards arguments verbatim to `codeconv depgraph` — pattern verbatim from `.claude/skills/codeconv-discover/SKILL.md`.
5. **Tombstone round-trip** adds five YAML keys (`topo_level`, `cycle_group_id`, `status`, `conversion_started_at`, `conversion_completed_at`) APPENDED to the existing field-order tuple in `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` — preserving the feature-012 / -014 idempotence guarantee (write-canonical YAML; sorted lists; pinned key order).

Net code touched: ~80–120 lines of Python in `codeconv/src/codeconv/tools/depgraph/` (new), ~10 lines in `tombstone.py` (extend `_FIELD_ORDER` and the writer's null-vs-missing handling), 1 new Alembic revision file (~80 lines), 1 new `SKILL.md` (~60 lines, structurally copied from discover). No change to feature-012 / -014 surfaces beyond the tombstone field-order tuple.

## Technical Context

**Language/Version**: Python 3.11+ (matches existing `codeconv/pyproject.toml` from feature 012)
**Primary Dependencies**: stdlib only for the algorithm (recursion or iterative Tarjan; no `networkx` / `scipy`); `sqlalchemy>=2.0` and `psycopg[binary]` (already vendored in for feature 012); `PyYAML` (already vendored in)
**Storage**: PGLite via the unified bridge — `codeconv.dart_files` (read), `codeconv.dart_imports` (read), `codeconv.dart_conversions` (NEW — read+write via subcommands), `codeconv.dart_depgraph` (NEW — write), `codeconv.depgraph_runs` (NEW, optional but planned), all under the `codeconv` schema (SC-007 / FR-007 schema isolation preserved)
**Testing**: `pytest codeconv/tests/`. Bridge-needing tests gated by `@needs_bridge` (feature 012 contract). All tests serialised via `--test-concurrency=1` (PGLite cold-init ~7 s on Windows; per memory `project_pglite_cold_init_windows.md`)
**Target Platform**: Windows 11 primary (this checkout); cross-platform-portable Python; no Windows-only APIs in this delta
**Project Type**: Python library + CLI inside the `codeconv/` subtree of a polyglot monorepo (Dart, Python, .NET, Node bridge)
**Performance Goals**: SC-001 — `/codeconv-depgraph` ≤ 5 s on a warm bridge, ≤ 15 s on a cold-bridge first run, against the current 128-file / 443-edge baseline. The added work is one PGLite read of ~571 rows + an in-memory Tarjan SCC (O(V+E), trivially sub-second) + one bulk UPSERT of 128 rows + one ~30 KB JSON write
**Constraints**: `--data-dir` override required on this checkout (D: is exFAT — `project_012_codeconv_runner_status.md` and `docs/known-issues.md` Issue 8). FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching) carry forward from feature 012 — this feature touches no SQL beyond `SELECT ... FROM codeconv.dart_imports`, plain `INSERT ... ON CONFLICT DO UPDATE` against the two new tables, and a single `DELETE FROM codeconv.dart_depgraph` at the start of each compute (atomic-per-run, FR-008)
**Scale/Scope**: 128 inventoried files, 443 in-subtree edges, ≥ 6 isolated nodes today (post-feature-014). Expected SCC count = 128 singletons + 0–2 multi-file SCCs (the live import graph is likely acyclic; multi-file SCCs would be reported as `cycle_count > 0`). 1 new schema (no — reuses `codeconv`), 2 new tables (3 with optional `depgraph_runs`), 0 new Python dependencies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` contains only template placeholders (`[PRINCIPLE_1_NAME]`, `[GOVERNANCE_RULES]`, etc.) — no concrete project principles have been ratified. Per the spec-first discipline in `CLAUDE.md` and `docs/DISCIPLINE.md` (the operative authority for this repo), the relevant gates for this feature are:

| Gate (from CLAUDE.md / DISCIPLINE.md) | Pass? | Note |
|---|---|---|
| §"Spec-First Development — No Implementation Without Spec" | PASS | spec.md present, fully clarified (2 Q&A entries 2026-05-11), checklist all green |
| DISCIPLINE.md §1.1 "Specification-First Development" | PASS | this plan derives entirely from spec FRs; no new behaviour invented here |
| DISCIPLINE.md §1.4 "Traceability" | PASS | each artefact below cites the spec FR and (where relevant) the feature-012/-014 mechanism it extends |
| DISCIPLINE.md §1.7 "Errors, not 'limitations' or 'issues'" | PASS | the "no-ordering" / "no-frontier" gaps are named in the spec as feature gaps, not "limitations" |
| DISCIPLINE.md §2.2 "Test baseline before/after" | PASS by design | tasks.md will sequence baseline-pytest BEFORE code change and re-run AFTER each step |
| Feature 012 spec contract preserved (FR-015 schema isolation; FR-022 tombstone round-trip; FR-006 auto-discovery) | PASS | new tables stay in `codeconv` schema; `stamp-tombstones` + `rebuild-conversions-from-tombstones` provide round-trip; tool registers by file-system convention |
| Feature 014 surface preserved | PASS | this feature reads `dart_imports` after feature 014's self-package fix; does not re-open `extract_imports` or `_scan_outside_callers` |

**Result**: GATE PASSED with no violations to justify; the Complexity Tracking table at the end is empty.

## Project Structure

### Documentation (this feature)

```text
specs/015-codeconv-depgraph/
├── plan.md                                  # This file (/speckit-plan output)
├── spec.md                                  # Feature spec (already written, fully clarified)
├── checklists/requirements.md               # Spec quality checklist (already passing)
├── research.md                              # Phase 0 output — R1-R10 (this run)
├── data-model.md                            # Phase 1 output — two new tables + optional third (this run)
├── quickstart.md                            # Phase 1 output — Flow H (depgraph end-to-end) (this run)
├── contracts/
│   ├── depgraph_algorithm.md                # Phase 1 — Tarjan SCC + condensation + level assignment
│   ├── depgraph_cli.md                      # Phase 1 — CLI subcommand surface (compute / mark-* / stamp / rebuild)
│   ├── depgraph_schema.md                   # Phase 1 — DDL contract for the new tables
│   └── tombstone_format_delta.md            # Phase 1 — five new YAML keys; field-order extension; idempotence proof
└── tasks.md                                 # Phase 2 output — /speckit-tasks (next chained command)
```

### Source Code (repository root)

This feature touches only `codeconv/` and `.claude/skills/`. No Dart, .NET, Node, or `glp_runtime/` change.

```text
codeconv/                                              # Python package — feature 012 surface
├── src/codeconv/
│   ├── tools/
│   │   └── depgraph/                                  # NEW — new tool subpackage (auto-discovered by runner.py)
│   │       ├── __init__.py                            # NEW — Typer app, exports `app`, `register_workflows`
│   │       ├── algorithm.py                           # NEW — pure-stdlib Tarjan SCC + condensation topo sort
│   │       ├── workflow.py                            # NEW — orchestrator: read graph → compute → write DB + JSON
│   │       ├── tombstone_writer.py                    # NEW — five-key stamp/rebuild helpers
│   │       └── json_writer.py                         # NEW — canonical JSON emitter (sorted keys, stable order)
│   ├── tools/discover/
│   │   └── tombstone.py                               # MODIFIED — extend _FIELD_ORDER with five new keys (after sha256)
│   └── db/migrations/versions/
│       └── 0002_dart_depgraph.py                      # NEW — Alembic revision adding dart_depgraph, dart_conversions, depgraph_runs
└── tests/
    ├── test_depgraph_algorithm.py                     # NEW — pure unit tests for Tarjan + condensation (no bridge needed)
    ├── test_depgraph_compute.py                       # NEW — @needs_bridge: end-to-end against synthetic fixture
    ├── test_depgraph_mark.py                          # NEW — @needs_bridge: mark-started / mark-completed semantics
    ├── test_depgraph_stamp.py                         # NEW — @needs_bridge: tombstone round-trip (idempotent re-stamp)
    ├── test_depgraph_rebuild_conversions.py           # NEW — @needs_bridge: rebuild-conversions-from-tombstones
    ├── test_depgraph_cycle_fixture.py                 # NEW — @needs_bridge: 3-file cycle fixture (US3)
    ├── test_depgraph_idempotence.py                   # NEW — @needs_bridge: re-run produces byte-identical JSON + zero diff rows (SC-002)
    └── test_depgraph_schema_isolation.py              # NEW — verifies SC-007 (codeconv schema only)

.claude/skills/
└── codeconv-depgraph/                                 # NEW — thin slash wrapper
    └── SKILL.md                                       # NEW — structurally copied from .claude/skills/codeconv-discover/SKILL.md

.codeconv/
├── tombstones/                                        # WRITTEN — by stamp-tombstones subcommand (five new YAML keys per file)
└── depgraph.json                                      # NEW (gitignored or checked-in? — see R10) — default JSON artefact

specs/015-codeconv-depgraph/
└── (the documentation tree above)
```

**Structure Decision**: Single-project Python additions inside the existing `codeconv/` package — exactly mirroring feature 014's structure decision (no new top-level directory, no new language touched). One Alembic revision; one slash skill; one new tool subpackage. The tool subpackage is the unit of registration (feature 012 FR-006 auto-discovery) so no runner edits are needed. The five new tombstone YAML keys live at the END of `_FIELD_ORDER` so they are append-only — existing tombstones gain new keys but the position of existing keys (`path`, `name`, `purpose`, `key_idea`, `dependencies`, `callers`, `mtime`, `sha256`) is unchanged.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

(empty — Constitution Check passed without violations)

## Phase 0: Research outputs

See [research.md](./research.md) for:

- **R1**: Tarjan SCC algorithm choice — pure stdlib, iterative, deterministic lexicographic tiebreak
- **R2**: Auto-recompute after `mark-*` — NO; user invokes `compute` explicitly
- **R3**: `--from-tombstones` rebuild surface — confined to `rebuild-conversions-from-tombstones`; depgraph itself is always recomputed from `dart_imports`
- **R4**: JSON schema-version field — `"schema_version": 1` in metadata block
- **R5**: `depgraph_runs` traceability table — YES (mirrors `discover_runs`)
- **R6**: Table name — `codeconv.dart_depgraph` confirmed (spec FR-008 wording is normative)
- **R7**: Status-column data integrity — CHECK constraint enumerating `pending|ready|in_progress|converted`
- **R8**: Idempotence preservation — stable JSON key order + canonical YAML + deterministic Tarjan number assignment
- **R9**: Performance — graph size is trivially under SC-001's 5 s warm budget
- **R10**: `.codeconv/depgraph.json` gitignore status — gitignored (developer-local, recomputable; tombstones carry the durable round-trip data per FR-014)

All NEEDS CLARIFICATION items raised by the plan template (now moot since the spec is fully clarified) are closed in research.md.

## Phase 1: Design artefacts

- **[data-model.md](./data-model.md)** — explicit delta against feature 012's data-model. **Two new tables** in the `codeconv` schema (plus one optional traceability table); **five new YAML keys** appended to the existing tombstone frontmatter field order; no change to any existing column or row shape. The Alembic revision is `0002_dart_depgraph.py`; downgrade is a single `DROP TABLE IF EXISTS ... CASCADE` for the three new tables.
- **[contracts/depgraph_algorithm.md](./contracts/depgraph_algorithm.md)** — Tarjan SCC algorithm specification (input: list of nodes + list of edges; output: per-node SCC id; condensation DAG topo level assignment with leaves=0; lexicographic tiebreak inside a level; deterministic numbering of SCCs). Includes the exact correctness invariant for FR-004 / SC-003 (`(A→B) ⇒ topo_level(A) > topo_level(B) ∨ cycle_group_id(A) = cycle_group_id(B)`).
- **[contracts/depgraph_cli.md](./contracts/depgraph_cli.md)** — `codeconv depgraph [compute|mark-started|mark-completed|stamp-tombstones|rebuild-conversions-from-tombstones]` signature surface, flag semantics, exit codes, JSON output shape, idempotence contracts, and the thin-slash-wrapper relationship to `.claude/skills/codeconv-depgraph/SKILL.md`.
- **[contracts/depgraph_schema.md](./contracts/depgraph_schema.md)** — DDL contract for `codeconv.dart_depgraph`, `codeconv.dart_conversions`, and `codeconv.depgraph_runs`. Column types, NOT NULL constraints, CHECK constraints on `status`, primary keys, foreign keys, and the atomic-per-run write protocol (FR-008).
- **[contracts/tombstone_format_delta.md](./contracts/tombstone_format_delta.md)** — exact five new YAML keys, their position in `_FIELD_ORDER` (appended after `sha256`), null vs missing semantics, and the proof that idempotence (SC-002 / feature-012 SC-008 carry-forward) is preserved by the append-only extension.

The agent context file (`CLAUDE.md`) was updated this run to reference this plan between the existing `<!-- SPECKIT START -->` / `<!-- SPECKIT END -->` markers, replacing the prior reference to feature 014's plan.
