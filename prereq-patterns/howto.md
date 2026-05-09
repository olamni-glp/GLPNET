# How to author a prerequisite pattern

This catalog (`prereq-patterns/`) holds curated, ready-to-adopt prerequisite implementations — the kinds of artefacts a glpnet feature needs *before* its own work begins (a local Postgres, a durable workflow runtime, a secrets store, a signing surface, a background-task registry). Each pattern here is a consolidation of working code somewhere upstream, indexed so a downstream feature can copy or adapt it without re-deriving the design. This document is the contract every pattern in the catalog MUST follow.

The catalog has three catalog-level governance files at `prereq-patterns/`: **`howto.md`** (this file — authoring contract), **`directory.md`** (the index), and **`policies.md`** (cross-cutting rules). All three MUST exist; none may be omitted. Per-pattern files (`description.md`, `applicability.md`, `sources.md`) live under each pattern's sub-directory and are governed separately (see `## Required files per pattern` below).

## Where patterns live

Patterns live under `prereq-patterns/` at the glpnet repo root. Each pattern occupies its own sub-directory whose basename is the pattern's name. Names MUST be lowercase, hyphen-separated (`[a-z0-9][a-z0-9-]*`). The catalog is **single-level** in v1: no nested categories, no grouping sub-directories. If the catalog grows past ~15 patterns we revisit; until then a flat list is sufficient.

## Required files per pattern

Every pattern sub-directory MUST contain exactly three files at minimum:

- `description.md` — what the pattern produces and why it matters.
- `applicability.md` — how the pattern adapts to each known consumer.
- `sources.md` — index of upstream source artefacts the implementer reads, copies, or models from.

All three files are mandatory. **None may be omitted, even if its content is trivial.** When the substantive content for a file genuinely collapses to a single statement (e.g. a universally-applicable pattern with no per-consumer adaptation, or a documentation-only pattern with no upstream source code), the file MUST still exist and MUST contain a single explanatory line — for example:

- `applicability.md`: `Universally applicable: no glpnet consumers yet — applicability TBD when first glpnet feature adopts this pattern.`
- `sources.md`: `No external sources: this pattern is self-contained.`

The file is never empty, and never reduces to just its H1 header.

## File formats

The exact line-, column-, and section-level shape of each file is specified in the format contracts at `specs/011-prereq-patterns-catalog/contracts/`:

- `description.md` → [`specs/011-prereq-patterns-catalog/contracts/description_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/description_md_format.md)
- `applicability.md` → [`specs/011-prereq-patterns-catalog/contracts/applicability_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/applicability_md_format.md)
- `sources.md` → [`specs/011-prereq-patterns-catalog/contracts/sources_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/sources_md_format.md)
- `directory.md` → [`specs/011-prereq-patterns-catalog/contracts/directory_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/directory_md_format.md)
- `howto.md` (this file) → [`specs/011-prereq-patterns-catalog/contracts/howto_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/howto_md_format.md)
- `policies.md` → [`specs/011-prereq-patterns-catalog/contracts/policies_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/policies_md_format.md)

In short:

- **`description.md`** opens with `# <Pattern Name>`, then `Status: <state>` on the second non-blank line, then a body. Recommended sections (not enforced): `## What this produces`, `## Why it matters`, `## How a feature uses this pattern`.
- **`applicability.md`** opens with `# Applicability — <Pattern Name>`, then one `### <consumer-name>` H3 per known consumer. Each consumer sub-section has at minimum a one-line statement of "what changes from a vanilla setup". H2 headings are avoided to keep the read flow flat. A trailing `### Other consumers` H3 is allowed for partial / experimental fits.
- **`sources.md`** opens with `# Sources — <Pattern Name>`, then a 4-column index table (`Path | Upstream | Action | Summary`), then one `### <Path>` sub-section per row in the same order. The `Action` column is a closed vocabulary of three tokens: `Read`, `Copy`, `Model`. The `Upstream` column MUST include `@<branch>` so citations remain pinned.

## Lifecycle states

Every pattern has exactly one of three lifecycle states, declared on a `Status:` line at the top of its `description.md` (literal text, not YAML frontmatter):

| State | `description.md` line | `directory.md` suffix |
|---|---|---|
| `active` | `Status: active` | _(none — `active` is the implicit default)_ |
| `draft` | `Status: draft` | ` (draft)` |
| `superseded` | `Status: superseded by <pattern-name>` | ` (superseded by <pattern-name>)` |

The `<pattern-name>` in a `superseded by` line MUST be the basename of an existing sub-directory in `prereq-patterns/`. A superseded pattern's directory is **retained**, not deleted, so historical citations from prior feature specs remain resolvable.

Transitions are author-driven: edit the `Status:` line in `description.md`, update the suffix on the matching `directory.md` line in the same PR. Both edits MUST land together — a `Status:` line and a `directory.md` suffix that disagree is a defect.

## Cross-cutting rules

`prereq-patterns/policies.md` is the canonical home of cross-cutting rules that apply across multiple patterns. It is a peer of this file and of `directory.md`, and is mandatory.

- **No cleartext auth tokens** (FR-CC-1): no pattern's data plane MAY persist authentication tokens in cleartext; secrets MAY be hashed only with Argon2id, scrypt, or bcrypt and only when the pattern's `description.md` carries explicit written justification → see [`policies.md`, Policy 1](./policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1).
- **Non-config history off-repo to glpnet datalake** (FR-CC-2): configuration history needed for ongoing operation lives in pglite; non-config history (logs, traces, metrics, telemetry, audit events, ephemeral records) MUST be routed to an off-repo destination at `D:/BSTDEV/research/glpnet-datalake/<pattern-or-app>/<data-class>/<partition>.parquet` and MUST NOT be committed to this repo → see [`policies.md`, Policy 2](./policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2).

**Allocation discipline.** `policies.md` says *what* the rule is; affected patterns say *how* the rule is realised in their domain. Concrete pattern-specific details — the chosen secret-hash algorithm and parameters, the runtime config-key for the datalake destination, the per-pattern destination filename, the unreachable-destination fallback — are written into the affected pattern's `description.md` (or, where appropriate, `applicability.md`), NOT into `policies.md`. Each affected pattern cross-links back to `policies.md` for the rule itself.

**No-restatement rule.** Affected patterns MUST cross-link to `policies.md`; they MUST NOT restate the policy text in their own `description.md` / `applicability.md`. Drift between `policies.md` and a pattern's restatement is a defect by construction.

## Registering a pattern in directory.md

Every pattern sub-directory MUST have exactly one matching line in `prereq-patterns/directory.md`. The line shape is fixed (see [`specs/011-prereq-patterns-catalog/contracts/directory_md_format.md`](../specs/011-prereq-patterns-catalog/contracts/directory_md_format.md) for the full grammar):

```text
- **<name>** — <description>.[ <status_suffix>]
```

- Bullet: `- ` (hyphen + space).
- `<name>`: bolded, exactly the sub-directory basename, lowercase + hyphen-separated.
- Separator: ` — ` (space, em-dash U+2014, space). Not `--`. Not `-`.
- `<description>`: one sentence, sentence-cased, period-terminated, **≤ 25 words** excluding the optional status suffix. Captures both purpose and applicability hint.
- `<status_suffix>`: empty when state is `active`; otherwise ` (draft)` or ` (superseded by <pattern-name>)`.

Pattern lines appear in the order they were added (chronological). New patterns are appended at the end of the bullet list. Within a single feature batch (i.e. one PR adding multiple patterns), the lines are appended in the spec's priority order (P1, P2, …); across features, ordering remains chronological per feature.

**Updating `directory.md` is the LAST step of any new-pattern PR.** A PR that adds a sub-directory under `prereq-patterns/` without a matching `directory.md` line is incomplete. Before requesting review, verify that:

1. The pattern's sub-directory exists with all three required files.
2. `description.md` has a `Status:` line declaring the pattern's state.
3. `directory.md` has exactly one new line for this pattern, with the correct status suffix.

## Authoring discipline

Patterns in this catalog are **consolidations of proven implementations**, not theoretical designs. The act of authoring a pattern is the act of taking something that already works in real source code somewhere and indexing it so the next implementer can adopt it without re-discovering the design.

Therefore:

- A pattern MUST be grounded in working source code. `sources.md` cites that source code, with `Upstream` identity and `Action` per row.
- A pattern that cites no sources and has no triviality justification is incomplete. Either the pattern has upstream code to model on (cite it), or it is self-contained (state so explicitly with a `triviality_line` in `sources.md`).
- Do not author a pattern for something you have not actually implemented or seen implemented. If the implementation is hypothetical, it belongs in a feature spec or a future-features document, not in this catalog.

## When a pattern is "done"

A pattern is ready to be cited from a glpnet feature spec when:

1. Its `description.md` declares `Status: active`.
2. Its `directory.md` line is present with no status suffix (the implicit `active`).
3. All three files (`description.md`, `applicability.md`, `sources.md`) exist and have either substantive content per their format contract or a single-line triviality statement justifying a thin body.
4. `sources.md` either cites at least one upstream artefact with `Path` + `Upstream` + `Action` + `Summary` and a matching `### <Path>` sub-section, OR contains a `No external sources:` triviality line.
5. `applicability.md` either has at least one `### <consumer-name>` H3 with concrete adaptation notes, OR contains a `Universally applicable:` triviality line.

Until all five conditions hold, leave `Status: draft` on `description.md` and the ` (draft)` suffix on `directory.md`. A pattern in `draft` state SHOULD NOT be cited from a glpnet feature spec.
