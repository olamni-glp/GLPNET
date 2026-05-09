# Handover — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog`
**Date**: 2026-05-09
**Author**: Claude Code session (Opus 4.7)
**Status**: Implementation complete — ready for review and merge

---

## Summary

This feature lands the AIGRID `prereq-patterns/` discipline into glpnet as a new
top-level peer at `prereq-patterns/`. The catalog ships with three governance
files (`directory.md`, `howto.md`, `policies.md`), eight imported per-pattern
sub-directories each with three required files (`description.md`,
`applicability.md`, `sources.md`), six format contracts copied verbatim and
scrubbed under `specs/011-prereq-patterns-catalog/contracts/`, a merged pglite
bridge at `prereq-patterns/pglite/pglite_bridge.mjs` consolidating glpnet's
no-pg-gateway investigation with AIGRID's serialization / lifecycle additions,
and a migration-analysis document classifying every distinguishing feature of
either pre-merge bridge with zero unclassified.

A PowerShell conformance script (`conformance-check.ps1`) enforces the six
structural invariants C1–C6; the final pre-merge run is recorded at the bottom
of this document.

---

## Conformance results (C1–C6)

Captured run output: [`conformance-output.txt`](./conformance-output.txt).

| Check | Status | Headline |
|---|---|---|
| C1 — three-files-per-pattern | PASS | 8 pattern dirs; each has all 3 required files; no file reduced to H1 alone |
| C2 — lifecycle agreement | PASS | 8 patterns: `description.md` `Status:` line agrees with `directory.md` suffix |
| C3 — catalog self-containment | PASS | 109 internal markdown links across 27 catalog files; all resolve inside glpnet |
| C4 — no live AIGRID cross-references | PASS | 75 total grep hits; 75 in allowed contexts (`sources.md` files or "external sibling" notes); 0 live cross-references |
| C5 — format-contract reachability | PASS | `howto.md` and `policies.md` link only to `specs/011-prereq-patterns-catalog/contracts/` for format-contract refs |
| C6 — migration-analysis completeness | PASS | 34 rows classified across 2 tables; all classifications valid; rationale non-empty; "Unclassified: 0" present |

Initial T031 run found 17 missed-scrubbing instances under C4 (description.md
and applicability.md prose mentions of "AIGRID upstream's" / "AIGRID's
catalog" / "the AIGRID Python reference") plus one C6 markdown-table-parsing
defect (the script split row A7's Feature cell on its escaped pipe `\|`).
Both classes were resolved during T032:

- **C4 fixes**: prose rewordings to remove the "AIGRID" name from
  `description.md` and `applicability.md` files. The phrase pattern "AIGRID
  upstream's `applicability.md`" became "the upstream catalog's
  `applicability.md`"; "consolidated upstream in AIGRID's catalog" became
  "consolidated in the curated upstream catalog"; the AIGRID-internal change
  order reference ("per AIGRID's CO #37 note") became "per the upstream
  catalog's recorded operational note"; the AIGRID Python sidecar mention in
  `pglite/description.md` became "Python sidecar reference / Python sidecar
  cited in [sources.md]". The substantive citations of AIGRID upstream paths
  were retained inside `sources.md` files (the FR-011 exception — `sources.md`
  is the canonical home for upstream attribution).
- **C6 fix**: changed `[regex]::Split` in the conformance script to use the
  negative-lookbehind regex `(?<!\\)\|` so markdown-escaped pipes inside table
  cells are no longer treated as column separators; the unescape step
  (`$_ -replace '\\\|','|'`) restores the cell content faithfully.

Re-run after fixes: all six checks PASS.

---

## Phase 6 — attribution audit (T034 / T035 / T036)

| Task | Status | Evidence |
|---|---|---|
| T034 — `Action` column ⊆ `{Read, Copy, Model}` across every `sources.md` | PASS | Every `Action` cell in every `prereq-patterns/*/sources.md` is one of `Read` or `Copy`; the closed vocabulary's third token (`Model`) is admissible but not used by this import (no row currently calls for "use as a structural model" beyond what `Read` already implies in context). No `Use` / `Reference` / `Adapt` / etc. found. |
| T035 — every AIGRID `Upstream` cell pinned `@<branch>` matching T003 | PASS | Every AIGRID upstream cell across the seven non-pglite patterns and the four AIGRID rows in `pglite/sources.md` reads exactly `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` (the branch+SHA pair recorded in `contracts/README.md` from T003). The five `this repo (canonical)` rows in `pglite/sources.md` carry no `@<branch>` because they are first-party glpnet artefacts, which is correct per FR-017 (the pin requirement applies to upstream citations, not to canonical in-repo files). |
| T036 — `pglite/sources.md` cites BOTH AIGRID's `pglite_bridge.mjs` AND glpnet's `bridge-direct.mjs` | PASS, after fix | Initial state cited glpnet's `bridge-direct.mjs` but did not cite AIGRID's pre-merge `pglite_bridge.mjs` as a standalone row. Fixed by appending the missing AIGRID upstream row plus a per-source notes section explaining what each contributed (`globalWorkChain` / synthetic `ROLLBACK` / `endsAtFlushBoundary` / CLI surface / `[bridge]` log prefix / `EADDRINUSE` exit-code split present-in-merged; COPY-FROM-STDIN interception path A4–A8 dropped-with-rationale). `docs/research/pgbridge-reference/package.json` was also added as a `Read` row alongside `bridge-direct.mjs` and `README.md` to close T028's enumerated citation list. |

---

## FR-014 disposition decision (recorded in T029)

`docs/research/pgbridge-reference/` was **retained** with a `MIGRATED.md`
forwarding note authored at `docs/research/pgbridge-reference/MIGRATED.md`.
Rationale per `research.md` § B1:

- The directory holds three reference bridges from glpnet's no-pg-gateway
  investigation (`bridge-traced.mjs`, `bridge-batched.mjs`, `bridge-direct.mjs`)
  plus the README narrative of the diagnostic journey. The investigation's
  *narrative value* — which bridge was used for which diagnostic step, why
  pg-gateway 0.3.0-beta.4 was eventually skipped, how Npgsql / psqlODBC
  compatibility falls out of the no-pg-gateway choice — would be lost if the
  directory were deleted, and the merged bridge's commit message cannot
  reasonably carry it.
- The `MIGRATED.md` file makes the forwarding pointer explicit: future readers
  arriving at `docs/research/pgbridge-reference/` are pointed at
  `prereq-patterns/pglite/` as the canonical glpnet artefact, with the
  pre-merge directory framed as archival.

---

## Deviations from spec / plan (flagged for review)

1. **T034: `Model` action token unused.** The closed vocabulary `{Read, Copy,
   Model}` is preserved by the spec, but every `Action` cell in this import
   landed as `Read` or `Copy`. This is not a defect (the vocabulary admits all
   three; `Model` was never required), but flagged so a reviewer can confirm
   the third token is intentionally held in reserve for a future pattern that
   carries a structural-model citation rather than a copy-or-read citation.

2. **T036 / T028 fix-forward.** T028 enumerated `bridge-direct.mjs`, `README.md`,
   `package.json` from `pgbridge-reference/` plus AIGRID's `pglite_bridge.mjs`
   as required citations in `pglite/sources.md`. Initial T028 implementation
   missed the AIGRID `pglite_bridge.mjs` row and the `pgbridge-reference/
   package.json` row. Both were added during T036 (Phase 6) with full
   per-source notes. No information was lost; the catalog is now strictly
   more complete than at the close of Phase 4.

3. **C4 prose rewordings under T032.** The T010 scrubbing pass during Phase 2
   focused on the *format-contract* files under
   `specs/011-prereq-patterns-catalog/contracts/`. The per-pattern
   `description.md` and `applicability.md` files authored in Phase 3 retained
   prose mentions of "AIGRID upstream" / "AIGRID's catalog" / "AIGRID's CO #37
   note" / "the AIGRID Python reference" / "the AIGRID `~/.aigrid/secrets/`
   convention" / "via AIGRID". These were rephrased to "upstream catalog" /
   "curated upstream catalog" / "upstream catalog's recorded operational
   note" / "Python sidecar reference" / "upstream catalog's home-directory
   secrets convention" / "via the upstream catalog" during T032. The
   substantive AIGRID-path citations remain in `sources.md` (the FR-011
   exception — `sources.md` is the canonical home for upstream attribution).
   Net effect: every file outside `sources.md` reads as glpnet-self-contained
   prose; no AIGRID name leaks outside `sources.md` or the explicit
   "external sibling" footnote in `policies.md`.

4. **Constitution check vacuous.** As recorded in `plan.md`, the project's
   speckit constitution at `.specify/memory/constitution.md` is an unfilled
   template stub. The de-facto constitution lives in `CLAUDE.md` and
   `docs/DISCIPLINE.md`; this feature was cross-checked against those.
   Filling the formal constitution is a project-wide governance action outside
   this feature's scope — flagged as a remediation candidate for a future
   `/speckit-analyze` run.

5. **SC-003 / SC-004 deferred.** These two pglite-specific runtime regression
   checks are intentionally NOT executed by this catalog-import feature.
   `prereq-patterns/pglite/sources.md` carries the verbatim turn-key procedures
   (Flow D1 / Flow D2) for the first glpnet feature that *adopts* the merged
   bridge to run. Mitigation in this feature: the static analogue —
   `pglite-merge-analysis.md` classifying every distinguishing feature of
   either pre-merge bridge with zero unclassified — is `PASS` (C6).

---

## Files changed by this feature

### Added — top-level catalog

```
prereq-patterns/
├── howto.md
├── policies.md
├── directory.md
├── pglite/
│   ├── description.md
│   ├── applicability.md
│   ├── sources.md
│   ├── pglite_bridge.mjs
│   └── package.json
├── dbos/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── flask-sqlalchemy-alembic-api/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── pglite-backup-restore/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── blazor-spa-bg-api/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── background-task-manager/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
├── local-secrets-store/
│   ├── description.md
│   ├── applicability.md
│   └── sources.md
└── secure-signatures/
    ├── description.md
    ├── applicability.md
    └── sources.md
```

### Added — speckit feature artefacts

```
specs/011-prereq-patterns-catalog/
├── contracts/
│   ├── README.md                       # promoted to "what was imported" record (T012)
│   ├── description_md_format.md
│   ├── applicability_md_format.md
│   ├── sources_md_format.md
│   ├── directory_md_format.md
│   ├── howto_md_format.md
│   └── policies_md_format.md
├── pglite-merge-analysis.md
├── conformance-check.ps1
├── conformance-output.txt
└── handover.md                         # this file
```

### Added — research-area forwarding note

```
docs/research/pgbridge-reference/MIGRATED.md
```

### Modified

- `specs/011-prereq-patterns-catalog/tasks.md` — task-status checkboxes updated
  through Phases 1–7 as work landed.

### Intentionally unchanged

- No `programs/`, `glp_runtime/`, `glp_multiagent/`, or `test/` files are
  added or modified. This is a documentation-catalog feature; the only "code"
  produced is the merged JavaScript bridge under `prereq-patterns/pglite/` and
  the PowerShell conformance script under `specs/011-prereq-patterns-catalog/`.
- `VERSION` is not bumped on this feature branch — CalVer applies on `main`
  after merge per `CLAUDE.md` and `docs/VERSIONING.md` (see "CalVer slot"
  below).

---

## CalVer slot for merge-time tag (T038)

**Intended slot**: `v2026.05.09` (or `v2026.05.09-N` if a same-day prior
release on `main` already claimed the bare slot).

Per `docs/VERSIONING.md` and `CLAUDE.md`, tag minting is the operator's
post-merge action, not the feature branch's responsibility. The handover
records the intended slot so that the operator can apply it without
re-deriving today's date at merge time.

**Note**: a `VERSION` file does not currently exist at the repo root. If
the operator's merge process expects one, creating it (with content
`v2026.05.09`) is part of the merge step, not part of this feature.

---

## CHANGELOG entry (T039)

A new H2 section `## [v2026.05.09] — 2026-05-09` was appended to `CHANGELOG.md`
under the existing H1, summarising the catalog-import surface (governance
files, eight pattern dirs, six format contracts, merged pglite bridge,
migration analysis, conformance script, FR-014 disposition) and citing
`specs/011-prereq-patterns-catalog/spec.md`, `plan.md`, `tasks.md`.

---

## Final pre-merge gate (T037)

Run `conformance-check.ps1` once more from the repo root immediately before
merge. Expected output:

```
[PASS] C1: 8 pattern dirs; each has all 3 required files; no file reduced to H1 alone
[PASS] C2: 8 patterns: description.md Status: line agrees with directory.md suffix
[PASS] C3: 109 internal markdown links across 27 catalog files; all resolve inside glpnet
[PASS] C4: 75 total grep hits; 75 in allowed contexts (sources.md or 'external sibling' notes); 0 live cross-references
[PASS] C5: 2 governance file(s) checked; format-contract links land only in specs/011-prereq-patterns-catalog/contracts/
[PASS] C6: pglite-merge-analysis.md: 34 rows classified across 2 tables; all classifications valid; Rationale non-empty; 'Unclassified: 0' present
OVERALL: PASS — all checks (C1..C6) green.
```

Final-gate result will be appended below this line during T037.

### Final-gate run

Executed 2026-05-09 from repo root via
`pwsh -NoProfile -File specs\011-prereq-patterns-catalog\conformance-check.ps1`.
Exit code: `0`.

```
=== prereq-patterns catalog conformance check ===
Repo root : D:\bstdev\research\glp\glpnet
Catalog   : D:\bstdev\research\glp\glpnet\prereq-patterns
Spec dir  : D:\bstdev\research\glp\glpnet\specs\011-prereq-patterns-catalog

[PASS] C1: 8 pattern dirs; each has all 3 required files; no file reduced to H1 alone
[PASS] C2: 8 patterns: description.md Status: line agrees with directory.md suffix
[PASS] C3: 109 internal markdown links across 27 catalog files; all resolve inside glpnet
[PASS] C4: 75 total grep hits; 75 in allowed contexts (sources.md or 'external sibling' notes); 0 live cross-references
[PASS] C5: 2 governance file(s) checked; format-contract links land only in specs/011-prereq-patterns-catalog/contracts/
[PASS] C6: pglite-merge-analysis.md: 34 rows classified across 2 tables; all classifications valid; Rationale non-empty; 'Unclassified: 0' present

OVERALL: PASS — all checks (C1..C6) green.
```

Captured at [`conformance-output.txt`](./conformance-output.txt).
