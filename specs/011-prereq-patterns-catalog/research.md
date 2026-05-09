# Phase 0 Research — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09

This file consolidates the decisions backing the spec and the implementation plan. Two categories: (A) decisions already resolved with the spec author and pinned in `spec.md` § Clarifications; (B) implementation-shape decisions made here in Phase 0 to remove ambiguity before `/speckit-tasks`. Every NEEDS CLARIFICATION from the spec scan is resolved below.

---

## A. Pre-resolved clarifications (mirrored from spec.md § Clarifications, Session 2026-05-09)

### A1 — pglite applicability scope

**Decision**: Superset. `prereq-patterns/pglite/applicability.md` carries all AIGRID Python consumer sections (DBOS, SQLAlchemy, Flask-SQLAlchemy, Alembic, psycopg, "Other consumers") AND adds new `### Npgsql` and `### psqlODBC` sections for glpnet's .NET stack.

**Rationale**: pglite becomes the catalog's reference example of a multi-stack pattern. Single-stack scoping (glpnet-only) would lose the Python upstream value; relegating Python to "external" creates drift between docs and reality (the upstream code is genuinely shared).

**Alternatives considered**:
- *glpnet-only*: rejected — loses the durable Python consumer guidance that already works upstream.
- *glpnet-primary with Python as external sibling*: rejected — Python is not external to the pglite **upstream**; demoting it would misrepresent the pattern.

### A2 — Policy 2 destination

**Decision**: Pin to `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet`. Sibling to glpnet repo root at `D:/BSTDEV/research/GLP/GLPNET/`, off-repo per Policy 2's "operational data lives outside the repository" rule.

**Rationale**: Mirrors AIGRID's `~/.aigrid/breenlake/...` convention adapted to glpnet's BSTDEV namespace; concrete enough that `policies.md` reads as instruction, not aspiration; off-repo so Policy 2 isn't self-contradictory; bootstrap (creating the tree, defining ingest modes, settings) is explicitly deferred to a future glpnet feature so this catalog import is not blocked on infrastructure.

**Alternatives considered**:
- *Stay deferred*: rejected — `policies.md` would ship with a TBD destination, weakening Policy 2 to opinion.
- *Inside-repo (`./datalake/`)*: rejected — violates Policy 2's off-repo invariant.
- *Shared with BreenLake (`~/.aigrid/breenlake/...`)*: rejected — re-introduces the AIGRID dependency the import is trying to remove. BreenLake remains an "external sibling, may share host" footnote.

### A3 — Source for the format contracts

**Decision**: Copy 6 contracts verbatim from AIGRID into `specs/011-prereq-patterns-catalog/contracts/`, then scrub AIGRID-only references. The 6: `description_md_format.md`, `applicability_md_format.md`, `sources_md_format.md`, `directory_md_format.md`, `howto_md_format.md` (from AIGRID `specs/001-prereq-patterns-pglite/contracts/`), and `policies_md_format.md` (from AIGRID `specs/002-add-prereq-patterns-batch/contracts/`). The AIGRID-feature-specific files `howto_md_amendment.md` and `sibling_clone_convention.md` are NOT brought across.

**Rationale**: Preserves fidelity to AIGRID's working contract shapes that have already shaped a tested catalog upstream; verbatim+scrub keeps the glpnet copy fully self-contained without re-deriving the contract from scratch and risking drift. The two excluded files are AIGRID-feature-specific (catalog amendment workflow, sibling-repo clone convention) — neither maps to glpnet, so importing them would be noise.

**Alternatives considered**:
- *Inline into governance files (`howto.md` + `policies.md`)*: rejected — bloats those files and entangles per-file format details with cross-cutting policy and authoring contract.
- *Copy + rewrite from first principles*: rejected — loses AIGRID's tested contract shape, introduces risk of subtle drift between glpnet's catalog and any future cross-pollination.
- *Reference AIGRID externally without copying*: rejected — violates the catalog self-containment goal (FR-011, SC-002).

---

## B. Phase-0 implementation-shape decisions

### B1 — `docs/research/pgbridge-reference/` disposition (FR-014)

**Decision**: Retain with a `MIGRATED.md` archival note. Files preserved in place; no redirect-only stub.

**Rationale**: The directory's `README.md` narrates the bug-discovery journey (PGLite implicit-Sync; pg-gateway 0.3.0-beta.4 response corruption) — a historical record worth preserving as a separate artefact from the merged bridge's `sources.md` summary, which describes outcome rather than journey. `MIGRATED.md` becomes the canonical pointer to `prereq-patterns/pglite/` for future readers.

**Alternatives considered**:
- *Remove with one-file forwarding note*: rejected — discards the bug-discovery narrative, which has independent reference value (cited by glpnet's own auto-memory).
- *Retain without `MIGRATED.md`*: rejected — leaves readers to discover the migration by accident; weakens SC-002 in spirit.

**Note**: This decision can still be re-litigated during `/speckit-implement` per FR-014 ("disposition is decided during implementation, not in this spec"). Recording the recommendation here so `/speckit-tasks` can produce concrete work items.

### B2 — Pglite merge structural approach

**Decision**: Use AIGRID's `pglite_bridge.mjs` as the **structural skeleton** and graft glpnet's `bridge-direct.mjs` no-pg-gateway startup path plus the two diagnosed bug fixes onto it. The merged file lands as `prereq-patterns/pglite/<bridge-filename>.mjs` (or stays cited from AIGRID upstream + glpnet upstream as the merge target — exact citation form is decided during implementation).

**Rationale**: AIGRID's serialization machinery (`globalWorkChain` global FIFO, per-connection `workChain`, `endsAtFlushBoundary()` flush detection, synthetic `ROLLBACK` on startup, `DETACHED_PROCESS` lifecycle) is the more invasive structural concern — harder to retrofit onto a different skeleton without subtle re-implementation bugs. Glpnet's distinguishing concerns (no pg-gateway, two specific bug fixes, Npgsql/psqlODBC compatibility) are localized: a hand-rolled startup path and two surgical patches. Grafting the localized concerns onto the more invasive skeleton minimizes the surface area for merge errors.

**Alternatives considered**:
- *Use glpnet skeleton + graft AIGRID serialization*: rejected — porting the chain semantics across skeletons risks subtle mis-implementation (e.g., `endsAtFlushBoundary` boundary cases); higher debugging cost.
- *Pick winner per concern (cherry-pick blend)*: rejected — loses architectural cohesion; produces a Frankenstein file that's harder to reason about than either upstream.

**Verification**: The migration-analysis document (FR-009 / `pglite-merge-analysis.md`) classifies every distinguishing feature of both pre-merge bridges so no learning is silently dropped. The Npgsql/psqlODBC regression check (SC-003) and the psycopg-style invariant check (SC-004) are documented in `prereq-patterns/pglite/sources.md` for future glpnet features to run.

### B3 — Pattern lifecycle states on import (FR-012)

**Decision**: `pglite` → `active`; the other seven (`dbos`, `flask-sqlalchemy-alembic-api`, `pglite-backup-restore`, `blazor-spa-bg-api`, `background-task-manager`, `local-secrets-store`, `secure-signatures`) → `draft`.

**Rationale**: pglite has a working glpnet implementation backing it after the merge — every `Status: active` requirement (a pattern is grounded by a glpnet artefact a feature can adopt) holds. The other seven have no glpnet consumer or implementation yet; calling them `active` would overstate readiness and break SC-007 (lifecycle visibility) by setting a precedent of inflated status.

**Alternatives considered**:
- *All draft*: rejected — understates pglite's readiness; future features wanting a local Postgres get a misleading "no patterns ready" signal.
- *All active*: rejected — overstates the seven; first downstream feature would discover unbacked patterns and lose trust in the catalog's lifecycle column.

### B4 — Conformance tooling for handover checks (SC-002, SC-006, SC-007, SC-008)

**Decision**: One-shot PowerShell script under `specs/011-prereq-patterns-catalog/` (e.g., `conformance-check.ps1`) — not committed to a permanent test suite. Run once during handover; output captured into the implementation report.

**Rationale**: The conformance checks are catalog-authoring discipline gates (link resolution, three-files-per-pattern, lifecycle drift, no-AIGRID grep), not runtime regressions. They belong with the feature's handover, not with `test/run_all_tests.sh`. PowerShell over Node/Bash because the host is Windows and the catalog deliberately avoids dependency creep.

**Alternatives considered**:
- *Add to `test/run_all_tests.sh`*: rejected — that suite tests GLP runtime behaviour; mixing in markdown-conformance dilutes its purpose.
- *npm `markdown-link-check` or Rust `lychee`*: rejected — drags a tool dependency into a no-dependency catalog. PowerShell `Resolve-Path` + `Select-String` is sufficient for the link-resolution and grep checks the spec calls for.

**Note**: If a future glpnet feature adopts another pattern, conformance can be promoted into a permanent test if the cost-benefit shifts.

### B5 — Citation form for AIGRID upstream paths (FR-017, FR-011 exception)

**Decision**: `sources.md` `Upstream` column carries paths in the form `<repo-relative path>@<branch>` where the repo is `D:/BREENDEV/aigrid/AWS-Infra/`. Example: `prereq-patterns/pglite/pglite_bridge.mjs@main`. The `@<branch>` is mandatory — no rolling-tip citations.

**Rationale**: Pinning by branch (and ideally by commit at implementation time) makes citations reproducible even if the AIGRID host moves; matches FR-011's explicit exception ("`sources.md` files MAY cite AIGRID upstream paths in their `Path` column with `@<branch>` pinning").

**Alternatives considered**:
- *Cite by commit SHA*: more precise but harder to maintain; `@<branch>` plus the implementer's note of the SHA-at-implementation in the per-row sub-section is the trade-off.
- *Cite by absolute filesystem path only*: rejected — fragile; assumes AIGRID at exactly `D:/BREENDEV/aigrid/...`.

---

## C. Best-practices research (AIGRID-style pattern catalogs)

| Practice | Source | Application here |
|---|---|---|
| Three-files-per-pattern (description / applicability / sources) | AIGRID `specs/001-prereq-patterns-pglite/` | FR-004 — codified as the catalog's required-file rule |
| Lifecycle states (`active` / `draft`) make adoption visible | AIGRID `directory.md` `(draft)` suffix convention | FR-012, FR-013, SC-007 — drift-detected by construction |
| Format contracts as separate normative files (not inlined) | AIGRID `specs/.../contracts/` | FR-005 — copied verbatim+scrubbed; supports independent reviewer grep-checking |
| Cross-cutting policies sit beside the catalog index, not inside patterns | AIGRID `policies.md` | FR-002, FR-010, FR-015 — `policies.md` is a peer of `directory.md` and `howto.md` |
| Bidirectional traceability via `sources.md` | AIGRID convention | FR-017, US4 acceptance scenarios |
| Catalog self-containment (no live external links from governance files) | AIGRID drift discipline | FR-011, SC-002, SC-008 — sole exception is `sources.md` upstream column |

---

## D. Open items deferred to `/speckit-implement`

These are intentionally not closed in Phase 0; recording them so `/speckit-tasks` can generate corresponding tasks rather than treat them as new ambiguities.

1. **Exact filename of the merged pglite bridge** under `prereq-patterns/pglite/` (e.g., `pglite_bridge.mjs` matching AIGRID, or `bridge-direct.mjs` matching glpnet, or a new neutral name). Decide during implementation by least-surprise to the most-likely future reader.
2. **Exact set of files cited from glpnet's `docs/research/pgbridge-reference/`** in pglite `sources.md`. Currently expected: at least `bridge-direct.mjs`, `README.md`, `package.json`. Confirm by re-reading the directory at implementation time.
3. **Disposition note text in `MIGRATED.md`** (B1) — short prose; finalized when the merged bridge filename is settled.
4. **AIGRID branch / commit SHAs** to pin in `sources.md` per B5 — captured at implementation time, not now.
