# Implementation Plan: codeconv-runner — overarching codeconv harness with unified `.pgdb` backing

**Branch**: `012-codeconv-runner` | **Date**: 2026-05-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/012-codeconv-runner/spec.md` (fully clarified, Session 2026-05-09).

## Summary

This feature consolidates PGLite into a single repo-wide deployment at `.pgdb/` guarded by an OS-level cross-process lock; migrates `.D2NET/pgdb/` data into the unified location without loss; converts the existing D2NET .NET tools (`D2Net.Init`, `D2Net.Scaffold`) into clients of the unified bridge instead of self-hosters; and ships `/codeconv-runner` (Claude Code skill + Python CLI built on DBOS over PGLite) plus its first registered tool `/codeconv-discover` (walks `glp_runtime_net/` for `.dart` files, populates a `codeconv` schema, writes Markdown tombstones at `.codeconv/tombstones/`).

The technical approach is dictated by spec clarifications and the existing prereq-pattern (feature 011): the canonical bridge file at `prereq-patterns/pglite/pglite_bridge.mjs` becomes the live deployment when run against `.pgdb/` (FR-012); cross-process exclusion uses `proper-lockfile` for OS-managed kernel-released locking (Clarifications Q3 / FR-002 / FR-003); auto-spawn-on-demand lifecycle is the primary path with a manual launcher as escape hatch (FR-006); DBOS Python + SQLAlchemy + the prereq-pattern's `pglite_engine_kwargs` and `pglite_compat_loaders` are used verbatim per applicability.md; tools register by file-system convention under `codeconv/tools/<name>/` and surface as sibling slash commands (FR-016).

## Technical Context

**Language/Version**:
- Node.js ≥ 20 — bridge process (`pglite_bridge.mjs`), already present per feature 011's `package.json`.
- Python ≥ 3.11 — `codeconv` runner CLI + tools, with DBOS + SQLAlchemy + psycopg.
- C# / .NET 8 — D2NET tools (`D2Net.Init`, `D2Net.Scaffold`); existing.

**Primary Dependencies**:
- npm: `@electric-sql/pglite@0.2.17` (pinned, existing); `proper-lockfile@^4.1.2` (NEW — OS-level cross-platform file lock with kernel-released semantics on POSIX and Windows).
- Python: `dbos`, `sqlalchemy>=2.0`, `psycopg[binary]>=3.1`, `typer` (CLI framework — type-hint native, fits the thin-CLI shape used by `/opskit-init`), `PyYAML` (tombstone frontmatter), `Mistletoe` or `python-frontmatter` (tombstone parse for `--from-tombstones`). Vendored: `pglite_engine_kwargs.py`, `pglite_compat_loaders.py` (copied from `D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/`).
- .NET: `Npgsql` (existing in D2Net.Init); add a small `D2Net.BridgeClient` library that implements the lock + sidecar discovery + connect protocol so `D2Net.Init` and `D2Net.Scaffold` (and any future D2NET tool) share a single implementation.

**Storage**: PGLite WASM cluster files at `.pgdb/` (gitignored per FR-029). Three logical schemas: `codeconv` (this feature's tables), `dbos` (DBOS-managed durable workflow tables), and whatever schema(s) D2NET already uses (unchanged).

**Testing**:
- Python: `pytest` for codeconv unit + integration tests (under `codeconv/tests/`).
- .NET: `xunit` for D2NET (existing; `tools/d2net/tests/`).
- Bridge: `node --test` for cross-process lock unit tests (under `prereq-patterns/pglite/tests/`).
- End-to-end: PowerShell smoke scripts under `specs/012-codeconv-runner/scripts/` exercise SC-001 / SC-002 / SC-003 / SC-006 / SC-008 / SC-013.

**Target Platform**: Windows 11 primary (Gabi's box at `D:\BSTDEV\research\GLP\GLPNET\`). Bridge + Python + .NET all target cross-platform; lock file behaviour is verified on Windows specifically (per Assumptions: if `proper-lockfile` does not honour kernel-managed release on Windows for this lock granularity, spec must be revisited).

**Project Type**: Multi-component repo deliverable (Node bridge + Python CLI + .NET clients + Claude Code skills). No single root project type.

**Performance Goals**:
- Bridge lock cycle ≤ 1 s (SC-001, SC-002).
- `/codeconv-discover` ≤ 60 s on fresh checkout (128 files), ≤ 5 s on idempotent re-run (SC-013).
- 100 sequential transactions across two stacks with zero `lost synchronization` (SC-003).

**Constraints**:
- Single bridge process per repo (FR-002).
- No `COPY ... FROM STDIN` against PGLite from any code introduced by this feature (FR-026).
- No client-side prepared-statement caching (FR-027).
- No regression of bridge in-process serialisation behaviour (FR-005).
- Caller graph is inside-only — files outside `glp_runtime_net/` may not contribute caller edges (FR-023).
- Inventory `purpose` / `key_idea` populated mechanically only — verbatim doc-comment block, no AI inference (FR-020).

**Scale/Scope**:
- 128 `.dart` files in `glp_runtime_net/` at SC-006.
- One repo, one developer, one bridge.
- `codeconv` registered tools: 1 in scope here (`discover`); the registration mechanism is built to accept N future siblings without runner edits.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution at `.specify/memory/constitution.md` is the unfilled spec-kit template (placeholders only — no ratified principles, no version, no enforced gates). There is no project-defined constitution to check this plan against.

**Gate decision**: PASS — vacuously, because no principles are defined. No violations to record in Complexity Tracking.

The authoritative project discipline lives in `CLAUDE.md` and `docs/DISCIPLINE.md` (already loaded by Claude on session start) and in the spec's clarifications + functional requirements. Those govern this plan.

## Project Structure

### Documentation (this feature)

```text
specs/012-codeconv-runner/
├── plan.md              # This file
├── spec.md              # Feature specification (clarified Session 2026-05-09)
├── research.md          # Phase 0 output — proper-lockfile, detached spawn, CLI, sidecar shape
├── data-model.md        # Phase 1 output — codeconv schema + sidecar JSON + tombstone YAML
├── quickstart.md        # Phase 1 output — fresh-checkout end-to-end smoke
├── contracts/           # Phase 1 output — bridge / runner / tool / migration contracts
│   ├── bridge_lifecycle.md
│   ├── bridge_cli.md
│   ├── codeconv_runner_cli.md
│   ├── codeconv_tool_contract.md
│   ├── codeconv_discover_cli.md
│   ├── tombstone_format.md
│   └── d2net_pgdb_migration_cli.md
├── checklists/          # speckit-checklist artefacts (out of scope here)
└── tasks.md             # /speckit-tasks output (next command)
```

### Source Code (repository root)

```text
prereq-patterns/pglite/                   # Canonical bridge — modified in place per FR-012
├── pglite_bridge.mjs                     #   add: lock acquire → sidecar JSON write → READY token capture
├── package.json                          #   add: proper-lockfile dependency
├── description.md                        #   amend: canonical bridge IS the live deployment for .pgdb/
├── applicability.md                      #   unchanged
├── sources.md                            #   unchanged
└── tests/                                # NEW — node --test cross-process lock + sidecar tests
    ├── lock_single_writer.test.mjs
    └── sidecar_roundtrip.test.mjs

.pgdb/                                    # NEW (gitignored) — runtime data dir
├── (PGLite cluster files, populated at runtime)
├── .bridge.lock                          # OS lock (proper-lockfile)
├── bridge.json                           # sidecar {port, pid, started_at, host}
└── bridge.log                            # rotated bridge stdout/stderr (~5MB × 3)

.codeconv/                                # NEW (checked in: tombstones tree only)
└── tombstones/
    ├── (mirrors glp_runtime_net/ tree, with .dart → .dart.md)
    └── .orphaned/                        # orphan history, also checked in

codeconv/                                 # NEW — Python package (runner + tools)
├── pyproject.toml
├── src/codeconv/
│   ├── __init__.py
│   ├── cli.py                            # Typer entry: `codeconv` (called by /codeconv-runner)
│   ├── runner.py                         # tool registry + DBOS workflow orchestration
│   ├── bridge_client.py                  # acquire-or-discover bridge, spawn detached, parse READY
│   ├── db/
│   │   ├── engine.py                     # SQLAlchemy engine + dbos config + apply_to_engine
│   │   ├── migrations/                   # Alembic env (NullPool + AUTOCOMMIT shape)
│   │   └── schema.py                     # codeconv.* SQLAlchemy models
│   ├── tools/
│   │   ├── __init__.py                   # registry scan
│   │   └── discover/
│   │       ├── __init__.py               # registered Tool subclass (entry point)
│   │       ├── workflow.py               # DBOS workflow + per-file step
│   │       ├── walker.py                 # filesystem walk + .gitignore-style filters
│   │       ├── parse.py                  # leading doc-comment + import extraction
│   │       └── tombstone.py              # frontmatter read/write
│   └── _vendor/
│       ├── pglite_engine_kwargs.py       # COPIED verbatim from BREENDEV opskit _vendor
│       └── pglite_compat_loaders.py      # COPIED verbatim from BREENDEV opskit _vendor
└── tests/
    ├── test_bridge_client.py             # lock race, sidecar fallback, READY parse
    ├── test_runner_registry.py           # tool discovery, no-edit-to-add-tool
    ├── test_discover_idempotence.py      # SC-008
    ├── test_discover_orphan_revival.py   # FR-025
    └── test_from_tombstones.py           # SC-007

tools/d2net/                              # MODIFIED — convert to unified-bridge clients
├── src/
│   ├── D2Net.BridgeClient/               # NEW — shared lock + sidecar + connect helper
│   │   ├── D2Net.BridgeClient.csproj
│   │   ├── BridgeClient.cs               # acquire-or-discover; mirrors codeconv/bridge_client.py
│   │   └── SidecarFile.cs
│   ├── D2Net.Init/
│   │   ├── (existing files)
│   │   ├── PgBridgeProcess.cs            # MODIFIED — replace own-bridge launch with BridgeClient call
│   │   ├── pgbridge/                     # REMOVED — old vendored bridge no longer used
│   │   └── ...
│   ├── D2Net.Scaffold/
│   │   ├── (existing files)
│   │   └── ...                           # MODIFIED if it currently launches its own bridge
│   └── D2Net.PgdbMigrate/                # NEW — one-shot migration CLI (FR-007/008/009)
│       ├── D2Net.PgdbMigrate.csproj
│       └── Program.cs
└── tests/                                # MODIFIED — point existing integration tests at .pgdb

.claude/skills/                           # NEW skills + migration-trigger sibling
├── codeconv-runner/
│   └── SKILL.md                          # thin wrapper around `codeconv` CLI
├── codeconv-discover/
│   └── SKILL.md                          # thin wrapper around `codeconv discover`
├── D2NET-init/                           # MODIFIED — point at unified .pgdb defaults
│   └── SKILL.md
├── D2NET-scaffold/                       # MODIFIED — point at unified .pgdb defaults
│   └── SKILL.md
└── D2NET-pgdb-migrate/                   # NEW — wraps tools/d2net/src/D2Net.PgdbMigrate
    └── SKILL.md

.gitignore                                # MODIFIED per FR-029
                                          #   + .pgdb/
                                          #   + .D2NET/pgdb.bak.*/
                                          #   (do NOT ignore .codeconv/tombstones/ or .orphaned/)
```

**Structure Decision**: Multi-component repo with three deployable surfaces — the canonical Node bridge (modified in place per FR-012, single source of truth, no copies), a new Python `codeconv/` package shipping the runner + first tool, and modified .NET `tools/d2net/` projects converted to bridge clients. Each surface has its own test suite. The Claude Code skills are thin slash-wrappers (per the BREENDEV `/opskit-init` reference and the existing `/D2NET-init`) and contain no business logic. Spec FR-016 mandates that the runner discovers tools by file-system convention so adding the next codeconv tool requires zero edits to runner code.

## Complexity Tracking

> No constitution gates were violated (constitution is unratified). No complexity to justify.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |
