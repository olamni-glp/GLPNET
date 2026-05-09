# Contract — `prereq-patterns/<name>/sources.md` format

## Purpose

`sources.md` is the index an implementer follows to find — and decide what to do with — every upstream source artefact the pattern is modelled on. It is the most-read file in the pattern when actually adapting it, so its shape is the most strictly specified.

## File-level shape

```text
# Sources — <Pattern Name>

<optional 1-paragraph framing — what reference implementation this pattern is grounded in, and at what version/branch>

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| <path-1> | <upstream-1> | <action-1> | <summary-1> |
| <path-2> | <upstream-2> | <action-2> | <summary-2> |
| ... | ... | ... | ... |

## Per-source notes

### <path-1>

- <focus bullet 1>
- <focus bullet 2>
- ...

### <path-2>

- ...
```

## Index table — column rules

| Column | Type | Rule |
|---|---|---|
| `Path` | string | Repo-relative path (when implementing in *this* repo after copy) OR absolute on-disk hint to the upstream reference (e.g. `D:/REFS/<repo>/...`). The on-disk hint is preferred when the upstream copy is locally available; otherwise use the repo-relative path under the upstream identity. |
| `Upstream` | string | `<owner>/<repo>@<branch>` form. Example: `someorg/example-repo@main`. The `@<branch>` part MUST be present so citations remain pinned even if the default branch changes. |
| `Action` | enum | Exactly one of: `Read`, `Copy`, `Model`. Vocabulary is closed (see Action vocabulary below). |
| `Summary` | string | One sentence, ≤ 20 words recommended, period-terminated. Says what the file IS, not what to do with it (the `Action` column already says what to do). |

## Action vocabulary (closed set of three)

| Token | Meaning |
|---|---|
| `Read` | Read end-to-end for orientation. Do not copy or write your own derived version; the value is in understanding context. Typical examples: a design doc, a prompt, a meta-discussion of the pattern's evolution. |
| `Copy` | Copy the file (or substantial portions of it) into your feature's working tree, with at most light renaming for repo-local conventions (paths, module names). The file is project-agnostic enough to live in your repo with minimal change. |
| `Model` | Treat as a design reference. Write your own equivalent, informed by it. Do **not** copy verbatim — typically because the reference is too entangled with its host repo's conventions (e.g. imports a sibling module that doesn't exist in your repo). |

A future revision MAY extend this vocabulary if a genuinely new relationship type emerges. For v1: only these three.

## Per-source narrative — section rules

| Element | Rule |
|---|---|
| Heading | `### <path>` (H3). The heading text MUST equal the `Path` cell of the corresponding index-table row, character-for-character. |
| Order | Sub-sections appear in the same order as the index-table rows. No reordering, no orphans. |
| Body | Bullet list naming the specific functions, classes, line ranges, behaviours, or patches the implementer must focus on. Each bullet should be specific enough that the reader knows what to look for without re-reading the entire file. |
| Code spans | Function and class names appear in `backticks`. Line ranges appear as `lines NN-MM` or `lines NN-MM, PP-QQ`. |

## Triviality

A `sources.md` for a pattern with no upstream source code to cite (rare — mostly applies to documentation-only patterns) MUST contain:

```text
# Sources — <Pattern Name>

No external sources: this pattern is self-contained.
```

No index table, no per-source sections.

## Example — minimal valid (one source)

```text
# Sources — example-pattern

Reference implementation lives at `D:/REFS/example-repo/`, upstream `someorg/example-repo@main`.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/REFS/example-repo/src/example.py` | `someorg/example-repo@main` | Copy | Single-file utility implementing the pattern. |

## Per-source notes

### `D:/REFS/example-repo/src/example.py`

- Function `process()` (lines 12-47) is the pattern's core logic.
- The `_validate()` helper (lines 51-68) is optional; skip if your inputs are pre-validated.
- The module-level `CONFIG` dict (lines 1-9) holds defaults; override per consumer.
```

## Common errors to avoid

| Error | Why bad |
|---|---|
| Index table row exists but no matching `### <path>` sub-section | Orphan citation; reader can't find the focus bullets. |
| `### <path>` sub-section exists but no matching index row | Orphan section; doesn't appear in the at-a-glance table. |
| `Action` column says `Adapt` or `Inspect` or some other unlisted token | Closed vocabulary violation — readers don't know how to interpret the citation. |
| `Path` cell points at a directory, not a file | The pattern of citation is per-file. Cite each file separately. |
| `Upstream` missing `@<branch>` | Citation will rot if the default branch changes. |
