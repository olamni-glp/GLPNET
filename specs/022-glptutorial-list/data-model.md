# Phase 1 Data Model: /glptutorial-list

**Feature**: `022-glptutorial-list` | **Date**: 2026-06-03
**Source**: [spec.md](./spec.md) Key Entities + [research.md](./research.md) D4–D7

The model is **in-memory only** — derived by a filesystem walk of the vendored
corpus at request time. No database, no persistence (D1: bridge-free).

---

## Entities

### Corpus
The root of the vendored snapshot the lister reads (FR-007).

| Field | Type | Notes |
|---|---|---|
| `root` | path | Default `<repo-root>/tutorials/olamni`; `--corpus` overrides. |
| `chapters` | `list[Tutorial]` | Discovered `chNN` dirs, sorted by id. |
| `overview_titles` | `dict[str,str]` | `chNN → human title` parsed from `tutorial.md` status table (D6). |
| `warnings` | `list[str]` | Non-standard dirs skipped (FR-011), emitted to stderr. |

Validation:
- `root` must exist and be readable, else **corpus-unreachable** error naming the
  path tried, non-zero exit, no partial listing (FR-006, Edge: corpus unreachable).

### Tutorial (chapter)
A chapter-level grouping `chNN` (spec "Tutorial").

| Field | Type | Notes |
|---|---|---|
| `id` | str | `chNN` (normalized lowercase, zero-padded). |
| `title` | str \| None | Human title (D6 precedence); None → render id only. |
| `exercises` | `list[Exercise]` | Sorted by exercise number; empty for planned chapters. |
| `is_empty` | bool | True when no recognizable `exercise-MM` dirs (FR-008). |

Validation / rules:
- A `chNN` dir with no `exercise-MM` children → `is_empty = True`, still listed
  with an explicit empty indicator (FR-008, US1 #3).
- Identifier matching against `id` and `title` per D5.

### Exercise
An `exercise-MM` directory — the **description anchor** (D4).

| Field | Type | Notes |
|---|---|---|
| `number` | str | `MM` from the dir name. |
| `dir` | path | The exercise directory. |
| `scripts` | `list[Script]` | One or more `.glp` files (D4). |
| `md_description` | str \| None | One-line summary from `ex-MM-tutorial.md` (D7 step 1). |

Validation / rules:
- An `exercise-MM` dir containing **no** `.glp` files is a non-standard shape →
  **skipped with a warning** (FR-011); it is not rendered as an empty exercise. A
  *chapter* with zero usable exercises is the distinct FR-008 empty-tutorial case
  (rendered with `(no scripts)`).
- Duplicate `MM` across different chapters is fine — always grouped under the
  owning chapter (Edge: duplicate exercise numbers).

### Script
A single `.glp` file — the **listable/runnable unit** (D4).

| Field | Type | Notes |
|---|---|---|
| `name` | str | The `.glp` file name (FR-003). |
| `path` | path | Repo-relative POSIX path under the corpus. |
| `description` | str | Resolved per D7; `(no description)` sentinel if none (US3 #2). |
| `description_source` | enum | `exercise_md` \| `glp_header` \| `none` (for `--json` / tests). |

Validation / rules:
- `description` is always a single trimmed line (collapse whitespace, length cap).
- Every `.glp` under a recognized exercise appears in output (SC-002, 100%
  coverage; never silently omitted).

---

## Relationships

```
Corpus 1───* Tutorial(chapter) 1───* Exercise 1───* Script(.glp)
   │                                     │
   └─ overview_titles (chNN→title)       └─ md_description (anchor; inherited/overridden per script)
```

- Read-only (FR-010): nothing is executed or mutated; the model is a projection
  of the filesystem.

---

## Description resolution (D7, FR-004 precedence)

For each `Script`:
1. **`exercise_md`** — from the exercise's `ex-MM-tutorial.md`: H1 descriptive
   tail (text after `—`) and/or first non-boilerplate paragraph, trimmed to one
   line.
2. **`glp_header`** — else the first informative leading `%%`/`%` comment line in
   the `.glp` (skipping a pure filename banner), trimmed.
3. **`none`** — else `(no description)`.

Multi-script exercises: the exercise `md_description` provides the shared anchor;
per-script `glp_header` disambiguates individual scripts (e.g. corrected vs
failing).

---

## Identifier matching (D5, FR-002 / SC-003)

Input normalized to lowercase, then matched in order:
1. exact `id`; 2. zero-pad-normalized id (`ch3`/`3` → `ch03`); 3. id prefix;
4. substring of `title`.

- 0 matches → "no match" + list of available chapter ids (non-zero exit, SC-003).
- ≥2 matches → list the candidate ids and ask to disambiguate by id.
- 1 match → list only that chapter (FR-002).

---

## State / lifecycle

Stateless per invocation. No transitions; the corpus is re-walked each run
(< 3 s, SC-005). Provenance of the snapshot itself lives in
`tutorials/olamni/SNAPSHOT.md` / `.snapshot.json` and is managed by
`codeconv tutorials sync` (D3), not by the list path.
