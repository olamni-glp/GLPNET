# Format Contracts — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09

This directory holds the six normative format contracts for the catalog files. Per FR-005, each contract was **copied verbatim from AIGRID then scrubbed of AIGRID-only references**. This README records what was actually imported during `/speckit-implement`.

## Source: AIGRID branch + commit pinned at import time

| Field | Value |
|---|---|
| AIGRID repo path (host-local) | `D:/BREENDEV/aigrid/AWS-Infra/` |
| Branch | `004a-opskit-sidecar-autospawn` |
| Commit SHA | `83b60585b886e06be9ea2d8954232649962b5d69` |
| Last commit subject | `004a: alembic migration runner + NullPool/AUTOCOMMIT DDL fix` |
| Imported on | 2026-05-09 |

All six contracts and the per-pattern `sources.md` `Upstream` columns pin to this branch+SHA pair. Future cross-references in the catalog use `@004a-opskit-sidecar-autospawn` (the branch component is mandatory; the SHA is the implementer's reproducibility anchor recorded here).

## Files (imported and scrubbed)

| File | Purpose | AIGRID source path | Scrubbing applied |
|---|---|---|---|
| `description_md_format.md` | Line/section shape of each pattern's `description.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/description_md_format.md@004a-opskit-sidecar-autospawn` | None — file had no AIGRID-only references. |
| `applicability_md_format.md` | Line/section shape of each pattern's `applicability.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/applicability_md_format.md@004a-opskit-sidecar-autospawn` | None — file had no AIGRID-only references. |
| `sources_md_format.md` | 4-column table + per-row sub-section shape of each pattern's `sources.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/sources_md_format.md@004a-opskit-sidecar-autospawn` | Replaced AIGRID-developer-host example paths (`D:/BSTDEV/lang/hatzinor_ai-ddp/...`, `olamni-research/hatzinor_ai-data-driven-publishing@develop`) with generic placeholders (`D:/REFS/<repo>/...`, `someorg/example-repo@main`). Convention text preserved verbatim. |
| `directory_md_format.md` | Bullet-list shape of `directory.md`, including `(draft)` suffix convention | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/directory_md_format.md@004a-opskit-sidecar-autospawn` | None — file had no AIGRID-only references. |
| `howto_md_format.md` | Section structure of `howto.md` (catalog authoring contract) | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/howto_md_format.md@004a-opskit-sidecar-autospawn` | One reference replaced: the `## File formats` section's link target was `specs/001-prereq-patterns-pglite/contracts/*.md` → now `specs/011-prereq-patterns-catalog/contracts/*.md`. |
| `policies_md_format.md` | `Rule. / Specifics. / Applies to. / Concrete details live in.` shape of each policy | `D:/BREENDEV/aigrid/AWS-Infra/specs/002-add-prereq-patterns-batch/contracts/policies_md_format.md@004a-opskit-sidecar-autospawn` | Removed framing reference to `feature 002-add-prereq-patterns-batch`; replaced `specs/001-prereq-patterns-pglite/contracts/` cross-reference with `specs/011-prereq-patterns-catalog/contracts/`; replaced FR-CC-3 / FR-CC-3a numerics in body prose with the generic phrases "no-restatement rule" and "allocation discipline" (the FR-CC-1 / FR-CC-2 parentheticals on policy headings are preserved by glpnet `policies.md` — see Policy 1 / Policy 2 there). |

## Excluded files (NOT brought across)

Per FR-005 and `research.md` A3, the following AIGRID-feature-specific files were NOT imported:

| AIGRID file | Why excluded |
|---|---|
| `howto_md_amendment.md` | AIGRID-feature-specific catalog amendment workflow; does not map to glpnet. |
| `sibling_clone_convention.md` | AIGRID-feature-specific cross-repo clone convention; does not map to glpnet. |

## Scrubbing rules (FR-011)

For traceability, the canonical scrubbing rules applied during import:

| AIGRID reference | glpnet replacement |
|---|---|
| `BreenLake`, `breenlake` | Footnote-only as "external sibling, may share host" |
| `aigrid` (lowercase, unless part of a path-pinned `Upstream` cell) | Removed; replaced by glpnet equivalent if any |
| `~/.aigrid/...` | Replaced with `D:/BSTDEV/research/glpnet-datalake/...` per FR-CC-2 |
| `opskit feature 004`, AIGRID feature numbers `specs/00[1-9]-...` | Removed from contract bodies; in the `Concrete details live in.` lines, replaced by glpnet-local references (e.g., `specs/011-prereq-patterns-catalog/contracts/<name>_format.md`) |
| Cross-references to AIGRID `specs/.../contracts/` | Replaced with `specs/011-prereq-patterns-catalog/contracts/` |

The single allowed retention is in `sources_md_format.md`: that file may *describe* upstream-citation conventions referencing AIGRID upstream paths, since `sources.md` itself is by design the place where upstream attribution lives (FR-011 exception). Concrete AIGRID paths in worked examples within the format contract are scrubbed; the convention itself (`@<branch>` pinning, 4-column table, `Read`/`Copy`/`Model` action vocabulary) is preserved.
