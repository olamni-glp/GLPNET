# Phase 0 Research: Marathon Stage Harness

**Feature**: 024-marathon-stage-harness | **Date**: 2026-06-05

All decisions below trace to the spec's FRs/SCs/clarifications/assumptions. There were
**no open `[NEEDS CLARIFICATION]` markers** in the spec (the `/buildkit-clarify` session
of 2026-06-05 resolved all five). The "unknowns" here are therefore *technical
realization* choices, not spec gaps.

---

## D1 — Durable substrate: DBOS-on-PGLite (primary) + JSON (fallback)

- **Decision**: Use the **DBOS-on-PGLite** substrate already wired in `codeconv` as the
  primary durable store, and a sequence-numbered **on-disk JSON store** as the fallback.
  Each stage-block runs as a DBOS workflow; DBOS's own completed-step durability gives
  cross-session recovery, and the harness layers its domain tables (checkpoints,
  approvals, status, trace, budget, run-linkage) in a new `marathon` schema.
- **Rationale**: Mandated stack (Assumptions: "skill + Python + PGLite + DBOS + a JSON
  backing store"). DBOS provides exactly the cross-session durable workflow checkpointing
  the Workflow tool lacks (FR-010). PGLite + bridge are proven and shared in this repo.
  JSON fallback satisfies FR-020 (resume with no loss when the primary is unavailable).
- **Alternatives rejected**:
  - *Pure-JSON only* — no replay-safe workflow durability; would re-implement what DBOS
    gives free; contradicts the mandated stack.
  - *SQLite/raw Postgres* — not the mandated stack; loses the shared bridge and
    `codeconv` reuse.

## D2 — Orchestration: compose the Claude Code dynamic Workflow tool

- **Decision**: Each stage-block is executed as **one** Workflow run. The harness never
  re-implements fan-out, per-agent JSONL journaling, in-session `resumeFromRunId`
  cached-prefix resume, or `budget.spent()/remaining()` — it composes them.
- **Rationale**: FR-009 + Assumptions ("compose, don't reinvent"). The Workflow tool's
  per-agent journals and run id are the in-session resume substrate; the harness records
  the run id as **run-linkage** so a same-session retry resumes the cached prefix.
- **Alternatives rejected**: hand-rolled subagent fan-out (reinvents orchestration,
  violates FR-009); a separate queue/worker system (out of mandated stack).

## D3 — Harness home: `codeconv.marathon` subpackage (reuse infra as libraries)  ⚑ FLAG FOR GABI

- **Decision (recommended)**: Place the harness as `codeconv/src/codeconv/marathon/`,
  a dedicated subpackage **outside** `codeconv/tools/`, importing `codeconv.bridge_client`,
  `codeconv.db.engine`, and `codeconv.durable` as libraries. Register its CLI statically
  in `cli.py` (like the bridge-free `tutorials` command), so it does **not** appear in
  the conversion `codeconv list` tool registry.
- **Rationale**: The mandated stack (PGLite + DBOS + bridge + migrations) is fully wired
  only in `codeconv`. Reusing it as libraries is the maximal *compose-don't-reinvent*
  move and gives the harness the migration runner, the PGLite compat patches, and the
  deterministic workflow-id derivation for free. Keeping it out of `tools/` avoids
  polluting the conversion-pipeline tool list (semantic mismatch: the harness is not a
  Dart→C# conversion tool).
- **Alternatives considered**:
  - *Standalone top-level package `marathon/`* — cleanest semantic separation, but must
    re-wire bridge discovery, DBOS launch, PGLite compat patches, and a second Alembic
    chain from scratch → contradicts compose-don't-reinvent; rejected.
  - *`codeconv/tools/marathon/` (auto-discovered)* — least code to register, but the
    harness would show up as a "conversion tool" in `codeconv list` and inherit the
    tool-registry framing it does not fit; rejected.
- **⚑ This is the single decision surfaced to Gabi at the plan-approval gate.** The rest
  of the plan's module breakdown is home-agnostic; only the import paths shift if Gabi
  prefers a standalone package.

## D4 — Cross-session vs in-session resume split

- **Decision**: In-session retry → Workflow `resumeFromRunId` (cached prefix).
  Across a session boundary, compaction, or crash → the harness's own durable checkpoint
  (DBOS workflow state + `marathon.checkpoints`), located via the established order:
  **roadmap (what feature+stage) → buildkit pipeline state (where in feature) →
  spec/plan/tasks (WIP unit)** — never a summary.
- **Rationale**: US4-AS3 + Assumptions: "Workflow resume is same-session only." FR-002
  mandates objective position location from durable state. Restart-resume order is the
  CLAUDE.md / memory protocol.
- **Alternatives rejected**: relying on Workflow resume across sessions (impossible —
  same-session only); reading position from the compaction summary (forbidden, FR-002,
  edge case "misleading summary").

## D5 — Store reconciliation: monotonic sequence numbers

- **Decision**: Every checkpoint carries a strictly monotonic `sequence_no`. On
  reconciliation, the store with the **strictly higher** sequence wins and fast-forwards
  the stale store. If both advanced past their last common checkpoint (a true fork, not a
  clean fast-forward), **stop and escalate to Gabi** — never silently pick. Primary
  (PGLite) is the default home; this rule governs only post-fallback reconciliation.
- **Rationale**: Verbatim FR-021 + the 2026-06-05 clarification. Edge case "Conflicting
  state between primary store and JSON fallback."
- **Alternatives rejected**: last-write-wins by timestamp (clock-skew unsafe, no fork
  detection); always-prefer-primary (would lose fallback work done during an outage,
  contradicting the clarification's "JSON fallback normally holds the newer work").

## D6 — Approval gate: append-only, superseded retained

- **Decision**: `marathon.approvals` is append-only. Presenting a plan records a row;
  approve/change is a terminal outcome on that row; a "change" creates a new row that
  supersedes (links back to) the prior one, which is retained. On resume, an existing
  approved row for the block short-circuits the gate (no re-ask).
- **Rationale**: FR-004/005, US2-AS2/AS3, SC-004 ("re-requested 0 times").
- **Alternatives rejected**: overwrite-in-place (loses decision history, violates AS3).

## D7 — Budget tracking + ceiling halt/escalate

- **Decision**: Persist a configurable `ceiling` plus running `spent`/`remaining` per
  marathon. During a run, read `budget.spent()/remaining()` from the Workflow tool;
  persist on each checkpoint. When spend reaches the ceiling, **end the in-flight unit at
  a safe checkpoint** then halt/escalate — never abandon a partial unit, never overrun.
- **Rationale**: FR-012, SC-006 (0 overruns), edge case "Budget ceiling reached
  mid-subagent → end at a safe checkpoint."
- **Alternatives rejected**: hard-kill at ceiling (leaves partial state, violates the
  edge case).

## D8 — Standardized periodic status (~5 min)

- **Decision**: A `status` report with exactly four fields — **done / issues / tokens
  (spent + remaining) / to-do** — emitted on a ~5-minute cadence during active work and
  persisted to `marathon.status_reports`. Pull tokens from the Workflow budget; pull
  done/remaining from checkpoints; issues from escalation/trace state.
- **Rationale**: FR-013, SC-005, US5-AS1/AS3.
- **Cadence mechanism**: the ~5-min cadence is **not** a separate daemon. During an active
  stage-block (a Workflow run) the harness emits a status report (a) at every durable
  checkpoint boundary and (b) via the Workflow run's periodic `log()`, whichever comes
  first within the ~5-min window. Between blocks (idle), no cadence is owed. This keeps the
  cadence inside the already-running orchestration (compose-don't-reinvent) and guarantees
  SC-005's "at least once per 5-minute interval" during active work without extra process
  machinery.
- **Alternatives rejected**: ad-hoc free-form logging (not standardized, not
  machine-readable, fails SC-005's "all four fields"); a standalone timer daemon (extra
  process lifecycle, not in the mandated stack, redundant with Workflow's own loop).

## D9 — Cadence mapping (stage → block)

- **Decision**: specify = 1 block (then restart); clarify = 1 block; **plan + task +
  analyze (including applied top remediations) = 1 block**; implement = a series of
  subagent sessions, each one block (fewest practical). Checkpoint and commit/push
  boundaries are identical to the Workflow-run boundary.
- **Rationale**: Verbatim FR-019 + the "logical block" clarification + Key Entity
  "Stage-block." (Note: this very command-chain — plan→tasks→analyze→safe-restart — is
  itself an instance of the plan+task+analyze block, manually executed to bootstrap.)
- **Alternatives rejected**: per-stage-always-1-block (implement would be one giant
  unresumable block — contradicts US3 per-subagent re-run).

## D10 — Preauthorizations: exactly two standing grants

- **Decision**: Two standing, marathon-scoped grants recorded durably at marathon start,
  both revocable by Gabi: (1) **commit + push per logical block** (stages only that
  block's files, never force-push, never bypass hooks); (2) the **Workflow-tool opt-in**.
  Neither relaxes the plan-approval gate (FR-004) or any escalation (FR-022).
- **Rationale**: FR-014/023 + Assumptions "Preauthorization scope" + the 2026-06-05
  opt-in clarification.
- **Alternatives rejected**: per-run Workflow prompts (defeats unattended auto-mode);
  broad "commit anything" grant (violates stage-only-block-files).

## D11 — Auto-mode policy: exactly two block-points

- **Decision**: In auto-mode the harness blocks for Gabi at **only** (a) each
  stage-block's plan-approval gate, and (b) escalations — non-retryable failure, store
  divergence (D5), blocked/non-fast-forward push, or a stage-flagged decision. Everything
  else within an approved block proceeds automatically. On reaching a gate/escalation
  while unattended, durably checkpoint and wait; never auto-approve.
- **Rationale**: Verbatim FR-022 + the auto-mode clarification + edge case "Auto-mode
  reaching a decision that requires Gabi."

## D12 — buildkit-stage hook integration

- **Decision**: A buildkit skill (`.claude/skills/marathon-stage-harness/`) hooks each
  pipeline stage (specify → clarify → plan → task → analyze → implement → review) so each
  stage runs as a marathon block, and roots into the memory chain at CLAUDE.md (the
  Restart-Resume protocol section). The skill is advisory/orchestration glue; the durable
  state lives in `codeconv.marathon`.
- **Rationale**: FR-018. Aligns with the existing CLAUDE.md "Multi-Stage Task Persistence
  & Restart-Resume" section, which already names this harness as the owner of the durable
  checkpoint + compaction/crash recovery.
- **Alternatives rejected**: a monolithic single skill with no per-stage hooks (loses the
  per-stage cadence + gate granularity).

## D13 — FR-011 verification spike as the FIRST implementation task

- **Decision**: The very first implementation unit (`verify_spike.py`) is a small
  multi-step Workflow run that (a) re-invokes with an unchanged prefix and asserts the
  unchanged prefix returns cached results while only the first changed/new step
  re-executes, and (b) confirms `spent`/`remaining` are observable throughout — recording
  the verification result durably.
- **Rationale**: FR-011 + US4 + SC-008. Gabi wants a *verified*, not assumed, restart
  method before the marathon relies on it. De-risks D2/D4 before building on them.
- **Alternatives rejected**: assuming Workflow behavior and building on top untested
  (explicitly rejected by US4's "verified, not assumed").

---

## Reuse map (what `codeconv` provides, used as-is)

| Need | Reused from codeconv | Reference |
|---|---|---|
| Spawn/discover PGLite bridge | `codeconv.bridge_client.acquire_or_discover` | `bridge_client.py` |
| Engine + DBOS launch + PGLite compat patches | `codeconv.db.engine.setup_dbos / build_url` | `db/engine.py:47-150` |
| Migration runner (Alembic upgrade head) | `codeconv migrate` flow | `cli.py:217-286` |
| Deterministic, replay-safe workflow ids | `codeconv.durable` id-derivation | `durable/__init__.py` |
| Two-phase `{stage}_started_at/_completed_at` durable-write idiom | tool precedent (convspec/plan/codegen) | `durable/steps.py` |
| Serial pytest + `@needs_bridge` harness | `codeconv/tests/conftest.py` | `tests/conftest.py` |

## Open items (none blocking)

- **Constitution stub**: `.specify/memory/constitution.md` is unfilled — recommend
  ratifying real principles in a later chore; not a blocker (see plan Constitution Check).
- **Home decision (D3)**: surfaced to Gabi at the approval gate before any code is
  written; does not block tasks generation (tasks are written against `codeconv.marathon`
  with a note that paths shift if Gabi chooses standalone).
