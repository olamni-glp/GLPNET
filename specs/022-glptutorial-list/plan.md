# Implementation Plan: /glptutorial-list — GLP tutorial browser

**Branch**: `022-glptutorial-list` | **Date**: 2026-06-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/022-glptutorial-list/spec.md`

## Summary

Deliver a **read-only GLP tutorial browser** on two equivalent surfaces: a Python
engine (`codeconv tutorials list [TUTORIAL]`) and a thin `/glptutorial-list`
skill that forwards to it (FR-009). With no argument it lists every tutorial
grouped by chapter → exercise → `.glp` script, each with a one-line description;
with a chapter identifier it lists only that chapter (FR-001, FR-002). The engine
is a **pure, bridge-free** sub-app on the existing codeconv CLI (research D1) —
mirroring `equiv/corpus.py` — so it never spins up the PGLite bridge or DBOS and
meets the <3 s / no-external-deps bar (SC-005, FR-010). It reads a **vendored
copy** of the corpus at `tutorials/olamni/` (FR-007), kept reproducible by a
build-time `codeconv tutorials sync` helper with a checked-in provenance manifest
(research D3).

## Technical Context

**Language/Version**: Python 3.11+ (codeconv `requires-python >=3.11`)
**Primary Dependencies**: Typer + PyYAML (both already codeconv deps); stdlib
`pathlib`, `re`. **No** `dbos` / `sqlalchemy` / `psycopg` on the list path.
**Storage**: filesystem only — vendored corpus under `tutorials/olamni/`; no DB.
**Testing**: pytest (`codeconv[dev]`); pure unit tests against a fixture corpus.
**Target Platform**: Windows + POSIX (cross-platform path handling).
**Project Type**: CLI tool (engine) + Claude skill (front-end).
**Performance Goals**: full catalog listing < 3 s (SC-005) — trivial for a
filesystem walk with no bridge cold-init.
**Constraints**: read-only (FR-010); bridge-free / DBOS-free on the list path; no
runtime dependency on the sibling GLP repo (FR-007); descriptions derived
mechanically, no LM (D7).
**Scale/Scope**: ~13 chapters, ~dozens of exercises, ~50–100 `.glp` scripts.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is an **unratified template** (placeholder
principles only). With no ratified constitution, the effective gates are project
discipline from `CLAUDE.md` and `docs/DISCIPLINE.md`:

| Effective gate | Status |
|---|---|
| Spec-first; implementation matches spec exactly | PASS — plan traces every choice to a spec FR/SC. |
| Single source of truth for `.glp` under `programs/` | PASS — vendored copies go to `tutorials/olamni/`, **not** `programs/` (research D2). |
| Read-only, no side effects (this feature lists, never runs) | PASS — FR-010 enforced; reserved `run` is out of scope. |
| Bridge/DBOS not invoked needlessly | PASS — pure `add_typer` built-in, guarded by a no-bridge-import test (D9). |
| Test baseline before/after; tests added for new behavior | PASS — pytest suite + fixture corpus (D9); does not touch the GLP REPL suite. |
| No language/runtime changes | PASS — pure tooling; no GLP language surface touched. |

No violations → Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/022-glptutorial-list/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D9
├── data-model.md        # Phase 1 — Corpus/Tutorial/Exercise/Script
├── quickstart.md        # Phase 1 — usage
├── contracts/
│   └── tutorials_cli.md # Phase 1 — CLI + skill interface contract
├── checklists/
│   └── requirements.md  # spec quality checklist (pre-existing)
└── tasks.md             # Phase 2 — /buildkit-tasks (NOT created here)
```

### Source Code (repository root)

```text
codeconv/src/codeconv/
├── cli.py                       # MODIFIED: app.add_typer(tutorials_app, name="tutorials")  [bridge-free]
└── tutorials/                   # NEW pure package (no bridge, no DBOS — mirrors equiv/corpus.py)
    ├── __init__.py
    ├── corpus.py                # walk vendored tree → Corpus/Tutorial/Exercise/Script (D4, D6)
    ├── describe.py              # description extraction precedence (D7)
    ├── match.py                 # identifier matching (D5)
    ├── render.py                # human-readable + JSON rendering (D8)
    ├── sync.py                  # build-time vendoring + provenance manifest (D3)
    └── cli.py                   # Typer sub-app: `tutorials list` (+ reserved run, sync)

tutorials/                       # NEW vendored snapshot (FR-007, D2)
└── olamni/                      # verbatim copy of sibling .../olamni/tutorial/
    ├── SNAPSHOT.md              # human provenance (source path, ref/date)
    ├── .snapshot.json           # machine provenance: {relpath: sha256}
    └── ch01/ … ch13/, tutorial.md, …

.claude/skills/glptutorial-list/
└── SKILL.md                     # NEW thin front-end → `codeconv tutorials list`

codeconv/tests/
├── fixtures/tutorials_corpus/   # NEW shaped fixture (multi-script ex, corrected/failing,
│                                #   empty chapter, non-standard dir, no-description, dup MM)
├── test_tutorials_corpus.py     # discovery + edge cases (FR-008, FR-011, dup MM)
├── test_tutorials_describe.py   # description precedence (D7 / FR-004)
├── test_tutorials_match.py      # identifier matching (D5 / SC-003)
├── test_tutorials_render.py     # human + JSON output, coverage (SC-002), exit codes
└── test_tutorials_no_bridge.py  # guard: list path imports no bridge/DBOS (D1)
```

**Structure Decision**: Host the engine as a **pure sub-app inside the existing
codeconv package** (`codeconv/src/codeconv/tutorials/`), wired into the codeconv
Typer CLI with a direct `add_typer` (bridge-free, like `codeconv list`), rather
than as an auto-discovered tool subpackage (which would trigger the bridge +
DBOS) or a brand-new standalone package (new packaging, no reuse). This maximizes
reuse of Typer, pytest, and the installed `codeconv/.venv` while preserving the
read-only / <3 s / no-external-deps contract. The vendored corpus lives at a
dedicated top-level `tutorials/olamni/`, deliberately outside `programs/` to honor
the single-source-of-truth rule for original `.glp`. Full rationale: research D1,
D2.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 — Outline & Research

Complete. All deferred decisions resolved in [research.md](./research.md):
engine hosting/invocation (D1), vendored location + contents (D2), sync/drift
story (D3), listing granularity (D4), identifier matching (D5), title source
(D6), description extraction (D7), output/exit-code contract (D8), testing (D9).
No `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

Complete: [data-model.md](./data-model.md) (Corpus → Tutorial → Exercise →
Script; resolution + matching rules), [contracts/tutorials_cli.md](./contracts/tutorials_cli.md)
(CLI + skill interface, JSON schema, exit codes, FR-009 equivalence),
[quickstart.md](./quickstart.md). Agent context (`CLAUDE.md` BUILDKIT marker)
updated to point here.

## Phase 2 — Task planning (preview; produced by /buildkit-tasks)

Tasks will be organized by user story (US1 P1, US2 P2, US3 P3) plus a foundational
slice and the supporting sync helper:

- **Foundational**: vendor the corpus (`tutorials/olamni/` + `SNAPSHOT.md`);
  scaffold `codeconv/src/codeconv/tutorials/` package and wire the bridge-free
  `add_typer` in `cli.py`; build the fixture corpus.
- **US1 (P1, MVP)**: `corpus.py` discovery (D4, D6) + `render.py` full-catalog
  human output + `--json`; empty-chapter indicator (FR-008); non-standard-dir
  warning (FR-011); no-bridge guard test.
- **US2 (P2)**: `match.py` identifier matching (D5) + single-chapter listing +
  no-match / ambiguous reporting and exit codes (SC-003).
- **US3 (P3)**: `describe.py` description precedence (D7) + no-description
  indicator; coverage assertions (SC-004).
- **Skill + parity**: `.claude/skills/glptutorial-list/SKILL.md`; FR-009
  equivalence test.
- **Supporting**: `sync.py` (`codeconv tutorials sync [--check]`) + provenance
  manifest (D3); opt-in <3 s perf bound (SC-005).
