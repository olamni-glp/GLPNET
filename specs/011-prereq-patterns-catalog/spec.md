# Feature Specification: prereq-patterns catalog (glpnet)

**Feature Branch**: `011-prereq-patterns-catalog`
**Created**: 2026-05-09
**Status**: Draft
**Input**: User description: "clone D:\BREENDEV\aigrid\AWS-Infra\prereq-patterns into this repo" — copy and adapt into a glpnet-equivalent.

## Context

The source catalog lives in the AIGRID repo at `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/` and consists of three governance files (`directory.md`, `howto.md`, `policies.md`) plus eight per-pattern sub-directories (`pglite`, `dbos`, `flask-sqlalchemy-alembic-api`, `pglite-backup-restore`, `blazor-spa-bg-api`, `background-task-manager`, `local-secrets-store`, `secure-signatures`). Each pattern is a consolidation of working code somewhere upstream, indexed by three required files: `description.md`, `applicability.md`, `sources.md`. The source design intent: a downstream feature wanting a prerequisite (a local Postgres, durable workflow runtime, secrets store, signing surface, background-task registry, …) finds the curated implementation here and copies / adapts it instead of re-deriving the design.

Glpnet today has no equivalent catalog. It does, however, have its own working PGLite-bridge investigation at `docs/research/pgbridge-reference/` (`bridge-direct.mjs`, `bridge-batched.mjs`, `bridge-traced.mjs`, `package.json`, `README.md`) with two diagnosed bugs already fixed in `bridge-direct.mjs`: PGLite's implicit-Sync after `execProtocolRaw`, and pg-gateway 0.3.0-beta.4 corrupting the response stream — the latter forced glpnet to skip pg-gateway entirely with a hand-rolled minimal Postgres-wire server, which is what enables Npgsql / psqlODBC compatibility. The AIGRID `pglite` pattern targets Python `psycopg` consumers and contributes complementary learning (a `globalWorkChain` global FIFO across connections, per-connection `workChain`, `endsAtFlushBoundary()` flush detection, synthetic `ROLLBACK` on startup handshake, Windows `DETACHED_PROCESS` lifecycle in a Python sidecar, a SQLAlchemy `engine_kwargs` helper).

This feature brings the catalog discipline and the eight patterns into glpnet, adapts AIGRID-only references to glpnet equivalents, and merges the two pglite implementations into one canonical bridge that loses neither glpnet's no-pg-gateway / .NET-ODBC compatibility nor AIGRID's serialization / lifecycle / Python-consumer adaptations.

## Clarifications

### Session 2026-05-09

- Q: Which consumers should pglite's `applicability.md` document — superset (AIGRID Python + glpnet .NET), glpnet-only, or glpnet-primary with Python as external? → A: **Superset.** Keep all AIGRID Python sections (DBOS, SQLAlchemy, Flask-SQLAlchemy, Alembic, psycopg, "Other consumers") AND add `### Npgsql` and `### psqlODBC` for glpnet's .NET stack. Pglite becomes the catalog's reference example of a multi-stack pattern.
- Q: Pin Policy 2's glpnet-local destination path now, or stay deferred? → A: **Pin to `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`** (sibling to glpnet repo root at `D:/BSTDEV/research/GLP/GLPNET/`, off-repo as Policy 2 requires). Bootstrap of the destination remains deferred to a future glpnet feature; only the path convention is pinned now.
- Q: Source for the format contracts under `specs/011-prereq-patterns-catalog/contracts/` — verbatim copy, copy + scrub AIGRID references, or inline into governance files? → A: **Copy 6 verbatim from AIGRID then scrub.** The 6: `description_md_format.md`, `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`, `howto_md_format.md` (from AIGRID `specs/001-prereq-patterns-pglite/contracts/`), and `policies_md_format.md` (from AIGRID `specs/002-add-prereq-patterns-batch/contracts/`). The AIGRID-feature-specific files `howto_md_amendment.md` and `sibling_clone_convention.md` are NOT brought across.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Future glpnet feature finds and copies a prerequisite (Priority: P1)

A glpnet developer is starting a new feature that needs a local Postgres-compatible database (or a durable workflow runtime, secrets store, signing surface, background-task registry). They open `prereq-patterns/directory.md`, find the matching pattern, read its `description.md` to understand what it produces and why it matters, follow `sources.md` to copy the cited files into their feature working tree, and consult `applicability.md` for consumer-specific adaptation. The catalog is fully self-contained inside glpnet — no cross-repo lookups required to copy a pattern.

**Why this priority**: This is the catalog's reason for existing. Without it, the import is just dead documentation.

**Independent Test**: Pick `prereq-patterns/pglite/`. Following only files inside glpnet (governance + per-pattern files + cited source artifacts), bring up a working PGLite TCP endpoint on a fresh checkout. Success = a `psycopg` or `Npgsql` client can connect, run `SELECT 1`, and disconnect cleanly.

**Acceptance Scenarios**:

1. **Given** a fresh glpnet checkout with `prereq-patterns/pglite/` populated, **When** a developer reads `description.md` → `sources.md` and copies the cited files into a feature working tree, **Then** they can start the bridge and connect a standard Postgres client without consulting the AIGRID repo.
2. **Given** the pglite pattern is at `Status: active`, **When** a feature spec under `specs/` cites `prereq-patterns/pglite/`, **Then** the citation resolves to a real sub-directory with all three required files and no `(draft)` suffix on its `directory.md` line.
3. **Given** any of the other seven imported patterns at `Status: draft`, **When** a developer consults its `description.md`, **Then** the draft state is visible both on the `Status:` line and on `directory.md`'s ` (draft)` suffix, signalling that no glpnet feature has yet validated the adaptation.

---

### User Story 2 — pglite migration preserves glpnet's distinctive learnings (Priority: P1)

A glpnet developer who has used `docs/research/pgbridge-reference/bridge-direct.mjs` opens the migrated `prereq-patterns/pglite/` and finds: (a) the no-pg-gateway hand-rolled wire-protocol approach is preserved (so Npgsql / psqlODBC still work); (b) the two diagnosed bugs are still fixed (PGLite's implicit-Sync + pg-gateway response corruption); (c) AIGRID's `globalWorkChain`, per-connection `workChain`, `endsAtFlushBoundary()` flush detection, and synthetic `ROLLBACK` startup handshake are now incorporated; (d) the Windows `DETACHED_PROCESS` lifecycle and `sidecar.json` discovery convention are documented; (e) PGLite is pinned at `@electric-sql/pglite@0.2.17`.

**Why this priority**: P1 because the pglite pattern is the only one starting at `Status: active` and the only one with a real glpnet predecessor; getting this merge wrong silently breaks D2NET's future PGLite path.

**Independent Test**: Run a comparison checklist against the migrated `prereq-patterns/pglite/`'s cited bridge file. Every distinguishing feature of glpnet's pre-existing `bridge-direct.mjs` (no-pg-gateway hand-rolled startup, Npgsql / psqlODBC compatibility, the two bug fixes) is either present in the migrated file or explicitly listed as superseded with rationale. Every distinguishing feature of AIGRID's `pglite_bridge.mjs` (globalWorkChain, per-conn workChain, endsAtFlushBoundary, synthetic ROLLBACK, sidecar discovery) is likewise present or explicitly superseded.

**Acceptance Scenarios**:

1. **Given** the migrated pattern's cited bridge file, **When** a developer connects with Npgsql or psqlODBC, **Then** the connection succeeds (regression check against the original glpnet investigation).
2. **Given** the migrated pattern's cited bridge file, **When** two clients fire concurrent `Parse → Bind → Describe → Execute → Sync` pipelines, **Then** responses are not interleaved on the wire and neither client sees `lost synchronization with server` (regression check against AIGRID's invariant).
3. **Given** the feature's migration analysis document, **When** any reviewer reads it, **Then** every line of glpnet's `bridge-direct.mjs` and every line of AIGRID's `pglite_bridge.mjs` has been categorized as: present-in-merged / superseded-with-rationale / dropped-with-rationale.

---

### User Story 3 — Catalog governance is fully glpnet-local (Priority: P2)

A glpnet maintainer reviewing a PR that adds a new pattern can verify the PR against `prereq-patterns/howto.md` (catalog authoring contract) and the format contracts under `specs/011-prereq-patterns-catalog/contracts/` (per-file format) without leaving the glpnet repo. `policies.md` does not reference AIGRID artefacts (BreenLake, opskit feature 004) that don't exist in glpnet — Policy 2's destination convention is named in glpnet-local terms with concrete bootstrap deferred to a future glpnet feature.

**Why this priority**: P2 because catalog hygiene matters for review velocity, but the immediate value comes from the patterns themselves (P1).

**Independent Test**: Grep `prereq-patterns/` for `BreenLake`, `breenlake`, `aigrid`, `opskit`, `~/.aigrid/`, and `specs/00[1-9]-` (AIGRID feature numbers). Every match is either inside an explicit "external sibling reference" note or is absent. The format contracts referenced from `prereq-patterns/howto.md` resolve to files inside glpnet.

**Acceptance Scenarios**:

1. **Given** the imported `prereq-patterns/howto.md`, **When** a reviewer follows every link, **Then** every linked target resolves inside the glpnet repo (no AIGRID cross-references).
2. **Given** the imported `prereq-patterns/policies.md`, **When** a reviewer reads Policy 2, **Then** the destination convention is pinned to `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`, and BreenLake is mentioned only as an "external sibling, may share host" note.
3. **Given** any per-pattern file, **When** a reviewer follows its links to format contracts, **Then** the contracts resolve inside `specs/011-prereq-patterns-catalog/contracts/`, not into AIGRID's `specs/`.

---

### User Story 4 — Source attribution and bidirectional traceability (Priority: P3)

A glpnet developer who wants to understand where each pattern came from can trace each pattern back to its AIGRID source, and an AIGRID developer reading glpnet's catalog can recognise which patterns differ in glpnet's adapted form. Each per-pattern `sources.md` cites both the AIGRID upstream (with `@<branch>`) AND, where glpnet contributes its own learning (the pglite case), the glpnet artefact.

**Why this priority**: P3 because attribution is hygiene, not blocking value.

**Independent Test**: For each pattern's `sources.md`, every cited AIGRID upstream path resolves at the AIGRID repo root, and every cited glpnet path resolves inside glpnet. The `Action` column uses the closed vocabulary `Read` / `Copy` / `Model`.

**Acceptance Scenarios**:

1. **Given** any pattern's `sources.md`, **When** a developer follows the citations, **Then** every `Path` resolves to a real file (in glpnet or in AIGRID) and every `Upstream` includes `@<branch>` for pinning.
2. **Given** the pglite pattern's `sources.md`, **When** read end-to-end, **Then** it cites both AIGRID's `pglite_bridge.mjs` (under AIGRID upstream) and glpnet's pre-existing `docs/research/pgbridge-reference/bridge-direct.mjs` (under glpnet upstream), with summaries that explain what each contributed to the merged bridge.

---

### Edge Cases

- **Pre-existing glpnet artefact obsoletion**: After the pglite pattern lands with the merged bridge, what happens to `docs/research/pgbridge-reference/`? It is either removed with a redirect note pointing at `prereq-patterns/pglite/`, or retained as a historical reference with a `MIGRATED.md` stub. Disposition is decided during implementation, not in this spec.
- **Source artefact unavailable**: If AIGRID's `D:/BSTDEV/lang/hatzinor_ai-ddp/...` paths are not accessible to a glpnet developer's host, the pattern's `sources.md` citations are still pinned by `@<branch>` so the artefact is fetchable from the upstream repo. Patterns that cannot be reconstructed without local AIGRID access are flagged with a triviality note in `sources.md`.
- **Stack-incompatible patterns**: `flask-sqlalchemy-alembic-api` and `blazor-spa-bg-api` reference Python/.NET stacks glpnet does not currently use. They are imported at `Status: draft` with applicability triviality lines until a glpnet feature adopts them; no glpnet code is required to make them runnable in the catalog itself.
- **Policy 2 destination collision**: If a future glpnet feature defines a "glpnet datalake" destination at a different path than this spec's placeholder convention, `policies.md` is updated by that future feature to match. This spec names the convention but does not bootstrap any destination.
- **Concurrent unrelated edits**: Multiple sessions might edit `directory.md` (the catalog index). Each new pattern adds exactly one line at the end of the bullet list; conflicts are resolvable by reordering append-only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (catalog location)**: The catalog directory `prereq-patterns/` MUST be created at the glpnet repo root (peer of `specs/`, `docs/`, `programs/`, `glp_runtime/`, `glp_multiagent/`, `test/`).
- **FR-002 (governance files)**: `prereq-patterns/` MUST contain three catalog-level governance files: `directory.md`, `howto.md`, `policies.md`. None may be omitted.
- **FR-003 (all eight patterns imported)**: All eight source patterns MUST be brought across as sub-directories of `prereq-patterns/`: `pglite`, `dbos`, `flask-sqlalchemy-alembic-api`, `pglite-backup-restore`, `blazor-spa-bg-api`, `background-task-manager`, `local-secrets-store`, `secure-signatures`.
- **FR-004 (per-pattern files)**: Each pattern sub-directory MUST contain the three required files `description.md`, `applicability.md`, `sources.md`. Each file MUST have substantive content per the source `howto.md` format contract OR a single-line triviality statement; no file may collapse to its H1 header alone.
- **FR-005 (format contracts self-contained)**: The six format contracts MUST be copied verbatim from AIGRID into glpnet under `specs/011-prereq-patterns-catalog/contracts/`, then scrubbed of AIGRID-only references (per FR-011) to point at glpnet-local equivalents. The six are: `description_md_format.md`, `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`, `howto_md_format.md` (sourced from AIGRID `specs/001-prereq-patterns-pglite/contracts/`), and `policies_md_format.md` (sourced from AIGRID `specs/002-add-prereq-patterns-batch/contracts/`). The AIGRID-feature-specific files `howto_md_amendment.md` and `sibling_clone_convention.md` are NOT imported. `howto.md` and `policies.md` MUST link into glpnet's local copies, not AIGRID's `specs/`.
- **FR-006 (pglite is a merge, not an overwrite)**: The pglite pattern MUST be a true merge of (a) glpnet's existing `docs/research/pgbridge-reference/bridge-direct.mjs` and the surrounding files, and (b) AIGRID's `pglite_bridge.mjs` plus its surrounding artefacts. Neither side's distinctive learnings may be silently dropped.
- **FR-007 (glpnet pglite learnings preserved)**: The merged pglite pattern MUST preserve glpnet's distinctive learnings: hand-rolled minimal Postgres-wire server (no pg-gateway), the implicit-Sync-after-execProtocolRaw fix, the pg-gateway 0.3.0-beta.4 response-corruption avoidance, and Npgsql / psqlODBC client compatibility (verified by regression check).
- **FR-008 (AIGRID pglite learnings incorporated)**: The merged pglite pattern MUST incorporate AIGRID's distinctive learnings: `globalWorkChain` (global FIFO across all connections), per-connection `workChain`, `endsAtFlushBoundary()` flush-tag-aware buffering, synthetic `ROLLBACK` on startup handshake, Windows `DETACHED_PROCESS` lifecycle, `sidecar.json` host+port discovery convention, and `@electric-sql/pglite@0.2.17` version pin.
- **FR-009 (migration analysis is a deliverable)**: A migration analysis document MUST exist at `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` enumerating each distinguishing feature of glpnet's `bridge-direct.mjs` and AIGRID's `pglite_bridge.mjs`, with each feature classified as `present-in-merged` / `superseded-with-rationale` / `dropped-with-rationale`. Nothing may be left unclassified.
- **FR-010 (Policy 2 destination)**: `policies.md` Policy 2 MUST replace the BreenLake DuckLake destination convention with the glpnet-local destination `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`. This path lives off-repo as Policy 2 requires (sibling to the glpnet repo at `D:/BSTDEV/research/GLP/GLPNET/`, not inside it). Concrete *bootstrap* of the destination (creating the directory tree, defining ingest modes, writing settings) is OUT OF SCOPE of this feature and is explicitly deferred to a future glpnet feature; only the path convention is pinned here. BreenLake is mentioned in `policies.md` only as an "external sibling, may share host" note.
- **FR-011 (no AIGRID cross-references)**: All cross-references inside `prereq-patterns/` to AIGRID-only artefacts (BreenLake, opskit feature 004, `~/.aigrid/`, AIGRID feature numbers like `specs/003-breenlake-datalake/`) MUST be either rewritten to glpnet equivalents or removed. The single exception: `sources.md` files MAY cite AIGRID upstream paths in their `Path` column with `@<branch>` pinning, since `sources.md` is by definition the place where upstream attribution lives.
- **FR-012 (lifecycle states on import)**: Pattern lifecycle states MUST be set as follows on import: `pglite` → `active` (it has a working glpnet implementation backing it after the merge); all other seven patterns → `draft` (they have no glpnet consumer or implementation yet). Transitions to `active` happen in later features as glpnet consumers adopt patterns.
- **FR-013 (directory.md ordering)**: The new `directory.md` MUST list the eight imported patterns. `pglite` is listed first as the only `active` pattern; the other seven follow in source-`directory.md` order with the ` (draft)` suffix. Future patterns are appended chronologically per `howto.md`.
- **FR-014 (existing pgbridge-reference disposition)**: Once `prereq-patterns/pglite/` is in place with the merged bridge, the existing `docs/research/pgbridge-reference/` directory MUST receive an explicit disposition: either removed with a one-file forwarding note pointing at `prereq-patterns/pglite/`, or retained with a `MIGRATED.md` note recording its archival status. The disposition is captured during implementation, not pre-decided here.
- **FR-015 (no cleartext auth tokens, FR-CC-1 carry-over)**: `policies.md` Policy 1 (no cleartext auth tokens; secret-material hashes restricted to `{Argon2id, scrypt, bcrypt}`) MUST be brought across verbatim. The `Applies to` list is preserved (`dbos`, `flask-sqlalchemy-alembic-api`, `background-task-manager`, `local-secrets-store`).
- **FR-016 (applicability triviality default)**: Each newly-imported pattern's `applicability.md` MUST contain at minimum one substantive `### <consumer-name>` H3 OR the triviality line `Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.` Inherited substantive consumer notes from AIGRID are retained where they describe consumer-class behaviour rather than AIGRID-specific call sites.
- **FR-017 (sources.md citation discipline)**: Each pattern's `sources.md` MUST follow the existing format contract (4-column `Path | Upstream | Action | Summary` table + per-row sub-section). Citations point at the most appropriate upstream — for pglite, both glpnet's `docs/research/pgbridge-reference/` artefacts AND AIGRID's `pglite_bridge.mjs` cluster; for other patterns, AIGRID upstream paths with `@<branch>` pinning.
- **FR-018 (pglite applicability is a superset)**: `prereq-patterns/pglite/applicability.md` MUST contain a *superset* of the AIGRID Python consumer sections AND new glpnet .NET consumer sections. Required `### <consumer-name>` H3s: `### DBOS`, `### SQLAlchemy`, `### Alembic`, `### psycopg` (carried verbatim from AIGRID where the content describes consumer-class behaviour, scrubbed of AIGRID-internal call-site references), `### Npgsql`, `### psqlODBC` (new, describing the .NET stack adaptations: `Pooling=false` / queue-of-one connection discipline, no prepared-statement caching equivalent, behaviour against the merged hand-rolled wire-protocol bridge). A trailing `### Other consumers` H3 retains the partial-fit notes (`asyncpg`, `psycopg2`, ORM wrappers). This makes the pglite pattern the catalog's reference example of a multi-stack pattern.

### Key Entities *(include if feature involves data)*

- **Catalog**: The `prereq-patterns/` directory at glpnet repo root. Identified by location. Owns three governance files plus one sub-directory per pattern.
- **Governance files**: `directory.md` (index), `howto.md` (authoring contract), `policies.md` (cross-cutting rules). Catalog-level peers; mandatory.
- **Pattern**: A sub-directory under `prereq-patterns/` named with a lowercase-hyphen-separated basename. Owns three required files.
- **Per-pattern files**: `description.md` (what + why + how-a-feature-uses-it; carries the `Status:` line), `applicability.md` (per-consumer adaptation), `sources.md` (upstream citations).
- **Format contract**: A file under `specs/011-prereq-patterns-catalog/contracts/` defining the line / column / section shape of one of the five catalog files. Referenced from `howto.md` and `policies.md`.
- **Cross-cutting policy**: A section of `policies.md`. Identified by `Policy <N> — <title> (FR-CC-<N>)`. Has a `Rule.`, `Specifics.`, `Applies to.`, `Concrete details live in.` shape.
- **Migration analysis (pglite-specific)**: A document at `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` that classifies every distinguishing feature of the two pre-merge bridges.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 (catalog discoverability)**: A glpnet developer can locate the right pattern for a stated prerequisite (local Postgres / durable workflow / secrets / signing / background tasks) by reading `prereq-patterns/directory.md` alone in under 2 minutes.
- **SC-002 (catalog self-containment)**: 100% of links from `prereq-patterns/howto.md`, `prereq-patterns/policies.md`, and any per-pattern file resolve to targets inside the glpnet repo, except for `sources.md` upstream citations which intentionally point outside. Verified by a link-check script run as part of the implementation handover.
- **SC-003 (pglite Npgsql / psqlODBC regression-clean)**: A `psqlODBC` and an `Npgsql` client both connect, run `SELECT 1`, and disconnect cleanly against the migrated pglite bridge. No `lost synchronization with server` errors over 100 sequential connect-query-disconnect cycles.
- **SC-004 (pglite psycopg-style invariant holds)**: Two simulated `psycopg` clients firing concurrent `Parse → Bind → Describe → Execute → Sync` pipelines do not see interleaved responses. No `DuplicatePreparedStatement` errors with `prepare_threshold=None` set.
- **SC-005 (no learning lost)**: Every distinguishing feature of glpnet's pre-existing `bridge-direct.mjs` and of AIGRID's `pglite_bridge.mjs` is classified in `pglite-merge-analysis.md` as one of `present-in-merged`, `superseded-with-rationale`, `dropped-with-rationale`. Zero items unclassified.
- **SC-006 (governance fidelity)**: All eight imported patterns conform to `howto.md`'s required-files rule (three per-pattern files exist; none reduced to its H1 header). Verified by a script that walks `prereq-patterns/` and checks the file count and minimum body length per file.
- **SC-007 (lifecycle visibility)**: For every pattern, `description.md`'s `Status:` line and `directory.md`'s suffix agree (no `Status: draft` line paired with an unsuffixed `directory.md` entry, and vice versa). Drift is a defect by construction.
- **SC-008 (Policy 2 has no AIGRID dependency)**: A `grep -i 'breenlake\|aigrid\|opskit'` over `prereq-patterns/` returns matches only inside explicit "external sibling reference" notes; no live cross-reference to AIGRID artefacts remains in glpnet's catalog rules.

## Assumptions

- The AIGRID source repo at `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/` is reachable from the implementer's host at the time of this feature's implementation. Once the import lands in glpnet, the catalog is self-contained and no longer depends on AIGRID-host accessibility.
- The two diagnosed bugs in glpnet's `bridge-direct.mjs` (PGLite implicit-Sync, pg-gateway response corruption) are real and worth preserving — confirmed by glpnet's auto-memory and `docs/research/pgbridge-reference/README.md`.
- Glpnet has no current consumer for `flask-sqlalchemy-alembic-api` or `blazor-spa-bg-api`; these patterns are imported at `Status: draft` for catalog completeness and possible future use, not for immediate adoption.
- "BreenLake" and "opskit" are AIGRID-only artefacts not present in glpnet's universe; their replacement in `policies.md` Policy 2 is the glpnet-local destination `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet` (path pinned in this feature; bootstrap deferred to a future glpnet feature).
- Glpnet's CalVer + branching workflow (`docs/VERSIONING.md`, `docs/BRANCHING.md`) governs this feature: it lives on branch `011-prereq-patterns-catalog` and lands as a normal merge to `main`.
- This feature does NOT change any GLP language definition, runtime, or test behaviour; nothing in `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/` is modified by this feature. Only `prereq-patterns/`, `specs/011-prereq-patterns-catalog/`, and possibly `docs/research/pgbridge-reference/` (for FR-014 disposition).
- The `sources.md` citation discipline (4-column table + per-row sub-section + `Read`/`Copy`/`Model` action vocabulary + `@<branch>` pinning) is preserved verbatim from the source — no glpnet-specific change to the format itself.
