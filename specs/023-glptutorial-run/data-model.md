# Phase 1 — Data Model: /glptutorial-run

**Feature**: `023-glptutorial-run` | **Date**: 2026-06-04 | **Spec**: [spec.md](./spec.md)
**Depends on**: [research.md](./research.md)

The run layer adds a thin *resolution + execution* model on top of the
feature-022 corpus model (`Corpus → Tutorial → Exercise → Script`), which it reuses
unchanged for selection. All entities below are plain frozen dataclasses (no DB,
bridge-free, D1).

---

## Reused from feature 022 (selection front-end)

`codeconv.tutorials.corpus` already provides, from the **vendored** snapshot:

- `Corpus(root, root_rel, chapters, overview_titles, warnings)`
- `Tutorial(id, title, exercises, is_empty)` — chapter `chNN`
- `Exercise(number, dir, scripts, md_description)` — the **uniform selectable unit**
- `Script(name, path, description, description_source)`

The run layer consumes these via `load_corpus()` + `match_tutorial()`. It adds no
new walk (D2).

---

## New entities (run layer)

### 1. `RunnableExample` — the resolved unit a run operates on

The bridge between a selected 022 `Exercise` and an executable run (spec Key
Entity "Runnable example"). Resolved by the resolver (D2–D5) from a `(Tutorial,
Exercise)` pair.

| Field | Type | Notes |
|---|---|---|
| `chapter_id` | `str` | e.g. `ch01`, `ch07` |
| `exercise_number` | `str` | e.g. `01` |
| `shape` | `Shape` | `SECTION_DRIVEN` \| `USE_CASE_DRIVEN` \| `NOT_IMPLEMENTED` |
| `load_target` | `LoadTarget` | what the backend loads (entity 2) |
| `goals` | `tuple[Goal, ...]` | documented goals; primary first (entity 3) |
| `golden` | `tuple[GoldenOutcome, ...]` | outcome-only expected results, by goal (entity 5) |
| `guide_path` | `str` | repo-rel path to `ex-MM-tutorial.md` (explain anchor) |
| `guide_text` | `str` | full guide prose (explanation source, D8) |

**Validation.** `shape == NOT_IMPLEMENTED` ⇒ no `load_target`/`goals` (exit 9).
A use-case `RunnableExample` MUST have exactly one primary play goal (`fplayMM`).

**State / lifecycle.** `selected → resolved → (previewed | executed) → explained`.
Resolution is read-only; only execution touches the REPL; only an approved
`propose --apply` mutates the corpus.

---

### 2. `LoadTarget` — what the backend loads (FR-003)

| Field | Type | Notes |
|---|---|---|
| `kind` | `LoadKind` | `SINGLE_FILE` \| `PROJECT_DIR` |
| `select_path` | `str` | repo-rel path in the **vendored** snapshot (provenance/preview) |
| `exec_path` | `str` | absolute path under the **sibling** execution root (D4) — what is actually loaded |

**Resolution rules (D3–D5):**

- **Section-driven** (`Exercise.scripts` non-empty, D2): `kind = SINGLE_FILE`;
  `select_path` = the chosen `Script.path`; `exec_path` =
  `<sibling-corpus>/<same-relpath-as-vendored>` (D4 root #1). If an exercise has
  multiple scripts, the user selects which (else the first is primary).
- **Use-case-driven** (`Exercise.scripts` empty + guide present, D2):
  `kind = PROJECT_DIR`; `exec_path` = `<sibling-glp-root>/programs/cssg_modules`
  for ch07 (D4 root #2, D5) — the canonical project, **not** the stale in-corpus
  `ch07/cssg-modules/`; `select_path` records the guide directory for provenance.
- **Not implemented** (stub chapter, D2): no `LoadTarget` → exit 9.

**Validation.** `exec_path` MUST exist on disk before a run; absence ⇒ exit 6 with
the path that was tried (FR-016). For `PROJECT_DIR`, module load order is delegated
to the REPL's directory `loadProject` (D5); a failed/missing module is reported by
the backend naming the module (FR-017).

---

### 3. `Goal` — a REPL goal to run (FR-004)

| Field | Type | Notes |
|---|---|---|
| `text` | `str` | the goal source, ending in `.` (e.g. `merge([1,2,3],[a,b],Xs).`) |
| `is_primary` | `bool` | the headline goal (the play goal `fplayMM.` for use-case) |
| `needs_limit` | `int \| None` | reduction limit to set first (e.g. `1000000` for plays) |
| `source` | `GoalSource` | `GUIDE` \| `USER_SUPPLIED` |

**Resolution rules (D3):** goals are the guide's `GLP> <goal>.` lines that are not
`:`-meta and not the load line; `needs_limit` is captured from a preceding
`:limit N`. No resolvable goal ⇒ exit 7 unless the user supplies one
(`--goal "<text>"`, `source = USER_SUPPLIED`). A goal that matches a documented REPL
limitation (struct-inside-list in a goal; `=..` in a clause body — CLAUDE.md
known-issues) is flagged before running ⇒ exit 10, not a crash (FR-016).

---

### 4. `Backend` — the REPL engine (FR-006/007/018)

| Field | Type | Notes |
|---|---|---|
| `kind` | `BackendKind` | `CSHARP` (default) \| `DART` |
| `available` | `bool` | resolved at run time (build present / exe found) |
| `invocation` | `list[str]` | argv to launch the line-oriented REPL |
| `unavailable_reason` | `str \| None` | populated when `available is False` |

**Rules (D6):** default `CSHARP` (`out/csharp/glp_repl` via `dotnet run` or a built
exe); `DART` on demand (sibling `dart run bin/glp_repl.dart` / `glp_repl.exe`). A
`CSHARP` that is unavailable or yields a wrong result is a **P1 defect** — surfaced
loudly (exit 8), optional Dart fallback only with a prominent P1 notice, never a
silent pass/hang (a run timeout converts a hang into a reported P1).

---

### 5. `GoldenOutcome` & `ActualOutcome` — outcome-only results (FR-008)

Shared shape (`Outcome`), one per goal:

| Field | Type | Notes |
|---|---|---|
| `bindings` | `tuple[Binding, ...]` | `Binding(name, value)`; value may be `<unbound>` |
| `status` | `Status` | `SUCCEEDS` \| `SUSPENDED` \| `FAILED` |
| `raw` | `str` | the captured/parsed text block (provenance) |

- `GoldenOutcome` is parsed from `ex-MM-repl-trace.md` (D7).
- `ActualOutcome` is parsed from backend stdout (D7) — identical grammar across
  backends (D6).
- **Normalization (D7):** fresh-variable tokens (`X<digits>`) are canonicalized
  before comparison; ground bindings and `status` compare verbatim.

---

### 6. `Verdict` — comparison result (FR-009)

| Field | Type | Notes |
|---|---|---|
| `kind` | `VerdictKind` | `MATCH` \| `DIFFERENCE` \| `NO_GOLDEN` |
| `diffs` | `tuple[Diff, ...]` | per-binding / status differences (empty on MATCH) |
| `explanation` | `str` | assembled from the guide prose + the verdict (D8) |
| `backend_used` | `BackendKind` | which backend produced the actual outcome |
| `p1_notice` | `str \| None` | set when a C# P1 defect was hit / Dart fallback used |

**Rules (D8):** `SUSPENDED` is a valid `status` (never a failure) where the guide
documents it (FR-010). A `DIFFERENCE` is always surfaced and explained — never a
silent pass. `NO_GOLDEN` ⇒ report the actual outcome and state no golden exists.

---

### 7. `Proposal` — read-only restructuring suggestion (FR-013/019)

| Field | Type | Notes |
|---|---|---|
| `id` | `str` | stable identifier |
| `kind` | `ProposalKind` | `RUN_MANIFEST` \| `DRIFT_GAP` \| `STALE_ARTEFACT` \| `LAYOUT_NORMALISE` |
| `chapter_id` / `exercise_number` | `str` | scope |
| `rationale` | `str` | why this improves consistency |
| `target_sibling_path` | `str` | the sibling source-of-truth file it would touch (apply only) |
| `applied` | `bool` | False unless approval-gated apply ran |

**Rules (D9):** generation is read-only. `--apply` requires explicit per-example
approval + recorded rationale, targets the sibling then re-vendors, is
semantics/clause-text-preserving and independently revertible (FR-019).

---

## Enumerations

```
Shape        = SECTION_DRIVEN | USE_CASE_DRIVEN | NOT_IMPLEMENTED
LoadKind     = SINGLE_FILE | PROJECT_DIR
GoalSource   = GUIDE | USER_SUPPLIED
BackendKind  = CSHARP | DART
Status       = SUCCEEDS | SUSPENDED | FAILED
VerdictKind  = MATCH | DIFFERENCE | NO_GOLDEN
ProposalKind = RUN_MANIFEST | DRIFT_GAP | STALE_ARTEFACT | LAYOUT_NORMALISE
```

---

## Resolution flow (read-only until execution)

```
(chapter, exercise) selector
   │  match_tutorial + load_corpus            (reuse 022)
   ▼
Exercise ──► detect Shape (D2: scripts empty?) ──► NOT_IMPLEMENTED ─► exit 9
   │
   ├─ SECTION_DRIVEN ─► LoadTarget(SINGLE_FILE, sibling-corpus path)
   └─ USE_CASE_DRIVEN ─► LoadTarget(PROJECT_DIR, sibling-glp-root/programs/cssg_modules)
   │
   ▼  parse ex-MM-tutorial.md (D3)
Goals (primary = fplayMM for use-case) ──► none? ─► exit 7 (unless --goal)
   │  parse ex-MM-repl-trace.md (D7)
GoldenOutcome[]
   ▼
RunnableExample  ──preview──► show goals + expected outcome (no exec, FR-005)
                 ──run──────► Backend (D6) → ActualOutcome (D7)
                 ──explain──► Verdict (D8) vs golden, referencing guide
```

Drift guard (D4): before any run, `sync --check` mismatch ⇒ warn + refuse (exit 11).
