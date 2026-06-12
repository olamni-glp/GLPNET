# Implementation Plan: Marathon Refinement

**Branch**: `030-marathon-refinement` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/030-marathon-refinement/spec.md`

## Summary

Refine glpnet's marathon-stage-harness (feature 024, `codeconv.marathon`) into a **workload-agnostic**
durable harness by **adopting-and-reconciling** the sibling `crucible_marathon` package, then extending it.
The refinement has three structural moves and one extension:

1. **Stage vocabulary becomes data** (US1). Replace 024's hard-coded seven-stage tuple
   (`STAGES = (specify…review)`) and its canonical-collapse cadence with a registrable, ordered,
   *growable* list of named stages — adopted from the sibling's `wm_stage` model (`stage_index` +
   fractional `order_key` + `origin`). Progress is reported against the *current* total, which may grow.
2. **Per-run isolated store + keeper lifecycle** (US3). Marathon state moves out of the shared repo
   `.pgdb/` cluster into a **per-run isolated PGLite store outside the repo**, owned by a background
   *keeper*. glpnet's keeper is a thin lifecycle over the **existing `codeconv.bridge_client`** (spawn /
   discover / sidecar-endpoint / consumer-lock single-writer / non-destructive force-shutdown / stale-
   heartbeat recovery) pointed at a per-run `--data-dir` — reusing hardened infrastructure rather than
   re-porting the sibling's `PGliteSupervisor`.
3. **Reconcile, don't duplicate** (US4 + US5). 024's superior capabilities the sibling lacks — per-stage
   plan-approval gate, per-block/per-subagent re-run, budget-ceiling escalation, append-only verification-
   trace substrate, and dual-store reconciliation — are **ported onto** the new data-driven stage model
   and per-run store. The `gitblock` scoped-commit boundary and the parseable status line are reconciled
   with dynamic + mini stages rather than re-implemented.
4. **Extension: emergent work + mini-pipeline** (US2). Capture mid-run work as typed first-class items
   (latent-requirement / issue / bug / missing-prerequisite), each expanding into an in-marathon
   **five-stage** mini-pipeline (`mini-specify → mini-clarify → mini-plan → mini-tasks → mini-analyze`)
   whose output feeds the **marathon's single implement stage**. Blocking missing-prerequisites route
   *ahead of* the stage they block; routing is advisory / default-deny.

The model is **greenfield** (FR-029): the new harness does not read 024's shared-cluster `marathon`
schema; those rows are left inert. Verified no live 024 marathon is in flight.

## Technical Context

**Language/Version**: Python ≥ 3.11 (matches `codeconv/pyproject.toml`)
**Primary Dependencies**: SQLAlchemy ≥ 2.0, `psycopg[binary]` ≥ 3.1, Typer, PyYAML, `portalocker` ≥ 2.8,
DBOS; reuses in-repo `codeconv.bridge_client`, `codeconv.db.engine`, `codeconv.durable`.
**Storage**: **Per-run isolated PGLite cluster** (one per marathon run, *outside* the working repo, e.g.
under a user-level marathon root on an NTFS/ReFS path) reached via `codeconv.bridge_client` with a per-run
`data_dir`; **plus a per-run JSON mirror** inside the same store root (the dual-store fallback,
reconciliation rebased onto it per FR-027). Greenfield schema created by an ORM `ensure_schema` in the
per-run cluster — **not** the shared-repo Alembic chain.
**Testing**: pytest (in `codeconv/tests/`), `--test-concurrency=1` mandatory (PGLite cold-init ~7 s on
Windows); `@needs_bridge` gate for cluster-dependent tests; pure library functions tested bridge-free.
**Target Platform**: Windows 11 (dev), Linux/macOS (CI/sibling); Python CLI + importable library.
**Project Type**: Single project — a module within the `codeconv` Python toolchain (library + thin CLI).
**Performance Goals**: Not throughput-bound. Resume-position computation and status-line emission are
read-only derivations over durable rows; target sub-second on a typical run (≲ a few hundred stages).
**Constraints**: Resume position MUST be identical with full context or after total context loss
(depends solely on durable state, SC-008); zero budget overruns (SC-006 substrate-level CHECK); commit
boundaries stage only named paths — never blanket-add / force / history-rewrite / hook-bypass (SC-007).
**Scale/Scope**: Single active marathon per store (single-writer, FR-015); a run accumulates an
unbounded-but-modest number of stages (registered + dynamic + per-item mini-stages).

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.0.0. Re-checked after Phase 1 design.*

| Principle | Gate-ability | Status | Note |
|---|---|---|---|
| I. Spec-First | judgement | **PASS** | Plan derives entirely from the clarified spec; no code-as-truth. |
| II. Bug-Protocol / No-Workarounds | judgement | **PASS** | Stale-residue recovery and store-unavailable handling are *spec'd* lifecycle behaviours (FR-014/016), not try/except masking. The reconciliation never silently picks (FR-024). |
| III. SRSW inviolable | machine | **PASS / N/A** | No GLP clauses; no `skipSRSW` token anywhere in the artifacts. |
| IV-a. Language Authority | judgement | **PASS / N/A** | No GLP language change. |
| IV-b. Preserve Working Internals | judgement | **PASS** | Greenfield per-run store leaves 024's shared-cluster `marathon` rows inert (not deleted); no removal of load-bearing internals. |
| V. Claude-Only LM / No External API | machine | **PASS** | No LM in the loop here; no `OPENAI_API_KEY` / `litellm` / `openai` on any path. (The mini-pipeline is advisory/default-deny — the *driving agent* runs in Claude.) |
| VI-a. Additive, idempotent, single-head migration | machine | **PASS** | Shared-repo Alembic head stays `0010` — **no new shared migration**. Per-run schema is created by an idempotent ORM `ensure_schema` in the isolated cluster, outside Alembic's scope. |
| VI-b. Single OS-lock-guarded PGLite cluster per repo | judgement | **PASS (VI-b amended v1.1.0)** | Constitution **v1.1.0 (2026-06-11)** scopes VI-b to the repo's *working-data* cluster and explicitly exempts ephemeral per-run marathon stores outside the repo (reached via the same `codeconv.bridge_client`). FR-027's per-run isolated store is now compliant, not a deviation. Rationale retained in Complexity Tracking. |
| VII. Test-gated, commit-scoped shipping | advisory | **PASS** | gitblock stages named paths only (FR-017); ship via GitFlow. |
| VIII. Single source of truth & traceability | judgement | **PASS** | One authoritative spec (030); roadmap→pipeline→tasks lineage intact; 024 spec referenced, not duplicated. |

**Gate: PASS.** The prior VI-b tension was resolved by constitution amendment v1.1.0 (2026-06-11, owner-approved); no remaining deviation, no CRITICAL machine-checkable violation.

## Project Structure

### Documentation (this feature)

```text
specs/030-marathon-refinement/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions D1–D9 + reconciliation findings
├── data-model.md        # Phase 1 — per-run greenfield schema + entity map
├── quickstart.md        # Phase 1 — end-to-end drive of a refined marathon
├── contracts/           # Phase 1 — library/CLI/keeper/store/intake/commit/status contracts
│   ├── library-api.md
│   ├── cli.md
│   ├── keeper-lifecycle.md
│   ├── store-schema.sql
│   ├── resume-position.md
│   ├── emergent-intake.md
│   ├── checkpoint-commit.md
│   └── status-line.md
├── checklists/requirements.md   # (from /buildkit-specify)
└── tasks.md             # Phase 2 — /buildkit-tasks output
```

### Source Code (repository root)

The refined harness **rewrites the existing `codeconv.marathon` package in place** to the data-driven
model (same `codeconv marathon …` CLI command, same static registration in `codeconv/src/codeconv/cli.py`).
024's modules are reorganised, not preserved as dead code; 024's *durable rows* in the shared cluster are
what is left inert (greenfield, FR-029).

```text
codeconv/src/codeconv/marathon/
├── __init__.py          # Typer sub-app `marathon_app`; public library re-exports (parity surface)
├── models.py            # Dataclasses + vocabulary; STAGES tuple & stage CHECK REMOVED (data-driven)
├── env.py               # NEW — MarathonEnv (per-run store root, engine, repo dir) + resolve_env
├── keeper.py            # NEW — keeper lifecycle over codeconv.bridge_client (start/stop/recover/endpoint)
├── store/
│   ├── __init__.py
│   ├── schema.py        # NEW — ORM tables + idempotent ensure_schema (per-run cluster)
│   └── repository.py    # Single-writer data access; PGLite primary + JSON mirror + reconcile()
├── stages.py            # NEW — register_run / append_stage / fractional order_key / origin
├── checkpoint.py        # start_stage + checkpoint (status flip, budget delta, committed paths); resume()
├── position.py          # resume_position (four-field, derived solely from durable rows); mini/blocking order
├── intake.py            # NEW — capture_item + 5-stage mini-pipeline expansion (advisory/default-deny)
├── gate.py              # PORTED — per-stage approval gate over the new stage model
├── orchestrate.py       # PORTED — Budget ceiling/halt-escalate, rerun_block, rerun_subagent, preauth
├── gitblock.py          # PORTED — scoped commit + push + re-drive-on-resume, over checkpoint rows
├── trace.py             # PORTED — append-only verification-trace substrate (per-run store)
├── escalation.py        # PORTED — auto-decision policy + durable escalation writer
└── status.py            # status_line + emit_status (parseable grammar, current total)

codeconv/tests/
├── test_marathon_stages_*.py        # US1 — register/append/grow-total
├── test_marathon_intake_*.py        # US2 — capture + mini-pipeline + blocking/non-blocking order
├── test_marathon_keeper_*.py        # US3 — endpoint, graceful stop, stale-recover, single-writer
├── test_marathon_commit_status_*.py # US4 — scoped commit, re-drive, status line over new model
├── test_marathon_preserved_*.py     # US5 — gate / rerun / budget / trace / reconcile (regression)
└── test_marathon_resume_*.py        # resume-position determinism (SC-008), edge cases
```

**Structure Decision**: Single-project module inside `codeconv`. Rewrite `codeconv/src/codeconv/marathon/`
in place (preserving the `codeconv marathon` CLI command and its static registration). The per-run
isolated store, keeper, data-driven stages, and intake are **new** modules; gate/orchestrate/gitblock/
trace/escalation/status are **ported** from 024 onto the new model. No new shared-cluster migration; the
per-run schema is created by `store/schema.py::ensure_schema`. Standalone package extraction is explicitly
deferred (FR-028).

## Complexity Tracking

> The prior VI-b tension was **resolved** by constitution amendment v1.1.0 (2026-06-11). The rationale is
> retained below for traceability — it justifies why the amendment was the right resolution.

| Resolved item | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **VI-b** (resolved v1.1.0) — a per-run PGLite cluster *outside* `<repo>/.pgdb/` (a "second cluster") | FR-027 (clarified & owner-approved 2026-06-11) mandates a **per-run isolated store outside the working repository and unrelated project state**, owned by the keeper, so a crashed/abandoned run can never wedge the shared repo cluster and runs cannot interfere. The keeper / single-writer / stale-recovery lifecycle (US3) is meaningful only against a store the keeper exclusively owns. | **Keeping marathon state in the shared `<repo>/.pgdb/` cluster** (the 024 model) was rejected in clarification: it couples run lifecycle to the repo's working data, gives no per-run isolation, has no single-writer keeper, and a crashed session can wedge the shared cluster requiring manual cleanup — the exact failure US3 exists to eliminate. The deviation is *scoped*: the marathon store is ephemeral per-run orchestration state, not repo project data; VI-b's shared bridge at `<repo>/.pgdb/` is untouched and still single-per-repo. Reconciled with FR-028 by reaching the per-run cluster through the *same* `codeconv.bridge_client` infrastructure, not a parallel stack. |
