# Format Contracts — prereq-patterns catalog (glpnet)

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09

This directory holds the six normative format contracts for the catalog files. Per FR-005, each contract is **copied verbatim from AIGRID then scrubbed of AIGRID-only references**. The actual content files are populated during `/speckit-implement` — this README is a Phase-1 placeholder enumerating what is expected and where it comes from.

## Files (populated during /speckit-implement)

| File | Purpose | AIGRID source path |
|---|---|---|
| `description_md_format.md` | Line/section shape of each pattern's `description.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/description_md_format.md@<branch>` |
| `applicability_md_format.md` | Line/section shape of each pattern's `applicability.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/applicability_md_format.md@<branch>` |
| `sources_md_format.md` | 4-column table + per-row sub-section shape of each pattern's `sources.md` | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/sources_md_format.md@<branch>` |
| `directory_md_format.md` | Bullet-list shape of `directory.md`, including `(draft)` suffix convention | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/directory_md_format.md@<branch>` |
| `howto_md_format.md` | Section structure of `howto.md` (catalog authoring contract) | `D:/BREENDEV/aigrid/AWS-Infra/specs/001-prereq-patterns-pglite/contracts/howto_md_format.md@<branch>` |
| `policies_md_format.md` | `Rule. / Specifics. / Applies to. / Concrete details live in.` shape of each policy | `D:/BREENDEV/aigrid/AWS-Infra/specs/002-add-prereq-patterns-batch/contracts/policies_md_format.md@<branch>` |

## Excluded files (NOT brought across)

Per FR-005 and research.md A3, the following AIGRID-feature-specific files are NOT imported:

| AIGRID file | Why excluded |
|---|---|
| `howto_md_amendment.md` | AIGRID-feature-specific catalog amendment workflow; does not map to glpnet |
| `sibling_clone_convention.md` | AIGRID-feature-specific cross-repo clone convention; does not map to glpnet |

## Scrubbing rules (FR-011)

When copying each file, replace or remove:

| AIGRID reference | glpnet replacement |
|---|---|
| `BreenLake`, `breenlake` | Footnote-only as "external sibling, may share host" |
| `aigrid` (lowercase, unless part of a path-pinned `Upstream` cell) | Removed; replaced by glpnet equivalent if any |
| `~/.aigrid/...` | Replaced with `D:/BSTDEV/research/glpnet-datalake/...` per FR-CC-2 |
| `opskit feature 004`, AIGRID feature numbers `specs/00[1-9]-...` | Removed from contract bodies; in the `Concrete details live in.` lines, replaced by glpnet-local references (e.g., `specs/011-prereq-patterns-catalog/contracts/<name>_format.md`) |
| Cross-references to AIGRID `specs/.../contracts/` | Replaced with `specs/011-prereq-patterns-catalog/contracts/` |

The single allowed retention is in `sources_md_format.md`: that file may *describe* upstream-citation conventions referencing AIGRID upstream paths, since `sources.md` itself is by design the place where upstream attribution lives (FR-011 exception). Concrete AIGRID paths in worked examples within the format contract are scrubbed; the convention itself (`@<branch>` pinning, 4-column table, `Read`/`Copy`/`Model` action vocabulary) is preserved.

## Implementation note

When `/speckit-implement` runs the import:

1. Verify the AIGRID host is reachable (per spec assumption #1). If not, surface as a blocker.
2. For each file in the table above, fetch from the listed AIGRID source path.
3. Record the AIGRID branch + commit SHA at fetch time (for traceability in this README's `<branch>` placeholders, replaced by concrete values post-import).
4. Apply scrubbing rules.
5. Verify the scrubbed file still parses as Markdown and renders without broken internal references.
6. Replace this README's `<branch>` placeholders with concrete branch + SHA strings; promote this README from "expected files" enumeration to "what was actually imported" record.
