# Data Model — codeconv-codegen (Phase 1)

## New table: `codeconv.dart_codegen` (migration `0007`)

Two-phase per-file codegen state, parallel to `dart_convspecs`/`dart_plans`.

| Column | Type | Notes |
|---|---|---|
| `path` | text PK | subtree-relative POSIX `.dart` path (FK-semantics to `dart_depgraph.path`, read-only) |
| `codegen_started_at` | timestamptz NULL | phase-1 marker (INSERT … ON CONFLICT DO NOTHING) |
| `codegen_completed_at` | timestamptz NULL | phase-2 terminal marker; set ONLY on a build-passing accept |
| `sha256_of_dart_at_codegen_start` | text NULL | drift detection vs `dart_files.sha256` |
| `target_cs_path` | text NULL | produced `.cs` path under `out/csharp/` |
| `build_status` | text NULL | `pass｜fail｜not_built` (CHECK) |
| `test_pass_rate` | real NULL | Inc-2 only; NULL when tests not in scope |
| `human_review_score` | smallint NULL | 1–5; NULL until reviewed |
| `open_escalation_count` | int NOT NULL DEFAULT 0 | `>0` ⇒ conversion-blocked (FR-007/009) |
| `batch_id` | text NULL | promotion-batch grouping |
| `promoted` | boolean NOT NULL DEFAULT false | set true only when the batch passes the promotion gate |
| `codegen_run_id` | text NULL | run traceability |

- DDL: `CREATE TABLE IF NOT EXISTS codeconv.dart_codegen (...)`; downgrade `DROP TABLE … CASCADE`. **`codeconv` schema only** — Alembic authors no `public`/`dbos` object.
- Lifecycle: append (started) → UPDATE (build/test/review/escalations) → UPDATE (completed + promoted on gate). Never bulk DELETE.

## Migration linearization
- `0006` is the current head (018). New `0007_codegen.py`: `revision="0007"`, `down_revision="0006"`. `alembic upgrade head` reaches a single head. (`test_migration_0007_single_head.py`.)

## Tombstone YAML delta (append-only, after the plan keys)
Appends, in pinned order AFTER 018's keys:
`codegen_started_at｜codegen_completed_at｜target_cs_path｜build_status｜codegen_open_escalation_count`.
Artifact *content* (the `.cs`, the prompt) is never mirrored to YAML. Idempotence: stamp→rebuild→stamp is a fixed point (`test_tombstone_codegen_stamp_rebuild.py`).

## Checked-in artifacts (not DB rows)
- **Generated code unit**: `out/csharp/<rel>.cs` — real, compilable C# (the deliverable). Validated to BE C# (inverse of convspec's no-C# rule). Git policy OPEN (R11).
- **Optimized-prompt artifact**: `.codeconv/codegen-prompt/optimized.md` — serialized optimized codegen instructions + provenance (optimizer version, metric score, dataset hash, UTC). The only optimizer→production handoff.
- **Codegen escalations report**: `.codeconv/conversion-code/_escalations-report.md` — aggregated open codegen escalations (FR-009); counts match `dart_codegen.open_escalation_count`.

## Read-only inputs (consumed, never mutated)
- `codeconv.dart_depgraph` (order/SCC/status, FR-002/FR-010) + `dart_files.sha256` (drift).
- `codeconv.dart_convspecs` + `.codeconv/conversion-specs/<rel>.dart.md`.
- `codeconv.dart_plans` + `.codeconv/conversion-plans/<rel>.dart.md`.
- `codeconv.conversion_idioms` (idiom KB, FR-011).

## State transitions (per file)
`not_started → in_progress` (codegen_started) → build gate: `fail` ⇒ retry/optimize or `escalated`; `pass` ⇒ `built` → review → batch promotion gate ⇒ `converted` (promoted). Source drift (sha mismatch) ⇒ `stale`, regenerated only under explicit `retry`/re-generate (FR-008).
