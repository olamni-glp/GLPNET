# Contract — `prereq-patterns/howto.md` format

## Purpose

`howto.md` is the catalog's governance document. It is the canonical answer to: how do new patterns get authored, where do they live, what shape must they have, and how are they registered? Everything else in the catalog implements rules stated here.

## File-level shape

`howto.md` MUST contain at minimum the following sections (H2 headings), in this order:

```text
# How to author a prerequisite pattern

<1-paragraph framing of what this catalog is for>

## Where patterns live

<state: prereq-patterns/<name>/ at the repo root, lowercase-hyphenated name, one level deep, no nested categories in v1>

## Required files per pattern

<state: description.md, applicability.md, sources.md — all three mandatory; cross-link or restate the format contracts>

## File formats

<state: link to the contracts/ specs OR restate them inline>
- description.md → format
- applicability.md → format
- sources.md → format

## Lifecycle states

<state: three states (draft, active, superseded), Status: line at top of description.md, surfacing rules in directory.md>

## Registering a pattern in directory.md

<state: format of a directory.md line; the registration step is the LAST step of any new-pattern PR; a sub-directory without a directory.md entry is incomplete>

## Authoring discipline

<state: patterns MUST be grounded in real working source code, not theoretical designs; sources.md cites that source code; the act of authoring a pattern is the act of consolidating something proven, not designing something new>

## When a pattern is "done"

<state: Status: active in description.md; entry in directory.md; the three files cover their respective concerns; ready to be cited from a feature spec>
```

The author MAY add further sections (e.g. an FAQ, a worked example, project-specific naming conventions) but the seven H2 sections above (plus the H1 title and framing paragraph) are mandatory.

## Section rules

| Section | Required content |
|---|---|
| `## Where patterns live` | Names the directory (`prereq-patterns/`), the naming rule (lowercase-hyphenated), the depth (single level — no nested categories in v1). |
| `## Required files per pattern` | States the three mandatory files (`description.md`, `applicability.md`, `sources.md`), no omissions. References the trivial-content rule (file exists with one explanatory line). |
| `## File formats` | Either restates the per-file contracts inline or links to `specs/011-prereq-patterns-catalog/contracts/*.md`. The author chooses; restate-inline is acceptable for a self-contained `howto.md`, link-out keeps it short. |
| `## Lifecycle states` | The three states (`draft`, `active`, `superseded`), the `Status:` line in `description.md`, the `directory.md` annotation rule (omit when active, `(draft)` when draft, `(superseded by <name>)` when superseded). |
| `## Registering a pattern in directory.md` | The line format (cross-link to `directory_md_format.md` contract). Critically: the registration step is the LAST step of a new-pattern PR. A PR adding a sub-directory without a `directory.md` entry is incomplete. |
| `## Authoring discipline` | Patterns are *consolidations* of proven implementations, not designs. `sources.md` cites the proven implementation. A pattern that cites no sources and has no triviality justification is incomplete. |
| `## When a pattern is "done"` | The completion checklist: `Status: active`, `directory.md` entry present, three files exist with substantive (or justified-trivial) content, ready to cite from a feature spec. |

## Tone

Imperative. "MUST", "MUST NOT", "MAY". `howto.md` is a contract with future authors; soft language ("you might want to consider…") makes it easier to drift.

## Length

Aim for ≤ 200 lines of body. Past that, `howto.md` becomes the kind of doc no one reads, which defeats its purpose. If a section needs more than that to explain, factor it into a contract or link out.
