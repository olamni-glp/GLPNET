# Implementation Plan: /glptutorial-run — run & explain a single GLP tutorial example

**Branch**: `023-glptutorial-run` | **Date**: 2026-06-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/023-glptutorial-run/spec.md`

## Summary

Deliver a **run-&-explain** capability for individual GLP tutorial examples on two
equivalent surfaces — a Python engine (`codeconv tutorials {preview,run,explain,propose}`)
and a thin `/glptutorial-run` skill that forwards to it (FR-014). It **extends the
feature-022 `tutorials` sub-app** (replacing its reserved `run` stub) and reuses
022's corpus-discovery layer as the selection front-end (FR-001), staying **pure /
bridge-free** (reads files + shells out to a REPL; no PGLite/DBOS — research D1).

The core is **one unified run-model across both chapter shapes** (the spec's hard
requirement): a resolver turns a selected **exercise** (the uniform unit) into a
`RunnableExample`, detecting shape from 022's model — **section-driven** (ch01–ch06:
load one `.glp`) vs **use-case-driven** (ch07: load the sibling project
`programs/cssg_modules/` and run `fplayMM`). Goals and the expected outcome are
parsed from the exercise's `ex-MM-tutorial.md` / `ex-MM-repl-trace.md` (FR-004/005/008);
execution uses a **hybrid corpus model** — select from the vendored snapshot, execute
the sibling in place, guarded against drift (FR-012). Runs go through the **C# REPL
(the mandated default)** with Dart on demand; a non-working C# backend is a **critical
P1 defect** surfaced loudly (FR-007/018). The outcome is captured **outcome-only**
(bindings + `→ succeeds|suspended|failed`), compared to the golden, and explained with
reference to the `.md` (FR-009/010). The corpus is **read-only by default**; the tool
may emit restructuring **proposals**, applied only with explicit approval (FR-013/019).

## Technical Context

**Language/Version**: Python 3.11+ (codeconv `requires-python >=3.11`). Backends are
external processes: the C#/.NET GLP REPL at `out/csharp/` (default) and the sibling
Dart REPL (on demand). Skill is markdown.
**Primary Dependencies**: the feature-022 `codeconv.tutorials` layer (`corpus`,
`match`, `sync`); Typer (existing codeconv dep); stdlib `subprocess`, `pathlib`, `re`.
**No** `dbos` / `sqlalchemy` / `psycopg` / `codeconv.{bridge_client,runner,db}` on any
path (bridge-free, D1).
**Storage**: filesystem only — vendored snapshot `tutorials/olamni/` for selection;
sibling repo in place for execution; no DB.
**Testing**: pytest (`codeconv[dev]`) — fixture-driven resolver/parser units; extended
no-bridge guard; gated real-backend run; skill≡CLI parity (D11). The GLP REPL suite
(`test/run_all_tests.sh`) is untouched by this Python feature.
**Target Platform**: Windows (win32) primary + POSIX (cross-platform path handling).
**Project Type**: CLI tool (engine) + Claude skill (front-end), extending an existing
sub-app.
**Performance Goals**: preview/resolve are sub-second filesystem ops; a run is bounded
by the backend (a `--timeout` converts a non-terminating goal into a reported P1, not a
hang). C# `dotnet run` cold-start is the dominant cost.
**Constraints**: bridge-free / DBOS-free on every path; read-only over the corpus by
default (FR-013/015); C# is the mandated default backend (FR-007/018); outcome-only
capture (FR-008); skill ≡ CLI (FR-014); no LM on the production path (022 discipline).
**Scale/Scope**: ch01–ch13; implemented runnable examples ≈ ch01(3)+ch02(3)+ch03(3)+
ch04(10)+ch05(7)+ch06(5) section-driven + ch07(7) use-case plays ≈ 38; ch08–ch13 are
stubs reported "not yet available".

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is an **unratified template** (placeholder
principles only). With no ratified constitution, the effective gates are project
discipline from `CLAUDE.md` and `docs/DISCIPLINE.md`:

| Effective gate | Status |
|---|---|
| Spec-first; implementation matches spec exactly | PASS — every choice traces to a spec FR/SC; all 4 Clarifications resolved. |
| Single source of truth for `.glp` under `programs/` | PASS — reuses 022's vendored `tutorials/olamni/`; adds no `.glp` copies; execution targets the sibling in place. |
| Read-only by default; mutations gated | PASS — preview/run/explain/propose are read-only (FR-015); only approval-gated `propose --apply` mutates, targeting the sibling then re-vendoring (FR-019). |
| Bridge/DBOS not invoked needlessly | PASS — extends the bridge-free `tutorials` sub-app via direct `add_typer`; guarded by an extended no-bridge-import test (D1, D11). |
| No GLP language / runtime change | PASS — pure tooling; loads & runs existing programs; touches no guards/predicates/type system (CLAUDE.md §Language Authority). |
| `.glp` written by Gabi never modified without approval | PASS — corpus edits only via the approval-gated apply path; book-exact clause text preserved (FR-019). |
| Test baseline before/after; tests for new behaviour | PASS — codeconv pytest suite green before/after; new fixture units + guard + parity (D11); GLP REPL suite unaffected. |

**Post-Phase-1 re-check**: design introduces no new violations (subprocess-only
backend, no bridge import, no corpus mutation on the default path).

No violations → Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/023-glptutorial-run/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D11
├── data-model.md        # Phase 1 — RunnableExample/LoadTarget/Goal/Backend/Outcome/Verdict/Proposal
├── quickstart.md        # Phase 1 — usage walkthrough (both shapes)
├── contracts/
│   ├── tutorials_run_cli.md   # Phase 1 — CLI + skill interface, exit codes, JSON
│   └── repl_backend.md        # Phase 1 — backend invocation + outcome-only capture
└── tasks.md             # Phase 2 — /buildkit-tasks (NOT created by /buildkit-plan)
```

### Source Code (repository root)

```text
codeconv/src/codeconv/tutorials/        # EXTEND the feature-022 bridge-free sub-app
├── cli.py                              # MODIFIED: replace `run` stub; add preview/run/explain/propose
├── resolve.py                          # NEW: Exercise → RunnableExample (shape, load target, goals, golden) — D2–D5
├── backends.py                         # NEW: C# (default) + Dart REPL drivers; subprocess; P1 policy — D6
├── outcome.py                          # NEW: outcome-only parse (stdout + golden), fresh-var normalize — D7
├── explain.py                          # NEW: actual-vs-golden Verdict + guide-referenced explanation — D8
├── propose.py                          # NEW: read-only proposals + approval-gated apply — D9
├── corpus.py / match.py / sync.py …    # REUSED unchanged (selection front-end)

.claude/skills/glptutorial-run/
└── SKILL.md                            # NEW thin front-end → `codeconv tutorials <verb>` (FR-014)

codeconv/tests/
├── fixtures/tutorials_run/             # NEW shaped fixtures: section single/multi, use-case guide,
│                                       #   stub chapter, no-goal, multi-goal, superseded exercise, golden samples
├── test_tutorials_resolve.py           # shape + load-target + goal resolution (D2–D5; FR-002/003/004)
├── test_tutorials_outcome.py           # outcome + golden parse, fresh-var normalization (D7; FR-008)
├── test_tutorials_explain.py           # verdict match/difference/suspended/no-golden (D8; FR-009/010)
├── test_tutorials_backends.py          # fake-backend driving + C# P1 policy + gated real run (D6/D11)
├── test_tutorials_propose.py           # read-only report + gated apply guards (D9; FR-013/019)
├── test_tutorials_run_cli.py           # CLI verbs, exit codes 6–11, JSON (D10; FR-016)
├── test_tutorials_run_parity.py        # skill ≡ CLI equivalence (FR-014)
└── test_tutorials_no_bridge.py         # EXTENDED: cover resolve/backends/outcome/explain/propose (D1/D11)
```

**Structure Decision**: Extend the **existing bridge-free `tutorials` sub-app** rather
than create a new tool subpackage (which would acquire the bridge/DBOS via
`tool_registry`) or a standalone package (new packaging, duplicated corpus layer). New
behaviour is split into small single-purpose modules (`resolve`, `backends`, `outcome`,
`explain`, `propose`) so each maps cleanly to a research decision and a user story, and
the bridge-free invariant is preserved and re-guarded. Selection reuses 022's vendored
snapshot; execution targets the sibling repo in place (the two `--sibling-*` roots,
D4/D5). Full rationale: research D1–D11.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

## Phase 0 — Outline & Research

Complete. All decisions resolved in [research.md](./research.md): engine hosting /
bridge-free (D1), shape detection (D2), goal+load-target resolution from docs (D3),
hybrid corpus + two sibling roots (D4), ch07 canonical substrate + ex→fplay map (D5),
backend abstraction + C# P1 policy (D6), outcome-only capture + fresh-var normalization
(D7), explain/verdict (D8), restructuring proposals + gated apply (D9), CLI/skill
contract + exit codes (D10), testing strategy (D11). No `NEEDS CLARIFICATION` remain.

## Phase 1 — Design & Contracts

Complete: [data-model.md](./data-model.md) (RunnableExample → LoadTarget / Goal /
Backend / Outcome / Verdict / Proposal + resolution flow);
[contracts/tutorials_run_cli.md](./contracts/tutorials_run_cli.md) (CLI + skill surface,
exit codes 0–11, JSON model, FR-014 parity); [contracts/repl_backend.md](./contracts/repl_backend.md)
(stdin script grammar, stdout outcome grammar, golden parsing, C# P1 policy);
[quickstart.md](./quickstart.md). Agent context (`CLAUDE.md` BUILDKIT marker) updated
to point here.

## Phase 2 — Task planning (preview; produced by /buildkit-tasks)

Tasks will be organized by user story (US1 P1 + US2 P1 co-equal; US3 P2, US4 P2; US5
P3) plus a foundational slice and the proposal capability:

- **Foundational**: scaffold the new modules in `codeconv/src/codeconv/tutorials/`;
  replace the `run` stub wiring with the `preview/run/explain/propose` verbs (still
  bridge-free); extend `test_tutorials_no_bridge.py`; build the shaped fixture corpus;
  add the `--sibling-corpus` / `--sibling-glp-root` resolution + drift-guard call.
- **US1 (P1)**: `resolve.py` section-driven path + `backends.py` C# driver +
  `outcome.py` parse → `run ch01 01` reports the actual outcome (SC-001).
- **US2 (P1, the unification)**: `resolve.py` use-case path (ch07 → `programs/cssg_modules/`
  + `fplayMM`, D5) + project load via the same `run` command (SC-002/007); missing/failed
  module reporting (FR-017).
- **US3 (P2)**: `preview` (goals + expected outcome from `.md`, no execution; FR-005/SC-004).
- **US4 (P2)**: `explain.py` verdict + guide-referenced explanation; suspended-is-valid;
  difference always surfaced (FR-009/010/SC-005); golden parse + fresh-var normalize.
- **US5 (P3)**: `--backend cs|dart`; C# P1 surfacing + optional flagged Dart fallback
  (FR-007/018/SC-006).
- **Proposals**: `propose.py` read-only report + approval-gated `--apply` (FR-013/019).
- **Skill + parity**: `.claude/skills/glptutorial-run/SKILL.md`; FR-014 parity test.
- **Cross-cutting**: exit codes 6–11 + actionable messages (FR-016); JSON output;
  SC-003 golden-match check across implemented ch01–ch07 examples (gated on backend).
