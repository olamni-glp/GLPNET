# Implementation Plan: Marathon Stage Harness

**Branch**: `024-marathon-stage-harness` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/024-marathon-stage-harness/spec.md`

## Summary

A durable, restart-safe orchestration harness that drives one long multi-stage buildkit
feature (the first marathon: `multi-protocol-link-layer`) through the full pipeline
(specify → clarify → plan → task → analyze → implement → review) across many sessions,
partly in auto-mode, with escalation to Gabi at decision points.

**Primary requirement** (FR-001/002/003): survive session-end, context compaction, and
process crash — on restart, objectively locate the active stage + WIP position from
durable state (never from a conversation summary), resume from the last durable
checkpoint, skip completed work, and continue with zero re-instruction.

**Technical approach**: *Compose, don't reinvent.* The Claude Code dynamic **Workflow
tool** supplies the in-session orchestration the spec forbids re-implementing (fan-out,
per-agent JSONL journals, `resumeFromRunId` cached-prefix resume, `budget.spent()/
remaining()`). On top of it the harness adds only the three things Workflow lacks
(FR-010): (1) **cross-session durable checkpointing** on the existing
**DBOS-on-PGLite** substrate already wired in `codeconv` (bridge_client + db.engine +
durable), (2) the **per-stage engineer-approval gate**, and (3) a **JSON-store
fallback** for when the PGLite bridge is unavailable. Each *stage-block* maps 1:1 to a
single Workflow run, which is also the checkpoint boundary and the preauthorized
commit/push boundary (FR-019) — so resume granularity and git granularity never drift.

## Technical Context

**Language/Version**: Python 3.11+ (matches `codeconv`); orchestration via the Claude
Code dynamic Workflow tool (JavaScript scripts, composed — not a pip dependency).
**Primary Dependencies**: DBOS (durable workflows), SQLAlchemy + psycopg (PGLite wire
protocol), Alembic (migrations), Typer (CLI). **Reused as libraries** (no re-wiring):
`codeconv.bridge_client`, `codeconv.db.engine`, `codeconv.durable`. Node-side shared
bridge `prereq-patterns/pglite/pglite_bridge.mjs` (PGLite 0.4.5 / PG17).
**Storage**: **Primary** = PGLite cluster `C:/pglite/research/glpnet` (canonical shared
bridge) — new schema `marathon` for harness domain tables + `dbos` schema for DBOS
runtime (auto-created). **Fallback** = on-disk JSON store under the marathon state
directory, sequence-number-mirrored for reconciliation (FR-020/021).
**Testing**: pytest in `codeconv/.venv`; serial only (PGLite is single-writer WASM —
no `pytest-xdist`); `@needs_bridge` marker for bridge-touching tests;
`--test-concurrency=1` mandatory. Plus the GLP REPL suite (`bash test/run_all_tests.sh`)
as the repo baseline (untouched by this feature but run before/after per Test Protocol).
**Target Platform**: Windows 11 dev host; cross-platform Python.
**Project Type**: CLI tool + buildkit-stage skills (durable orchestration harness).
**Performance Goals**: Not throughput-bound. Resume = locate position + recover in
seconds (dominated by PGLite cold init ~7s on Windows). Status cadence target ~5 min
during active work (FR-013/SC-005).
**Constraints**: Single-writer PGLite (serial bridge access); DBOS workflow IDs MUST be
deterministic for replay-safe recovery (reuse `codeconv.durable` id-derivation);
cross-session resume MUST come from durable state, never a summary (FR-002); commits
stage only the block's files, never force-push, never bypass git hooks (FR-014/015);
Workflow `resumeFromRunId` is **same-session only** — cross-session resume is the
harness's own durable checkpoint (FR-009 assumption, US4-AS3).
**Scale/Scope**: One marathon (`multi-protocol-link-layer`), 7 stages, ~tens of blocks,
≥3 deliberate session boundaries (SC-009). Robust-but-minimal prototype; NOT
general-purpose (Scope boundary clarification).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Constitution file status**: `.specify/memory/constitution.md` is an **unfilled
template stub** — only placeholder tokens (`[PRINCIPLE_1_NAME]`, …), no ratified
principles, no version/ratification date. There are therefore **no project-specific
constitution gates** to evaluate against. Rather than vacuously pass, this plan applies
the **governing principles already binding in `CLAUDE.md`** as de-facto gates:

| De-facto gate (from CLAUDE.md) | Verdict | Evidence |
|---|---|---|
| **Spec-First** — no design without spec backing | ✅ PASS | Every design decision traces to an FR/SC/clarification (see research.md); no invented behavior. |
| **Compose, don't reinvent** (Assumptions; FR-009) | ✅ PASS | Workflow tool for orchestration; `codeconv` bridge/db/durable reused as libraries; harness adds only the 3 missing capabilities. |
| **Single source of truth** | ✅ PASS | Roadmap + buildkit pipeline state = position authority; `marathon` schema = durable position store; spec is the one authority for behavior. |
| **Mandated stack** (skill + Python + PGLite + DBOS + JSON) | ✅ PASS | Exactly this stack; no substitution. |
| **Scope discipline** — must not become its own marathon | ✅ PASS | IN = the 7 user stories sized to drive only `multi-protocol-link-layer`; trace substrate only, no optimizer (FR-017). |
| **Commit discipline** — stage by name, never force-push, never bypass hooks | ✅ PASS | FR-014/015 + gitblock design stages only block files; escalates on non-fast-forward. |
| **GEPA/codegen-opt runs Claude-only, no external LLM API** | ✅ PASS | Harness provides trace substrate only; the optimizer (out of scope) reuses codeconv's Claude-only GEPA. |

**Result**: No violations. Complexity Tracking table left empty (nothing to justify).
**Recommendation logged for Gabi**: ratify a real constitution later (out of scope for
this feature); not a blocker for this plan.

## Project Structure

### Documentation (this feature)

```text
specs/024-marathon-stage-harness/
├── plan.md              # This file (/buildkit-plan)
├── research.md          # Phase 0 — decisions + rationale (/buildkit-plan)
├── data-model.md        # Phase 1 — marathon schema entities (/buildkit-plan)
├── quickstart.md        # Phase 1 — start/interrupt/resume/verify walkthrough (/buildkit-plan)
├── contracts/           # Phase 1 — CLI + store + workflow-composition + hooks + escalation
│   ├── cli.md
│   ├── checkpoint-store.md
│   ├── workflow-composition.md
│   ├── buildkit-hooks.md
│   └── escalation.md
└── tasks.md             # Phase 2 (/buildkit-tasks — NOT created by /buildkit-plan)
```

### Source Code (repository root)

The harness lives **inside the `codeconv` package** as a dedicated, non-conversion
subpackage `codeconv.marathon`, so it reuses the already-wired DBOS-on-PGLite bridge,
engine, migration runner, and `durable` id-derivation **as libraries** without
re-implementing any of it (research.md D3). It is NOT placed under `codeconv/tools/`
(that registry is the conversion-pipeline tool list); its CLI is registered statically
in `cli.py`, mirroring the bridge-free `tutorials` command.

```text
codeconv/src/codeconv/marathon/
├── __init__.py            # `marathon` Typer app (start/resume/status/gate/rerun/trace/reconcile/doctor)
├── store.py               # Dual store: PGLite-primary + JSON-fallback; seq-numbered checkpoints; reconcile (FR-001/020/021)
├── checkpoint.py          # Checkpoint write/read + objective resume-locate (roadmap→pipeline→tasks) (FR-002/003)
├── gate.py                # Approval gate: present plan, record approve/change, append-only history (FR-004/005)
├── cadence.py             # Stage→block mapping (FR-019); block kinds + ordinals
├── orchestrate.py         # Composes the Workflow tool: 1 stage-block = 1 Workflow run; run-linkage; budget (FR-009/010/012)
├── verify_spike.py        # FR-011 first-task smoke test: resumeFromRunId cached-prefix + budget; records result
├── status.py              # Standardized ~5-min status report (done/issues/tokens/to-do) (FR-013)
├── gitblock.py            # Preauthorized commit+push per block; staged-by-block; escalate-on-block (FR-014/015)
├── trace.py               # Append-only verification-trace substrate (FR-016/017)
├── escalation.py          # Auto-mode policy: 2 block-points (gate + escalation); preauthorizations (FR-022/023)
└── models.py              # Dataclasses for marathon/stage_block/checkpoint/approval/status/trace/budget rows

codeconv/src/codeconv/db/migrations/versions/
└── 0010_marathon_schema.py   # CREATE SCHEMA marathon + harness tables (data-model.md)

codeconv/src/codeconv/cli.py   # +static registration of the `marathon` Typer app (like `tutorials`)

.claude/skills/marathon-stage-harness/   # buildkit-stage hook skill(s) (FR-018)
└── SKILL.md (+ helpers)        # wraps each stage as a marathon block; roots into CLAUDE.md memory chain

codeconv/tests/
├── test_marathon_store.py        # dual-store + reconciliation (US1, FR-020/021)
├── test_marathon_resume.py       # cross-session resume / skip-completed (US1, SC-001/002)
├── test_marathon_gate.py         # approval gate persistence + no-reask (US2, SC-004)
├── test_marathon_rerun.py        # per-stage / per-subagent re-run (US3, SC-003)
├── test_marathon_verify_spike.py # FR-011 cached-prefix + budget verification (US4, SC-008)
├── test_marathon_status.py       # status report fields + cadence (US5, SC-005)
├── test_marathon_budget.py       # ceiling halt/escalate (US5, SC-006)
├── test_marathon_gitblock.py     # commit-only-block-files + escalate-on-block-push (US6, SC-010)
└── test_marathon_trace.py        # append-only trace + refine-history order (US7)
```

**Structure Decision**: Single-project layout inside `codeconv` (Option 1 of the
template), chosen because the mandated stack (PGLite + DBOS + bridge + JSON) is already
fully wired only in `codeconv`; a standalone package would have to re-wire all of it,
violating *compose-don't-reinvent*. The home choice (`codeconv.marathon` vs standalone
package vs `tools/marathon`) is the one decision flagged for Gabi at the approval gate
(research.md D3) — the rest of the plan is home-agnostic in its module breakdown.

## Complexity Tracking

> No constitution violations to justify — table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | —          | —                                    |
