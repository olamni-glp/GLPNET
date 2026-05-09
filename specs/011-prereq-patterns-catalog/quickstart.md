# Quickstart — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09

After this feature lands, glpnet has a catalog of curated prerequisite patterns at `prereq-patterns/`. This file walks two flows from the user's standpoint plus the post-merge regression checks for the pglite pattern. It is the human-facing companion to `data-model.md` (which describes structure) and `research.md` (which records decisions).

---

## Flow A — Future glpnet feature locates and adopts a pattern

You are starting a new feature that needs a *prerequisite* — a local Postgres-compatible store, a durable workflow runtime, a secrets store, a signing surface, a background-task registry, etc. Use the catalog instead of re-deriving the design.

### Steps

1. **Open** `prereq-patterns/directory.md`. It lists every pattern with one-line descriptions. Patterns suffixed ` (draft)` are not yet adopted by any glpnet feature; pattern listed without a suffix are `active` (have a working glpnet implementation backing them).
2. **Identify** the pattern that matches your prerequisite. (Naming is intentional: `pglite` for local Postgres, `dbos` for durable workflow, `local-secrets-store` for secrets, etc.)
3. **Read** `prereq-patterns/<pattern>/description.md`. The `Status:` line tells you the pattern's lifecycle state. The body explains what the pattern produces, why it matters, and how a feature uses it.
4. **Consult** `prereq-patterns/<pattern>/applicability.md`. Find your consumer (`### DBOS`, `### Npgsql`, `### psqlODBC`, etc.). It documents the consumer-specific adaptations you need to apply on top of the pattern's reference implementation.
5. **Follow** `prereq-patterns/<pattern>/sources.md`. Each row of its 4-column table cites a source artefact: `Path | Upstream | Action | Summary`. The `Action` column is one of:
   - `Read` — read for context, do not copy.
   - `Copy` — copy this file (or its content) verbatim into your feature working tree.
   - `Model` — use as the structural model for your own re-implementation.
6. **Adopt** the pattern. If you are the first glpnet feature to adopt a `draft` pattern, update both the pattern's `description.md` `Status:` line and `directory.md` suffix in your feature's PR (this transition is part of your feature's scope, per the lifecycle rule in `data-model.md`).

### Worked example — pglite

> Goal: bring up a local Postgres-compatible endpoint a `psqlODBC` client can connect to.

```text
1. Open prereq-patterns/directory.md
   → see "pglite — local PGLite-backed Postgres-wire endpoint"
2. Read prereq-patterns/pglite/description.md
   → Status: active. Confirms a working glpnet implementation exists.
3. Open prereq-patterns/pglite/applicability.md → ### psqlODBC
   → Notes: connection string form, Pooling=false discipline, no
     prepared-statement caching equivalent, behaviour against the
     hand-rolled wire-protocol bridge.
4. Open prereq-patterns/pglite/sources.md
   → Row 1: Path=<merged bridge file>; Action=Copy
   → Row 2: Path=<package.json>; Action=Copy
   → Row 3: Path=docs/research/pgbridge-reference/README.md; Action=Read
5. Copy the rows marked `Copy` into your feature working tree. Run.
6. (No status change — pglite is already `active`.)
```

---

## Flow B — Maintainer adds a new pattern to the catalog

You have identified a new prerequisite worth curating in glpnet. Use the catalog's authoring contract.

### Steps

1. **Read** `prereq-patterns/howto.md` — the catalog's authoring contract. It tells you what a pattern is, when to add one, and the acceptance bar.
2. **Read** the relevant format contracts in `specs/011-prereq-patterns-catalog/contracts/` for the files you'll author:
   - `description_md_format.md`
   - `applicability_md_format.md`
   - `sources_md_format.md`
3. **Consult** `prereq-patterns/policies.md` — apply any cross-cutting policy whose `Applies to` list includes your new pattern.
4. **Create** the pattern directory `prereq-patterns/<your-pattern-name>/` with the three required files:
   - `description.md` — `Status: draft` (no glpnet consumer adoption yet)
   - `applicability.md` — at minimum the triviality line (`Universally applicable: …`), or a substantive `### <consumer-name>` H3 if you have a target consumer in mind
   - `sources.md` — the 4-column table with `@<branch>`-pinned upstream citations
5. **Update** `prereq-patterns/directory.md` — append one line at the end of the pattern list with the ` (draft)` suffix. (Append-only convention; do not reorder existing entries.)
6. **Commit** the new pattern dir + updated `directory.md` together. The conformance script (next section) verifies internal consistency.

---

## Flow C — Conformance handover check (one-shot, after `/speckit-implement`)

Run the catalog conformance script and capture its output as part of the implementation handover. The script enforces the structural rules in `data-model.md`.

### Checks performed

| ID | Check | What it asserts |
|---|---|---|
| C1 | three-files-per-pattern | Each pattern dir contains exactly `description.md`, `applicability.md`, `sources.md`; none reduced to its H1 header alone (SC-006, FR-004) |
| C2 | lifecycle agreement | Every pattern's `description.md` `Status:` line agrees with its `directory.md` suffix (SC-007) |
| C3 | catalog self-containment | Every link from any governance file or per-pattern file (excluding `sources.md` `Upstream` column) resolves inside the glpnet repo (SC-002) |
| C4 | no live AIGRID cross-references | `grep -i 'breenlake\|aigrid\|opskit'` over `prereq-patterns/` returns matches only inside explicit "external sibling reference" notes (SC-008, FR-011) |
| C5 | format-contract reachability | `prereq-patterns/howto.md` and `prereq-patterns/policies.md` link only into `specs/011-prereq-patterns-catalog/contracts/`, never into AIGRID `specs/` (FR-005 link target) |
| C6 | migration-analysis completeness | `pglite-merge-analysis.md` classifies every distinguishing feature of both pre-merge bridges as one of `{present-in-merged, superseded-with-rationale, dropped-with-rationale}`; zero unclassified (SC-005, FR-009) |

### Invocation

```powershell
# From repo root
.\specs\011-prereq-patterns-catalog\conformance-check.ps1
```

Expected output: `PASS: C1..C6` with file counts. Any FAIL is a defect to be fixed before merge.

---

## Flow D — Pglite regression checks (deferred to first glpnet adopter)

These two checks are **not** run during this catalog-import feature — they are documented in `prereq-patterns/pglite/sources.md` for future glpnet features that adopt the merged bridge.

### D1 — Npgsql / psqlODBC connectivity (SC-003)

A `psqlODBC` client AND an `Npgsql` client each:
- Connect to the merged bridge.
- Run `SELECT 1`.
- Disconnect cleanly.

Run 100 sequential connect-query-disconnect cycles. Pass criteria: zero `lost synchronization with server` errors; both clients succeed every cycle.

### D2 — Psycopg-style invariant (SC-004)

Two simulated `psycopg` clients each fire a `Parse → Bind → Describe → Execute → Sync` pipeline concurrently. Pass criteria: responses are not interleaved on the wire; neither client sees `lost synchronization with server`; with `prepare_threshold=None` set, no `DuplicatePreparedStatement` errors.

---

## Where to read more

| Topic | File |
|---|---|
| What patterns exist + lifecycle | `prereq-patterns/directory.md` |
| How to author a new pattern | `prereq-patterns/howto.md` |
| Cross-cutting rules (auth tokens, datalake destination) | `prereq-patterns/policies.md` |
| Per-file format contract | `specs/011-prereq-patterns-catalog/contracts/<name>_format.md` |
| Why this feature was scoped this way | `specs/011-prereq-patterns-catalog/research.md` |
| Catalog entity model + validation rules | `specs/011-prereq-patterns-catalog/data-model.md` |
| Pglite merge classification | `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` (authored during implementation) |
