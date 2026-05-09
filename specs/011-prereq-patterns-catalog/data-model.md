# Phase 1 Data Model — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09

The "data model" for a documentation-catalog feature is the entity-relationship structure of its content. This file enumerates the catalog's entities, their fields, their relationships, the validation rules drawn from the spec's functional requirements, and the lifecycle state transitions for the only stateful entity (Pattern).

---

## Entity Diagram

```text
Catalog ─┬── 3 Governance Files (directory.md, howto.md, policies.md)
         │       │
         │       └── policies.md owns N Cross-Cutting Policies (FR-CC-1, FR-CC-2, …)
         │
         ├── 8 Patterns
         │       │
         │       └── each owns 3 Per-Pattern Files (description.md, applicability.md, sources.md)
         │                       │
         │                       └── each conforms to one Format Contract
         │
         └── (associated, lives under specs/011/contracts/) 6 Format Contracts

Specs Directory (specs/011-prereq-patterns-catalog/)
    └── owns 1 Migration Analysis (pglite-specific, FR-009)
```

---

## Entity: Catalog

The top-level container. Identified by location.

| Field | Type | Validation |
|---|---|---|
| `location` | filesystem path | MUST equal `prereq-patterns/` at glpnet repo root (FR-001) |
| `governance_files` | set of 3 file paths | MUST contain exactly `directory.md`, `howto.md`, `policies.md` (FR-002) |
| `patterns` | set of pattern dirs | MUST contain exactly the 8 source patterns (FR-003) |

**Relationships**: owns 3 Governance Files (1:1 by name); owns 8 Patterns (1:N); references 6 Format Contracts (under `specs/011-prereq-patterns-catalog/contracts/`, M:N — each pattern's three files reference one contract each).

---

## Entity: Governance File

A catalog-level file. Three instances exist; identified by filename.

| Field | Type | Validation |
|---|---|---|
| `name` | enum | One of `directory.md`, `howto.md`, `policies.md` |
| `path` | filesystem path | `prereq-patterns/<name>` |
| `content` | Markdown | MUST conform to its associated Format Contract |
| `links` | set of internal references | All MUST resolve inside the glpnet repo (FR-011, SC-002); the only off-repo references permitted are inside `sources.md` (which is a per-pattern file, not a governance file) — governance files have no off-repo references |

**Per-instance rules:**

- `directory.md`:
  - MUST list all 8 imported patterns; pglite first (active); other 7 in source-`directory.md` order with ` (draft)` suffix (FR-013)
  - Suffix MUST agree with each pattern's `Status:` line (SC-007); drift is a defect by construction
  - Append-only thereafter — new patterns add one line at the end (Edge case 5)
- `howto.md`:
  - MUST link into `specs/011-prereq-patterns-catalog/contracts/`, NOT into AIGRID's `specs/` (FR-005 link target)
- `policies.md`:
  - MUST contain Policy 1 (no cleartext auth tokens, FR-CC-1) verbatim; `Applies to` list preserved (FR-015)
  - MUST contain Policy 2 with destination pinned to `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet` (FR-010)
  - MUST mention BreenLake only as an "external sibling, may share host" footnote — no live cross-reference (FR-011)

---

## Entity: Pattern

A sub-directory under `prereq-patterns/`. Eight instances exist; identified by basename.

| Field | Type | Validation |
|---|---|---|
| `name` | string | Lowercase, hyphen-separated; one of the 8 fixed values |
| `path` | filesystem path | `prereq-patterns/<name>/` |
| `status` | enum | `active` or `draft` (FR-012, see state transitions below) |
| `description_md` | Per-Pattern File | MUST exist; MUST contain `Status:` line agreeing with `directory.md` suffix (SC-007) |
| `applicability_md` | Per-Pattern File | MUST exist; substantive content OR triviality line per FR-016 |
| `sources_md` | Per-Pattern File | MUST exist; MUST follow the 4-column table + per-row sub-section format (FR-017) |

**Initial values on import (FR-012):**

| Pattern | Initial Status |
|---|---|
| `pglite` | `active` |
| `dbos` | `draft` |
| `flask-sqlalchemy-alembic-api` | `draft` |
| `pglite-backup-restore` | `draft` |
| `blazor-spa-bg-api` | `draft` |
| `background-task-manager` | `draft` |
| `local-secrets-store` | `draft` |
| `secure-signatures` | `draft` |

### State transitions for `status`

```text
        ┌─────────────┐                                 ┌─────────────┐
        │             │   first glpnet feature adopts   │             │
        │   draft     │ ──────────────────────────────► │   active    │
        │             │   the pattern + the pattern's   │             │
        └─────────────┘   merged artefact lands         └─────────────┘
              ▲                                                │
              │                                                │
              │            (no demotion in this feature;       │
              └────────────  any rule-level "deactivation" ────┘
                             is out of scope here)
```

**Transition rule**: A pattern moves from `draft` to `active` in a *future* feature, not in this one. The feature making the transition is responsible for updating both `description.md` `Status:` line and `directory.md` suffix in the same change so SC-007 holds.

---

## Entity: Per-Pattern File

One of `description.md`, `applicability.md`, `sources.md` inside a Pattern directory. Three per pattern × 8 patterns = 24 instances.

| Field | Type | Validation |
|---|---|---|
| `name` | enum | `description.md`, `applicability.md`, `sources.md` |
| `format_contract` | reference | One of the 6 Format Contracts (by name match, e.g., `description.md` → `description_md_format.md`) |
| `min_content` | rule | MUST NOT collapse to its H1 header alone (FR-004) — at minimum a substantive H3 or a single-line triviality statement |
| `internal_links` | set | MUST resolve inside glpnet (FR-011) |
| `external_links` | set | Permitted ONLY in `sources.md` `Upstream` column with `@<branch>` pinning (FR-011 exception, FR-017) |

**Per-name rules:**

- `description.md`: MUST contain a `Status:` line matching the pattern's `directory.md` suffix (SC-007)
- `applicability.md`:
  - MUST contain at minimum one substantive `### <consumer-name>` H3 OR the triviality line `Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.` (FR-016)
  - For the pglite pattern specifically: MUST be a superset — `### DBOS`, `### SQLAlchemy`, `### Alembic`, `### psycopg`, `### Npgsql`, `### psqlODBC`, `### Other consumers` (FR-018)
- `sources.md`: MUST follow the 4-column `Path | Upstream | Action | Summary` table + per-row sub-section format (FR-017); `Action` ∈ `{Read, Copy, Model}` (closed vocabulary, US4 Independent Test); `Upstream` cells pin with `@<branch>`

---

## Entity: Format Contract

A normative file under `specs/011-prereq-patterns-catalog/contracts/` defining the line/column/section shape of one catalog file. Six instances; identified by filename.

| Field | Type | Validation |
|---|---|---|
| `name` | enum | `description_md_format.md`, `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`, `howto_md_format.md`, `policies_md_format.md` |
| `path` | filesystem path | `specs/011-prereq-patterns-catalog/contracts/<name>` |
| `source_aigrid_path` | upstream reference | One of: AIGRID `specs/001-prereq-patterns-pglite/contracts/<name>` (5 files) or AIGRID `specs/002-add-prereq-patterns-batch/contracts/<name>` (1 file: `policies_md_format.md`) |
| `content_origin` | rule | Verbatim copy from `source_aigrid_path`, then scrubbed of AIGRID-only references per FR-011 (FR-005) |

**Excluded files** (NOT brought across, FR-005): AIGRID's `howto_md_amendment.md` and `sibling_clone_convention.md` — both AIGRID-feature-specific.

---

## Entity: Cross-Cutting Policy

A section of `policies.md`. Identified by ordinal + `FR-CC-<N>` tag.

| Field | Type | Validation |
|---|---|---|
| `ordinal` | integer | Sequential within `policies.md` (Policy 1, Policy 2, …) |
| `tag` | string | `FR-CC-<N>` matching ordinal |
| `title` | string | Short descriptive title |
| `rule` | Markdown block | MUST start with the line `Rule.` |
| `specifics` | Markdown block | MUST start with the line `Specifics.` |
| `applies_to` | Markdown block | MUST start with the line `Applies to.`; lists pattern names |
| `concrete_details_in` | Markdown block | MUST start with the line `Concrete details live in.`; references format contracts or other catalog files |

**Initial set (FR-015, FR-010):**

| # | Title | Tag | Applies to |
|---|---|---|---|
| 1 | No cleartext auth tokens; secret-material hashes restricted to `{Argon2id, scrypt, bcrypt}` | FR-CC-1 | `dbos`, `flask-sqlalchemy-alembic-api`, `background-task-manager`, `local-secrets-store` |
| 2 | Operational data lives outside the repository at `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet` | FR-CC-2 | (TBD per pattern by future glpnet feature) |

---

## Entity: Migration Analysis (pglite-specific)

A document at `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` (FR-009). Authored during `/speckit-implement`.

| Field | Type | Validation |
|---|---|---|
| `path` | filesystem path | `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` |
| `glpnet_features` | set of distinguishing features of `bridge-direct.mjs` | Each MUST be classified as one of `{present-in-merged, superseded-with-rationale, dropped-with-rationale}` (FR-009, SC-005) |
| `aigrid_features` | set of distinguishing features of `pglite_bridge.mjs` | Each MUST be classified as one of `{present-in-merged, superseded-with-rationale, dropped-with-rationale}` (FR-009, SC-005) |
| `unclassified_count` | integer | MUST equal 0 (SC-005) |

**Required-feature checklists** (the spec's FR-007 + FR-008 enumerate the floor; the implementer expands as needed):

*Glpnet floor (FR-007)*: hand-rolled minimal Postgres-wire server (no pg-gateway); implicit-Sync-after-execProtocolRaw fix; pg-gateway 0.3.0-beta.4 response-corruption avoidance; Npgsql / psqlODBC client compatibility (verified by SC-003 regression).

*AIGRID floor (FR-008)*: `globalWorkChain`; per-connection `workChain`; `endsAtFlushBoundary()`; synthetic `ROLLBACK` on startup handshake; Windows `DETACHED_PROCESS` lifecycle; `sidecar.json` host+port discovery; `@electric-sql/pglite@0.2.17` version pin.

---

## Cross-Entity Validation Rules (the conformance gates)

These are the SC checks turned into structural assertions that the handover script (`conformance-check.ps1`, B4 in research.md) validates:

| Rule | Source SC | What it checks |
|---|---|---|
| Three-files-per-pattern | SC-006, FR-004 | Each pattern dir contains exactly `description.md`, `applicability.md`, `sources.md`; none reduced to its H1 header alone |
| Lifecycle agreement | SC-007 | For every pattern: `description.md` `Status:` line and `directory.md` suffix agree |
| Catalog self-containment | SC-002 | Every link from any governance file or per-pattern file (excluding `sources.md` `Upstream` column) resolves inside the glpnet repo |
| No live AIGRID cross-references | SC-008, FR-011 | `grep -i 'breenlake\|aigrid\|opskit'` over `prereq-patterns/` returns matches only inside explicit "external sibling reference" notes |
| Migration analysis classification complete | SC-005, FR-009 | Every distinguishing feature of either pre-merge bridge is classified; zero unclassified |
| Pglite Npgsql / psqlODBC connectivity | SC-003 | (Future check, documented in `pglite/sources.md`; not run during catalog import) |
| Pglite psycopg-style invariant | SC-004 | (Future check, documented in `pglite/sources.md`; not run during catalog import) |
