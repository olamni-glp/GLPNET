# Implementation Plan: prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/011-prereq-patterns-catalog/spec.md`

## Summary

Bring the AIGRID `prereq-patterns/` discipline into glpnet: a root-level catalog directory with three governance files (`directory.md`, `howto.md`, `policies.md`), eight imported per-pattern sub-directories each with three required files (`description.md`, `applicability.md`, `sources.md`), and six format contracts copied verbatim+scrubbed under `specs/011-prereq-patterns-catalog/contracts/`. The substantive engineering work is the **pglite merge**: a single canonical implementation that preserves glpnet's no-pg-gateway hand-rolled wire-protocol bridge (Npgsql / psqlODBC compatible) plus its two diagnosed bug fixes, while incorporating AIGRID's `globalWorkChain` / per-conn `workChain` / `endsAtFlushBoundary()` / synthetic-`ROLLBACK` / Windows `DETACHED_PROCESS` lifecycle / `sidecar.json` discovery / `@electric-sql/pglite@0.2.17` pin. A migration-analysis document classifies every distinguishing feature of both pre-merge bridges as `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale` so that no learning is silently dropped. After this feature, the catalog is fully glpnet-local — no AIGRID cross-references for governance.

## Technical Context

**Language/Version**: Markdown (catalog content); the cited pglite bridge code is JavaScript on Node ≥18 (cited, not authored by this feature beyond the merged sources.md).
**Primary Dependencies**: None for the catalog import itself. The pglite pattern cites `@electric-sql/pglite@0.2.17` (pinned in AIGRID upstream); the catalog itself adds no runtime dependency to glpnet.
**Storage**: N/A for the catalog; pglite pattern cites PGLite (file-backed, off-repo).
**Testing**: Link-check + grep-based conformance scripts (one-shot, run during handover) covering SC-002 (link resolution), SC-006 (governance fidelity), SC-007 (lifecycle drift), SC-008 (no-AIGRID grep). Pglite regression checks (SC-003 Npgsql/psqlODBC, SC-004 psycopg-style invariant) are documented in `prereq-patterns/pglite/sources.md` for execution by future glpnet features that adopt the bridge — not run as part of this catalog-import feature.
**Target Platform**: Windows host (paths under `D:/`); cross-platform Markdown content.
**Project Type**: documentation-catalog (no source code or tests in `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/` is added or modified).
**Performance Goals**: N/A — content artefact.
**Constraints**: 100% glpnet-local link resolution from `prereq-patterns/howto.md`, `prereq-patterns/policies.md`, and per-pattern files (`sources.md` upstream column intentionally excepted); zero learning loss in pglite merge (verified via `pglite-merge-analysis.md`); no GLP language / runtime / test behaviour change.
**Scale/Scope**: 3 governance files + 8 pattern dirs × 3 files = 27 catalog markdown files; 6 format contracts; 1 migration analysis; the plan/research/data-model/quickstart artefacts. ≈ 40 markdown files; ~0 lines of code added (catalog import only).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The repository's speckit constitution at `.specify/memory/constitution.md` is an **unfilled template stub** (placeholders `[PROJECT_NAME]`, `[PRINCIPLE_1_NAME]`, …). The Constitution Check gate is therefore **vacuous** — there are no defined principles to gate against. This is recorded as a complexity item below; resolving it is out of scope for this feature.

In place of a formal constitution, this plan was cross-checked against the de-facto principles encoded in `CLAUDE.md` and `docs/DISCIPLINE.md`:

| Principle | Source | This feature |
|---|---|---|
| Spec-First Development | DISCIPLINE 1.1 | PASS — `spec.md` finalized, clarifications resolved Q1–Q3, requirements checklist all green |
| No Workarounds | DISCIPLINE 1.2 | PASS — pglite is a clean merge; no bypasses |
| Fix Infrastructure, Not Symptoms | DISCIPLINE 1.3 | PASS — single canonical bridge replaces two scattered references |
| Traceability | DISCIPLINE 1.4 | PASS — every pattern's `sources.md` cites upstream with `@<branch>` pinning |
| Verify Before Acting | DISCIPLINE 1.5 | PASS — implementation requires reading actual AIGRID files; spec assumes AIGRID host reachable at implementation time |
| GLP-First | DISCIPLINE 1.12 | N/A — no GLP code authored |
| Language Authority | DISCIPLINE 1.14 | N/A — no GLP language change |
| Test Baseline | DISCIPLINE Part II | N/A — feature touches no GLP / Dart / Flutter test code; baseline irrelevant |

**Verdict**: PASS (de-facto). No principle violations; vacuous formal gate noted in Complexity Tracking.

**Re-check after Phase 1 design**: PASS. Phase 1 artefacts (`research.md`, `data-model.md`, `quickstart.md`, `contracts/README.md`) introduce no new principle conflicts.

## Project Structure

### Documentation (this feature)

```text
specs/011-prereq-patterns-catalog/
├── plan.md                       # This file (/speckit-plan output)
├── spec.md                       # Already authored
├── research.md                   # Phase 0 output (this command)
├── data-model.md                 # Phase 1 output (this command)
├── quickstart.md                 # Phase 1 output (this command)
├── pglite-merge-analysis.md      # FR-009 deliverable, authored during /speckit-implement
├── checklists/
│   └── requirements.md           # Already authored
├── contracts/                    # FR-005 — six format contracts copied verbatim+scrubbed during /speckit-implement
│   ├── README.md                 # Phase 1 placeholder enumerating the 6 expected files + AIGRID source paths
│   ├── description_md_format.md
│   ├── applicability_md_format.md
│   ├── sources_md_format.md
│   ├── directory_md_format.md
│   ├── howto_md_format.md
│   └── policies_md_format.md
└── tasks.md                      # /speckit-tasks output (NOT created by /speckit-plan)
```

### Source Code (repository root)

This is a documentation-catalog feature. No code is added under `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/`. The catalog itself adds one new top-level peer:

```text
prereq-patterns/                       # FR-001 — new top-level peer of specs/, docs/, programs/, glp_runtime/, glp_multiagent/, test/
├── directory.md                       # FR-002 — index, lists pglite first (active), other 7 with (draft) suffix in source order
├── howto.md                           # FR-002 — authoring contract; links to specs/011-prereq-patterns-catalog/contracts/
├── policies.md                        # FR-002, FR-010, FR-015 — Policy 1 verbatim, Policy 2 with glpnet-local destination
├── pglite/                            # FR-003, FR-012 — Status: active
│   ├── description.md                 # FR-004 — what+why+how-a-feature-uses-it; Status: active
│   ├── applicability.md               # FR-004, FR-018 — superset: DBOS, SQLAlchemy, Alembic, psycopg, Npgsql, psqlODBC, Other consumers
│   └── sources.md                     # FR-004, FR-017 — cites both AIGRID pglite_bridge.mjs and glpnet bridge-direct.mjs
├── dbos/                              # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── flask-sqlalchemy-alembic-api/      # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── pglite-backup-restore/             # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── blazor-spa-bg-api/                 # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── background-task-manager/           # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── local-secrets-store/               # FR-003, FR-012 — Status: draft
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
└── secure-signatures/                 # FR-003, FR-012 — Status: draft
    ├── description.md
    ├── applicability.md
    └── sources.md
```

**Touched outside the new top-level peer:**

- `docs/research/pgbridge-reference/` — disposition per FR-014: either removed with a one-file forwarding note, or retained with a `MIGRATED.md` archival note. Decision is captured during `/speckit-implement`, not pre-decided here.
- `CLAUDE.md` — speckit-managed plan reference is updated from `specs/010-scaffold-skill/plan.md` to `specs/011-prereq-patterns-catalog/plan.md` (per the slash command's Phase 1 agent-context update).

**Structure Decision**: Documentation-catalog feature. The catalog lives at the repo root (`prereq-patterns/`) as a new top-level peer; speckit artefacts live under `specs/011-prereq-patterns-catalog/`. No `src/`, `backend/frontend/`, or `api/` structure applies.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Constitution Check is vacuous (`.specify/memory/constitution.md` is an unfilled template) | This feature was scheduled before the project's speckit constitution was authored. The de-facto constitution lives in `CLAUDE.md` + `docs/DISCIPLINE.md` and was used for the gate evaluation above. | Filling the constitution is a project-wide governance action, not a docs-catalog import; doing it here would balloon scope. Flagged as a remediation candidate for `/speckit-analyze` to surface. |
| Two specification authorities (catalog `howto.md` vs `specs/011/contracts/`) | Catalog `howto.md` is the catalog-author-facing contract; format-contract files in `contracts/` are normative line-shape definitions. Both are required by the source AIGRID design. | Collapsing into one would either bloat `howto.md` past readability or scatter the format details across pattern files. Splitting matches AIGRID precedent and survives spec-template review. |
| Pglite pattern is a "merge" not a "copy" | FR-006 — neither glpnet's no-pg-gateway / Npgsql-compat learnings nor AIGRID's serialization / lifecycle / Python-consumer adaptations may be silently dropped. | A straight copy from either side loses real, costly learnings (two known bugs in glpnet's case; serialization invariants in AIGRID's case). The migration-analysis document (FR-009) is the cost of doing it right. |
