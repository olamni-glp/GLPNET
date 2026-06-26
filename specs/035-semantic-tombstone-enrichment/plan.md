# Implementation Plan: Semantic Tombstone Enrichment

**Branch**: `035-semantic-tombstone-enrichment` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/035-semantic-tombstone-enrichment/spec.md`

## Summary

Add a new auto-discovered codeconv tool, `codeconv enrich`, that fills blank
`purpose`/`key_idea` tombstone fields for discovered Dart files by inferring a
concise **purpose** (responsibility/role) and a distinct **key_idea** (central
algorithm/mechanism) from the file's actual source — exclusively through an
injected **Claude/Agent seam** (no external LM API; the GEPA-no-API rule). Each
field gains a provenance marker `purpose_source`/`key_idea_source` ∈
{`doc`,`inferred`,`absent`}, persisted to **both** the tombstone `.dart.md` and
the `codeconv.dart_files` row. Enrichment is idempotent, change-aware,
path-scopable, and fault-isolated. A scoped, provenance-aware change to
`discover` preserves inferred values across re-runs (FR-008). Technical approach
and every design decision are grounded in the verified codeconv source in
[research.md](./research.md); entities in [data-model.md](./data-model.md);
interfaces in [contracts/](./contracts/).

## Technical Context

**Language/Version**: Python ≥3.11 (`from __future__ import annotations`), the
codeconv harness.
**Primary Dependencies**: Typer (CLI), SQLAlchemy + `codeconv.bridge_client` /
`codeconv.db.engine` (shared PGLite bridge), PyYAML (tombstone frontmatter),
Alembic (additive migration). **Explicitly NOT**: openai / litellm / any
external-LM SDK (Constitution V).
**Storage**: `.pgdb` PGLite cluster — `codeconv.dart_files` — plus the
checked-in `.codeconv/tombstones/<rel>.dart.md` markdown records, plus a
durable per-run log file `.codeconv/enrich-runs/<run-id>.json` (FR-011; no new
DB table — analyze C1).
**Testing**: pytest under `codeconv/tests/`, `@needs_bridge` + `run_codeconv()`
integration helpers, fake-`infer_fn` stubs for the seam; run via
`codeconv/.venv/Scripts/python.exe -m pytest`.
**Target Platform**: Windows dev host; cross-platform Python CLI.
**Project Type**: Single project — a codeconv CLI tool subpackage (auto-discovered).
**Performance Goals**: inference is the cost driver; a no-change re-run performs
**zero** inferences (SC-002). No hard latency target — bounded by the Claude seam.
**Constraints**: Claude-only LM / no external API (V); additive single-head
migration `0010→0011` (VI-a); single OS-lock-guarded bridge reused, no second
consumer (VI-b); blank-field-only (FR-006); tombstones stay git-reviewable
(FR-014); fault isolation — a per-file failure never corrupts a tombstone (FR-010).
**Scale/Scope**: the discovered set under `glp_runtime_net/` (Dart→C# pair),
order ~100–200 Dart files; `.orphaned/` excluded.

All Technical Context unknowns are resolved in research.md (no NEEDS
CLARIFICATION remain).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0. Re-checked
after Phase 1 — still passing.*

| Principle | Gate-ability | Verdict | Evidence in this plan |
|---|---|---|---|
| I. Spec-First | judgement | **PASS** | spec.md clarified (2 Q resolved), quoted & consistency-checked; plan derives from it, no code-led decisions |
| II. Bug-Protocol / No-Workarounds | judgement | **PASS** | no try/catch "robustness" masking caller bugs; FR-010 fault isolation is a *specified* per-file outcome, not a workaround (research R-007) |
| III. SRSW / `skipSRSW` | machine | **PASS (N/A)** | Python codeconv tool; zero `skipSRSW` tokens in artifacts |
| IV-a. Language Authority | judgement | **PASS (N/A)** | no GLP language surface touched |
| IV-b. Preserve Working Internals | judgement | **PASS** | discover edits are additive/conditional; `_PRESERVED_APPENDED_KEYS` machinery extended, not removed |
| **V. Claude-Only LM / No External API** | machine | **PASS** | FR-003/SC-004; `InferFn` injected + `_require_fn` no-API-default (contracts/infer_seam.md); zero `openai`/`litellm`/`OPENAI_API_KEY` on the path |
| **VI-a. Additive, Idempotent, Single-Head Migration** | machine | **PASS** | migration `0011` chains off `0010`, additive + `IF NOT EXISTS`; new `test_migration_0011_single_head.py` asserts `heads == ["0011"]` (contracts/migration_0011.md) |
| VI-b. Single Bridge | judgement | **PASS** | reuses `acquire_or_discover` + `build_engine`; no second PGLite consumer (contracts/enrich_cli.md) |
| VII. Test-Gated, Commit-Scoped | advisory | **PASS** | baseline-green-before/after; commit only feature files; ship via GitFlow |
| VIII. Single Source of Truth & Traceability | judgement | **PASS** | spec→plan→tasks traceable; tombstone format authority stays `discover/tombstone.py` (extended, not duplicated) |

**No violations. Complexity Tracking is empty.**

One downstream note for `/bk-analyze`: the constitution text references "current
head `0010`"; this feature advances it to `0011` (the principle is about the
*single linear head discipline*, which holds — not a frozen number). The
pre-existing `test_migration_0010_single_head.py` (asserts `["0010"]`) must be
updated/superseded to `["0011"]` — captured for `/bk-tasks`.

## Project Structure

### Documentation (this feature)
```text
specs/035-semantic-tombstone-enrichment/
├── plan.md              # This file
├── research.md          # Phase 0 — 8 design decisions, source-grounded
├── data-model.md        # Phase 1 — provenance domain, frontmatter+DB model, state machine
├── quickstart.md        # Phase 1 — run/verify recipe
├── contracts/
│   ├── enrich_cli.md            # the `codeconv enrich run` CLI contract
│   ├── infer_seam.md            # the injected Claude InferFn seam (no-API)
│   ├── discover_preservation.md # FR-008 discover-side provenance preservation
│   └── migration_0011.md        # additive provenance columns + head test
├── spec.md
└── checklists/requirements.md
```

### Source Code (repository root)
```text
codeconv/src/codeconv/
├── tools/
│   └── enrich/                      # NEW auto-discovered tool (zero runner/CLI edits)
│       ├── __init__.py             #   app: typer.Typer + register_workflows (no-op); `run` cmd
│       ├── workflow.py             #   run_enrich(): candidate scan → infer_fn → write tombstone+DB; summary
│       └── seam.py                 #   InferRequest/InferResult/InferFn + _require_fn (no-API-default)
│   └── discover/                    # SCOPED edits (existing tool; NOT the registry)
│       ├── tombstone.py            #   + purpose_source/key_idea_source in _FIELD_ORDER & _PRESERVED_APPENDED_KEYS
│       └── workflow.py             #   seed sets provenance; conditional inferred-preservation on re-write
├── db/migrations/versions/
│   └── 0011_enrich_provenance.py    # NEW additive migration (purpose_source/key_idea_source + backfill)
└── (runner.py / cli.py UNCHANGED — auto-discovery)

codeconv/tests/
├── test_enrich_blank_inference.py   # US1: blank → inferred, distinct key_idea, sha unchanged
├── test_enrich_idempotence.py       # US2/SC-002: re-run = 0 inferences, byte-identical
├── test_enrich_scope_and_faults.py  # US3: --path scope, per-file failure isolation, summary counts
├── test_enrich_no_api_seam.py       # SC-004: bare CLI exits 2; fake infer_fn only (no network)
├── test_discover_preserves_inferred.py # FR-008/SC-003: discover re-run preserves inferred values
└── test_migration_0011_single_head.py  # VI-a: single head 0011, linear chain
```

**Structure Decision**: Single-project codeconv tool. The new behavior is one
auto-discovered subpackage (`tools/enrich/`) plus one additive migration; the
only edits to existing code are scoped, provenance-aware changes inside the
`discover` tool (`tombstone.py`, `workflow.py`) demanded by FR-008 — never the
runner/CLI registry (FR-016 preserved). Driving Claude seam lives in a future
`/codeconv-enrich` skill that injects `infer_fn` (out of this plan's code scope;
the tool ships no built-in LM backend).

## Complexity Tracking

> No Constitution violations — no justifications required. (Table intentionally empty.)
