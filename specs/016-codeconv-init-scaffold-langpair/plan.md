# Implementation Plan: codeconv init + scaffold behind a pluggable language-pair registry

**Branch**: `016-codeconv-init-scaffold-langpair` | **Date**: 2026-05-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/016-codeconv-init-scaffold-langpair/spec.md`

## Summary

Port the two load-bearing D2NET .NET tools (Init, Scaffold) into the existing `codeconv` Python tool package as `codeconv init` / `codeconv scaffold` (+ `/codeconv-init` / `/codeconv-scaffold` skills), and replace their implicit Dart→C# coupling with an explicit, pluggable **language-pair registry**. A `(source, target)` pair is a plugin exposing per-stage hooks; a master package aggregates the per-stage pieces for one pair. The pair is chosen once at `init`, persisted in `codeconv`-schema workspace settings, and **fixed for every stage** (init, discover, depgraph, scaffold). D2NET's `public.*` tables move into the `codeconv` schema via a new Alembic migration `0003`; `public.dart_files` / `public.scaffold_tracker` are dropped (folded into `codeconv.dart_files` + the feature-015 tombstone `target_path`). `D2Net.PgdbMigrate` and `D2Net.BridgeClient` are NOT ported (D1/D2); `tools/d2net/` and the `D2NET-*` skills are removed. The only production pair shipped/validated is Dart→C#; the registry is proven extensible by a test-only second pair. Full unit + integration + regression coverage; no behavioral regression to features 012/014/015.

## Technical Context

**Language/Version**: Python 3.11+ (codeconv venv at `codeconv/.venv`); existing `codeconv` package.
**Primary Dependencies**: Typer (CLI), SQLAlchemy + psycopg (PGLite via the unified bridge), PyYAML (tombstones); existing `codeconv.bridge_client`, `codeconv.db.engine`, Alembic (`codeconv/src/codeconv/db/migrations/`).
**Storage**: PGLite cluster via the unified bridge (`.pgdb/`; on this exFAT checkout the canonical data-dir is `C:/pglite/research/glpnet` — pass `--data-dir`). All new tables in the `codeconv` schema. Tombstones under `.codeconv/tombstones/`.
**Testing**: pytest (`codeconv/tests/`), `@needs_bridge` gating, `discover_repo`/`tmp_path` fixtures (fresh 0.4.5 clusters), subprocess `run_codeconv` harness — same conventions as features 012/014/015.
**Target Platform**: Windows-first dev (exFAT D:, NTFS `C:/pglite/research/glpnet`); POSIX-compatible.
**Project Type**: CLI tool subpackages inside an existing Python package (single project).
**Performance Goals**: init + scaffold each ≤ existing discover budget on `glp_runtime_net/` (≤ 60 s fresh, ≤ 5 s idempotent re-run); no per-file network/db chattiness beyond one transaction per stage.
**Constraints**: schema isolation to `codeconv` (FR-020); FR-026/FR-027 carry-forward (no `COPY ... FROM STDIN`, no client-side prepared-statement cache); staged/atomic target writes; idempotence; built on the unmerged feature-015 branch (tombstone `target_path` substrate).
**Scale/Scope**: `glp_runtime_net/` (~128 files / ~443 edges post-014); two new tools (~Init 3.4k LOC / Scaffold 1.4k LOC of .NET to re-express in Python, much shed via D3 delegation), one langpair plugin package, one migration, two skills.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is the unfilled placeholder template (no ratified principles). Per established project practice (features 012/014/015), the Constitution Check is **N/A** and this plan defers to the authoritative project-discipline docs: `CLAUDE.md` and `docs/DISCIPLINE.md` (spec-first, TDD, no-workarounds, schema isolation, single-source-of-truth, commit-per-logical-group). No gate violations: this feature adds isolated tool subpackages + one migration + one plugin package; it removes (does not fork) the .NET toolchain; it reuses the existing bridge/runner/tombstone infrastructure rather than introducing parallel mechanisms.

## Project Structure

### Documentation (this feature)

```text
specs/016-codeconv-init-scaffold-langpair/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── langpair_plugin_contract.md
│   ├── codeconv_init_cli.md
│   └── codeconv_scaffold_cli.md
├── checklists/requirements.md   # from /speckit-specify (passing)
├── spec.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
codeconv/src/codeconv/
├── langpairs/                         # NEW — pluggable source→target pairs
│   ├── __init__.py                    # registry: register()/get()/list_pairs()
│   ├── base.py                        # LangPair protocol + per-stage hook ABCs
│   └── dart_csharp/                   # NEW — the only production pair
│       ├── __init__.py                # master registration (binds the per-stage pieces)
│       ├── source_dart.py             # source ext(s) + tool-exclusion recs + import/doc extraction (factored from tools/discover/{walker,parse,pubspec}.py)
│       └── target_csharp.py           # target extension + working-dir/naming convention
├── tools/
│   ├── init/                          # NEW — port of D2Net.Init (de-branded)
│   │   ├── __init__.py                # Typer app: `codeconv init` (+ add/remove-exclude, list, inspect)
│   │   └── workflow.py                # workspace settings + exclusions + phase tables; delegates inventory to discover
│   ├── scaffold/                      # NEW — port of D2Net.Scaffold (de-branded)
│   │   ├── __init__.py                # Typer app: `codeconv scaffold`
│   │   ├── workflow.py                # plan → staged copy → atomic move → tombstone target_path + phase advance
│   │   └── planner.py                 # target-tree plan (uses the selected pair's target hooks)
│   ├── discover/                      # MODIFIED — sources Dart-specifics from the selected langpair (pair-generic)
│   └── depgraph/                      # (unchanged behaviour; reads workspace pair for the mismatch guard only if needed)
└── db/migrations/versions/
    └── 0003_d2net_into_codeconv.py    # NEW — codeconv.{workspace_settings,excluded_directories,phase_sequence,phase_status}

.claude/skills/
├── codeconv-init/SKILL.md             # NEW (mirrors /codeconv-discover wrapper + D2NET destructive-confirm)
├── codeconv-scaffold/SKILL.md         # NEW
├── D2NET-init/  D2NET-scaffold/  D2NET-pgdb-migrate/   # REMOVED (FR-022)
tools/d2net/                            # REMOVED/ARCHIVED (.NET sources, incl. PgdbMigrate + BridgeClient)

codeconv/tests/
├── test_langpair_registry.py          # unit — registry + dart_csharp hooks (pos/neg)
├── test_init.py                       # integration — @needs_bridge
├── test_scaffold.py                   # integration — @needs_bridge
└── test_pipeline_dart_csharp.py       # regression — init→discover→depgraph→scaffold
```

**Structure Decision**: Single-project, additive tool subpackages under the existing `codeconv` package, conforming to `specs/012-codeconv-runner/contracts/codeconv_tool_contract.md` (auto-discovered `app: typer.Typer` + optional `register_workflows`). The Dart-specific logic currently embedded in `tools/discover/{walker,parse,pubspec}.py` is factored behind the `langpairs/` registry so discover/scaffold/init become pair-generic; `dart_csharp` is the first registered pair. One Alembic migration (`0003`) carries the D2NET workspace tables into the `codeconv` schema. The .NET `tools/d2net/` tree and `D2NET-*` skills are deleted, not forked, so exactly one toolchain remains.

## Complexity Tracking

No Constitution gate violations (constitution is an unfilled placeholder; project discipline is satisfied). The one notable structural addition — the `langpairs/` registry indirection — is the explicit, owner-mandated reason for the feature (D6) and is justified by FR-003/SC-003 (a new pair must slot in with zero stage-tool edits); a hard-coded Dart→C# port would be simpler but fails the feature's defining requirement.
