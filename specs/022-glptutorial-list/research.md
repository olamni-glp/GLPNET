# Phase 0 Research: /glptutorial-list — GLP tutorial browser

**Feature**: `022-glptutorial-list` | **Date**: 2026-06-03
**Input**: [spec.md](./spec.md)

This document resolves every open decision the spec deferred to planning. Each
decision lists the options considered, then a recommendation (the choice this
plan adopts). Nothing below remains `NEEDS CLARIFICATION`.

---

## Ground truth observed in the corpus

Verified against the authoritative sibling corpus
`D:/bstdev/research/glp/GLP/olamni/tutorial/` (the vendoring source):

```
tutorial/
├── tutorial.md                     # top-level overview + "Chapter status" table (chNN → name)
├── charter.md                      # non-tutorial doc
├── ch01/ … ch13/
│   ├── chNN_tutorial.md            # chapter guide (implemented chapters)
│   ├── chNN-sources.md             # source notes (planned chapters carry ONLY this)
│   ├── chNN-specification-input-prompt.md
│   ├── spec-rev-eng-input/         # NON-STANDARD dir (FR-011 skip-with-warning case)
│   └── exercise-MM/
│       ├── ch-NN-ex-MM-<slug>.glp  # ONE OR MORE .glp scripts per exercise
│       ├── ex-MM-tutorial.md       # per-exercise guide (primary description source)
│       └── ex-MM-repl-trace.md     # outcome-only golden (consumed by /glptutorial-run, not the lister)
```

Confirmed facts that shape the design:

1. **An exercise can contain more than one `.glp` script.** `ch03/exercise-01`
   has `…-glp-fair-stream-merger.glp` + `…-producer-consumer.glp` (loaded
   together as one pipeline). `ch05/exercise-06` and `ch05/exercise-07` each have
   a `…-corrected.glp` + `…-failing.glp` pair. The spec's Key Entities treat
   "script" ≈ `exercise-MM`; reality is finer. → **Decision D4.**
2. **Goals live inside `ex-MM-tutorial.md`**, not in a standalone goals file; the
   golden is `ex-MM-repl-trace.md`. The lister needs neither — only name +
   description — so this is informational.
3. **Planned chapters (ch08–ch13)** have a `chNN-sources.md` but no
   `exercise-MM/` dirs → they are the FR-008 "empty tutorial" case.
4. **Chapter human titles** are tabulated in the top-level `tutorial.md`
   "Chapter status" table (e.g. ch03 → "GLP Core"). → **Decision D6.**

---

## D1 — Where the Python engine lives and how it is invoked (CENTRAL)

The spec mandates "BOTH a Claude skill and a Python tool … the Python tool is the
engine, the skill is the thin front-end" (FR-009). The hard constraint is that
the lister is **read-only, < 3 s, no external dependencies** (FR-010, SC-005) —
in particular it must **not** spin up the PGLite bridge or DBOS.

Key fact from `codeconv/src/codeconv/cli.py`: built-in commands registered with
`@app.command(...)` (`list`, `doctor`, `migrate`) and sub-apps wired with
`app.add_typer(...)` are **bridge-free**; only *tool subpackages* discovered via
`runner.tool_registry()` "trigger bridge acquisition + engine + DBOS bootstrap"
(cli.py docstring). `equiv/corpus.py` is the precedent for a **pure**
(filesystem-only) module living inside the codeconv package.

| Option | Description | Verdict |
|---|---|---|
| **A. Pure sub-app on the codeconv CLI** | New pure package `codeconv/src/codeconv/tutorials/`; expose `codeconv tutorials list [TUTORIAL]` by wiring a Typer sub-app via `app.add_typer(...)` directly in `cli.py` (NOT through `tool_registry`). Bridge-free, exactly like `codeconv list`. Reuses Typer, pytest, the installed `codeconv/.venv`, and the existing skill-forwarding model. | **RECOMMENDED** |
| B. codeconv tool subpackage (auto-discovered) | Register under `tools/` so it appears in `tool_registry()`. | **Rejected** — tool subcommands trigger bridge + DBOS (cli.py), violating read-only/<3 s/no-deps; and a tutorial browser is not a code-conversion tool. |
| C. Standalone new Python package + console script | Fully separate from codeconv. | **Rejected for now** — cleanest identity separation but adds new packaging, a second venv/distribution story, and zero reuse, for no functional gain. |

**Recommendation: Option A.** Parsimonious and reuse-maximizing while staying
bridge-free. Identity concern (codeconv brands itself a "code-conversion runner")
is mitigated by keeping the module pure and documenting it as a read-only utility
that merely rides codeconv's CLI/test/venv infrastructure — the same way
`equiv/corpus.py` rides it.

**Command surface (leaves room for the companion `/glptutorial-run`):**
- `codeconv tutorials list [TUTORIAL]` — this feature.
- `codeconv tutorials run …` — reserved for the companion feature.
- `codeconv tutorials sync [--check]` — corpus vendoring/drift helper (D3).

The `/glptutorial-list` skill forwards verbatim to `codeconv tutorials list`.

---

## D2 — Vendored corpus location and contents (FR-007)

The clarification settled *that* we vendor a copy; planning settles *where* and
*what*.

| Option | Location | Verdict |
|---|---|---|
| **A. Top-level `tutorials/olamni/`** | `<repo>/tutorials/olamni/…` | **RECOMMENDED** — clear snapshot boundary, outside `programs/`, repo-root-relative resolution, easy to gitignore-or-commit as a unit. |
| B. Inside the package | `codeconv/src/codeconv/tutorials_corpus/` | Rejected — ships inside the wheel, bloats the package, conflates code with data. |
| C. Under `programs/` | `programs/tutorials/…` | **Rejected** — `programs/` is the single source of truth for *original* `.glp`; vendored copies there would violate that invariant (CLAUDE.md). |
| D. Under `specs/` | `specs/022-…/corpus/` | Rejected — specs are not a runtime data home. |

**Recommendation: Option A — `tutorials/olamni/`.** The engine resolves the
default corpus path as `<repo-root>/tutorials/olamni`, overridable with
`--corpus <path>` (tests point this at a fixture tree) and, optionally, an env
var.

**What to vendor:** the full `olamni/tutorial/` tree as-is (chapter dirs,
exercise dirs with their `.glp` + `ex-MM-tutorial.md` + `ex-MM-repl-trace.md`,
chapter `.md`s, and the top-level `tutorial.md`). Rationale: description sourcing
needs the `.md` guides and `.glp` headers; FR-008 empty-chapter behavior is
exercised by the planned chapters; and the companion `/glptutorial-run` will need
the golden traces from the same vendored copy. A faithful snapshot = a verbatim
copy.

---

## D3 — Corpus refresh / sync story + drift detection (the carried risk)

The checklist flagged that a vendored snapshot drifts from the authoritative
sibling corpus and "needs a refresh/sync story." Resolution:

- **Provenance manifest** checked in at `tutorials/olamni/SNAPSHOT.md` (+ machine
  `tutorials/olamni/.snapshot.json`) recording: the sibling source path, the
  sibling git commit/ref if available (else the copy date — note `Date.now()` is
  unavailable to workflow scripts but the sync tool runs in a normal Python
  process, so a real timestamp is fine there), and a sorted `{relpath: sha256}`
  map of every vendored file.
- **`codeconv tutorials sync`** re-vendors (copy sibling → `tutorials/olamni/`)
  and rewrites the manifest. It is the only path that reads the sibling repo, and
  it is **build-time only** — the runtime list path never touches the sibling
  (consistent with FR-007 + Dependencies).
- **`codeconv tutorials sync --check`** recomputes hashes of the vendored tree
  and compares to the manifest (detects local tampering); when the sibling repo
  is present it additionally diffs vendored-vs-sibling to report staleness. Exit
  non-zero on drift so it can gate CI later.

Scope note: `sync` is a **supporting capability**, not one of the three user
stories. The MVP user value (US1–US3) is the listing; `sync` exists so the
snapshot is reproducible and auditable rather than a mystery copy.

---

## D4 — Listing granularity: chapter → exercise → script

Reality (D-ground-truth #1) is three levels, but the spec's "script" is loosely
`exercise-MM`.

**Decision:** model three levels — **Tutorial (chapter `chNN`) → Exercise
(`exercise-MM`) → Script (`.glp` file)**. The listable/runnable unit is the
`.glp` **script**; the **exercise** is the description anchor.

Rendering rule:
- Group by chapter (FR-001). Under each chapter, list its exercises in order.
- Under each exercise, list its `.glp` script(s), each with a one-line
  description (FR-003).
- When an exercise has exactly one script, the renderer MAY collapse the exercise
  and script onto adjacent lines for scannability; when it has several (composed
  pipelines, corrected/failing pairs) each script gets its own line. Either way,
  every `.glp` script appears (SC-002 100% coverage).

This faithfully handles composed pipelines and corrected/failing pairs that a
flat "exercise = script" model would hide.

---

## D5 — Tutorial identifier matching (FR-002, SC-003)

Accept, case-insensitively:
1. Exact chapter id (`ch03`).
2. Zero-pad-normalized id (`ch3` → `ch03`, `3` → `ch03`).
3. Prefix on the id.
4. Substring match on the chapter human title (D6), e.g. `core` → ch03 "GLP Core".

Ambiguous match (more than one chapter) → report the candidates and ask the user
to disambiguate by id. No match → "no match" message + the full list of available
chapter ids (SC-003). Matching is read-only and non-interactive (FR-004 spirit).

---

## D6 — Chapter human title source

Precedence: (1) the "Chapter status" table in the top-level `tutorial.md`
(authoritative chNN → name map); (2) the H1 of `chNN_tutorial.md`; (3) fall back
to the bare id. Parsed once per run; cheap.

---

## D7 — Description extraction (FR-003, FR-004, US3)

Per-script description, in FR-004 precedence order (tutorial `.md` first, then
the script's own leading comment, else "no description"):

1. **Exercise `.md`** (`ex-MM-tutorial.md`): take the H1 title's descriptive tail
   (text after the `—`) and/or the first non-boilerplate paragraph; trim to one
   line. This is the primary source.
2. **Script `.glp` leading comment**: the first informative `%%`/`%` comment line
   that is not a pure filename banner (e.g. `%% producer/2 byte-exact from …`).
   Used to disambiguate multi-script exercises and when (1) is absent.
3. **Fallback**: explicit `(no description)` indicator (US3 #2) — never omit the
   script.

Descriptions are normalized to a single trimmed line (collapse whitespace, cap
length for terminal scannability).

---

## D8 — Output format and contract (FR-005, FR-006, FR-009)

- **Human-readable** indented listing to **stdout**: `chNN — <title>` headers,
  exercises and their scripts indented beneath, each script line `name —
  description`. Empty chapter → `(no scripts)`. (FR-005, FR-008)
- **Warnings** (non-standard dirs, FR-011) and **errors** (corpus unreachable,
  unknown identifier, FR-006) go to **stderr**; corpus-unreachable names the path
  it tried and exits non-zero without a partial listing.
- **`--json`** emits the structured model (chapters → exercises → scripts) for
  testing and to guarantee skill↔CLI equivalence (FR-009). The skill renders/relays
  the same content the CLI produces.
- Exit codes: `0` success (including "no scripts" chapters and a clean "no match"
  report is still a usage signal — use a distinct non-zero for no-match vs
  corpus-unreachable so callers can tell them apart). Final codes pinned in the
  contract.

---

## D9 — Testing approach

- pytest under `codeconv/tests/`, using a **fixture corpus** at
  `codeconv/tests/fixtures/tutorials_corpus/` that reproduces the real shapes:
  multi-script exercise, corrected/failing pair, empty (planned) chapter,
  non-standard dir, a script with no derivable description, and a duplicate
  exercise number across two chapters.
- The discovery/describe/render modules are pure → unit-testable with no bridge,
  no REPL, no DBOS. A `test_no_bridge_import_on_list_path` guard mirrors equiv's
  `test_no_lm_on_production_path` to lock the bridge-free invariant.
- SC-005 (<3 s) is trivially met by a filesystem walk; an opt-in perf assertion
  can bound it.

---

## Summary of decisions

| ID | Decision |
|---|---|
| D1 | Pure `codeconv tutorials` sub-app (bridge-free `add_typer`); module `codeconv/src/codeconv/tutorials/`; skill forwards to `codeconv tutorials list`. |
| D2 | Vendor full corpus at `tutorials/olamni/`; default path repo-root-relative, `--corpus` override. |
| D3 | `codeconv tutorials sync [--check]` re-vendors + writes/verifies `SNAPSHOT.md`/`.snapshot.json` provenance; build-time only. |
| D4 | Three-level model Chapter → Exercise → Script(.glp); script is the unit, exercise the description anchor. |
| D5 | Case-insensitive id / zero-pad / prefix / title-substring matching; ambiguous → candidates; none → list ids. |
| D6 | Chapter title from `tutorial.md` status table → `chNN_tutorial.md` H1 → bare id. |
| D7 | Description: exercise `.md` → `.glp` header → `(no description)`. |
| D8 | Indented stdout listing; warnings/errors to stderr; `--json`; distinct exit codes. |
| D9 | pytest with a shaped fixture corpus; bridge-free guard test; opt-in perf bound. |
