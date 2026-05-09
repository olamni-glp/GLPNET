# Migrated

**Date**: 2026-05-09
**Migrated to**: [`prereq-patterns/pglite/`](../../../prereq-patterns/pglite/)
**Feature**: `specs/011-prereq-patterns-catalog/`

The reference bridges in this directory (`bridge-traced.mjs`, `bridge-batched.mjs`, `bridge-direct.mjs`, `package.json`, `README.md`) supplied the lineage for the merged PGLite Postgres-wire bridge that now lives at [`prereq-patterns/pglite/pglite_bridge.mjs`](../../../prereq-patterns/pglite/pglite_bridge.mjs). The merged bridge incorporates this directory's no-pg-gateway hand-rolled startup and the two diagnosed bug fixes (PGLite implicit-Sync, pg-gateway 0.3.0-beta.4 response corruption), plus AIGRID's downstream serialization (`globalWorkChain`) and session-state safety (synthetic `ROLLBACK` on startup) additions.

## Where to read what

| If you want to … | Go to |
|---|---|
| Use the bridge in a new feature | `prereq-patterns/pglite/` (description.md → sources.md → copy `pglite_bridge.mjs` + `package.json`) |
| Understand the bug-discovery journey (which bridge variant diagnosed which bug, and why pg-gateway was eventually skipped) | This directory: `README.md`, `bridge-traced.mjs`, `bridge-batched.mjs`, `bridge-direct.mjs` |
| See the feature-by-feature classification of what was preserved / superseded / dropped during the merge | `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` |
| Verify the bridge against Npgsql / psqlODBC / psycopg | `prereq-patterns/pglite/sources.md` § "Deferred regression checks" — SC-003 (100 sequential cycles) and SC-004 (concurrent psycopg pipeline) |

## Why this directory was retained, not deleted

The narrative and the three bridge variants in this directory are a historical record of *why* the merged bridge looks the way it does. Removing them would lose the reasoning. The merged bridge's [`description.md`](../../../prereq-patterns/pglite/description.md) and [`sources.md`](../../../prereq-patterns/pglite/sources.md) describe outcomes; this directory describes the journey.

Future glpnet features adopting the pattern should copy from `prereq-patterns/pglite/`, not from here. This directory is archival.
