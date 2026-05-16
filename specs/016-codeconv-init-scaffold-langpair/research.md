# Phase 0 Research: codeconv init + scaffold behind a language-pair registry

The spec has **zero `[NEEDS CLARIFICATION]`** (decisions D1–D6 were resolved by the owner before drafting). This document records the design decisions, rationale, and rejected alternatives that the plan/contracts build on. Inventory facts are from the read-only D2NET/codeconv investigation captured for this feature.

## R1 — Language-pair plugin & registry shape

**Decision**: `codeconv/src/codeconv/langpairs/` is a package with a process-wide registry. `base.py` defines a `LangPair` protocol exposing per-stage hooks grouped by stage:
- `source`: `source_extensions() -> tuple[str,...]`, `tool_exclusion_globs() -> tuple[str,...]`, `extract_imports(path, subtree_root, package_name) -> list[str]`, `extract_leading_doc(path) -> str`, `read_package_name(subtree_root) -> tuple[str|None, dict|None]`.
- `target`: `target_extension() -> str`, `target_for(source_rel: str) -> str`, `workdir_name(source_rel: str) -> str | None`.
- identity: `source` / `target` string ids; `key() -> tuple[str,str]`.
A pair is registered by importing its master package (`langpairs/dart_csharp/__init__.py` calls `register(DartCSharp())`). `langpairs/__init__.py` exposes `register()`, `get(source,target)`, `list_pairs()`. `langpairs/dart_csharp` is auto-imported by `langpairs/__init__.py` so it is always available.

**Rationale**: A protocol + small registry is the minimal mechanism that satisfies FR-003/SC-003 ("new pair = new files, zero stage-tool edits"). Grouping hooks by stage matches the pipeline stages and lets each stage depend only on the slice it needs.

**Alternatives rejected**: (a) entry-point/plugin-discovery via `importlib.metadata` — heavier, packaging-coupled, unnecessary for an in-repo registry; (b) one monolithic `LangPair` god-object without stage grouping — works but blurs which stage uses which hook and complicates the test matrix; (c) hard-coded Dart→C# (no registry) — simplest but fails the feature's defining requirement.

## R2 — Factoring Dart-specifics out of discover (pair-genericisation, no behaviour change)

**Decision**: The Dart logic in `tools/discover/{walker.py,parse.py,pubspec.py}` is moved/wrapped into `langpairs/dart_csharp/source_dart.py`. `tools/discover/workflow.py` obtains the active pair from workspace settings (falling back to the `dart_csharp` default when no workspace is initialised, preserving today's behaviour) and calls the pair's source hooks instead of importing `parse`/`pubspec` directly. The existing `parse.py`/`pubspec.py`/`walker.py` either become thin shims re-exporting the `dart_csharp` implementations or are deleted with imports repointed — chosen per minimal-diff at task time. **Net behaviour for the default Dart path is byte-identical** (same regexes, same exclusion globs, same package rewrite) — verified by the unchanged feature-012/014/015 discover suites (FR-023/SC-005).

**Rationale**: Single source of truth for "what is a Dart import/exclusion"; discover becomes pair-generic without a rewrite. The feature-012/014/015 tests are the regression oracle.

**Alternatives rejected**: duplicating the Dart parser inside `langpairs/` while leaving discover's copy intact — guarantees drift between two inventories of "the same" logic (the exact anti-pattern DISCIPLINE §1.3 forbids).

## R3 — Pair selected once, fixed for every stage

**Decision**: `codeconv.workspace_settings` stores the selected `(source,target)` (+ source/target locations, options). `init` writes it (FR-004/FR-006). Every stage resolves the effective pair via a shared helper `codeconv.langpairs.resolve_workspace_pair(engine)`; if the workspace pair is unset (and the stage requires one) or differs from a `--source/--target` override, the stage refuses with a non-zero actionable error (FR-004/FR-018/SC-008). `discover` keeps a no-workspace default of `dart_csharp` so pre-016 usage is unaffected; `scaffold` requires an initialised workspace.

**Rationale**: A persisted, single authority prevents mixed-pair output (SC-008) and matches D6 ("once chosen … fixed for all stages").

**Alternatives rejected**: passing `--source/--target` on every stage invocation (no persistence) — invites per-invocation drift and mixed output; inferring the pair from file extensions — ambiguous and unenforceable.

## R4 — init delegates the inventory to discover (D3)

**Decision**: `codeconv init` does configuration only (validate paths, write `workspace_settings`, compute+store recommended exclusions from the pair's `tool_exclusion_globs()`, initialise `phase_sequence`/`phase_status`), then **invokes the existing discover** to build the inventory. Invocation is in-process (`from codeconv.tools.discover.workflow import run_discover; run_discover(..., root=<source>, data_dir=...)`) so one bridge acquisition is shared and there is no second process. Exclusions are applied by discover's existing walker via the pair's exclusion globs. `init` never scans the source tree itself (no second `dart_files` writer).

**Rationale**: Single inventory source of truth (DISCIPLINE §1.3); reuses the proven, tested discover path; avoids the D2NET `public.dart_files` duplicate.

**Alternatives rejected**: re-implementing the scanner in `init` (two inventories, drift); shelling `codeconv discover` as a subprocess (extra process + second bridge acquire for no benefit).

## R5 — scaffold target tree + tombstone integration (D4)

**Decision**: `scaffold` reads the in-scope file set from `codeconv.dart_files` (+ exclusions from `codeconv.excluded_directories`), plans the target tree via the pair's `target_for()`/`workdir_name()`, writes into a staging dir `<target>.codeconv-scaffold-tmp/`, then atomically moves it into place (mirroring D2Net.Scaffold's `StagingMutator` so a failure never leaves a half-written target). For each scaffolded source file it records the produced target path into the **existing feature-015 tombstone `target_path`** by reusing `codeconv.tools.depgraph.tombstone_writer` helpers (not a new `scaffold_tracker` table; not a parallel writer). It advances `codeconv.phase_status` (`phase='scaffold'`). A missing tombstone for a scaffolded file → warning, not failure (FR-015). Destructive overwrite of a non-empty target requires explicit confirmation (skill-level gate, mirroring `/D2NET-scaffold`).

**Rationale**: Unifies scaffold + depgraph + tombstone state on one per-file record (D4); reuses the canonical YAML writer (idempotence carry-forward); staged+atomic preserves D2NET's safety property.

**Alternatives rejected**: a `codeconv.scaffold_tracker` table (D5 explicitly drops it; would re-introduce a parallel per-file store); writing target files in place without staging (half-written tree on failure — regresses a D2NET safety guarantee).

## R6 — Schema migration 0003

**Decision**: New Alembic migration `0003_d2net_into_codeconv.py` (`down_revision="0002"`) creates `codeconv.workspace_settings`, `codeconv.excluded_directories`, `codeconv.phase_sequence`, `codeconv.phase_status` via `CREATE TABLE IF NOT EXISTS` (idempotent, isolation-safe). It does **not** create `public.dart_files` or any `scaffold_tracker`. `downgrade()` drops the four tables `IF EXISTS … CASCADE` in reverse order. Legacy D2NET `public.*` data is **not** migrated (D1; transient build state — re-established by running `codeconv init`).

**Rationale**: FR-020 (codeconv-schema only); idempotent CREATE matches migration 0001/0002 style; no data migration keeps scope minimal and avoids coupling to the dropped PgdbMigrate.

**Alternatives rejected**: `ALTER … SET SCHEMA` to move existing `public.*` tables — couples the migration to a specific pre-existing live cluster state and to the dropped legacy path; rejected for a clean `IF NOT EXISTS` create.

## R7 — Removing the .NET toolchain (FR-022)

**Decision**: Delete `tools/d2net/` (Init, Scaffold, PgdbMigrate, BridgeClient) and `.claude/skills/D2NET-init|D2NET-scaffold|D2NET-pgdb-migrate`. Update docs that reference them (`CLAUDE.md` "Migration to unified bridge" paragraph, `.gitignore` D2NET-backup line, any README/known-issues pointers) to point at `codeconv init`/`codeconv scaffold`. A short note records that the one-shot legacy `.D2NET/pgdb/`→`.pgdb/` migration is historically complete and intentionally not ported.

**Rationale**: SC-006 ("exactly one toolchain"); D1/D2 (PgdbMigrate done, BridgeClient duplicates `codeconv.bridge_client`).

**Alternatives rejected**: keeping `tools/d2net/` in parallel — violates SC-006 and leaves two bridge clients; archiving under a `legacy/` dir — still ships dead .NET and confuses "which toolchain", rejected in favour of deletion (git history is the archive).

## R8 — Test strategy

**Decision**: (a) `test_langpair_registry.py` — pure unit, no bridge: registry register/get/list, unregistered-pair refusal, `dart_csharp` hook outputs incl. positive+negative controls, and a registered **test-only second pair** proving zero stage-tool edits (SC-003). (b) `test_init.py` / `test_scaffold.py` — `@needs_bridge`, `discover_repo`/`tmp_path` fixtures (fresh 0.4.5 cluster), subprocess `run_codeconv`: idempotence, destructive-confirm, exclusion add/remove, pair-mismatch refusal, tombstone `target_path` recording, staged-write atomicity. (c) `test_pipeline_dart_csharp.py` — regression: init→discover→depgraph→scaffold on a synthetic subtree asserting cross-stage consistency; plus the existing discover/depgraph suites remain green (SC-005).

**Rationale**: Mirrors the proven 012/014/015 test conventions; satisfies FR-024/025/026.

## R9 — Build order & branch base

**Decision**: Feature 016 branches off the unmerged `015-codeconv-depgraph` HEAD because D4/FR-015 depend on the feature-015 tombstone `target_path` + `tombstone_writer` surface (only present on 015). 016 lands after 015 merges to `main` (or merges 015 first); the spec records this dependency as an assumption.

**Rationale**: D4 is unimplementable without feature-015's conversion-tracking substrate; branching off 015 keeps the dependency satisfied without rebasing churn.

**Alternatives rejected**: branching off `main` and re-implementing a `target_path` surface — duplicates feature-015 work and risks divergence.
