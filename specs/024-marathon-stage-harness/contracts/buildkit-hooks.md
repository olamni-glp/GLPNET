# Contract: buildkit-Stage Hook Integration

The harness integrates as hooks into every buildkit pipeline stage and into the memory
chain rooted at CLAUDE.md (FR-018). A buildkit skill
(`.claude/skills/marathon-stage-harness/`) is the glue; the durable state lives in
`codeconv.marathon`.

## Stage → block cadence (FR-019 / D9)

| buildkit stage | Block mapping | Notes |
|---|---|---|
| `/buildkit-specify` | 1 block, then **restart** | restart after specify is part of the cadence |
| `/buildkit-clarify` | 1 block | |
| `/buildkit-plan` + `/buildkit-tasks` + `/buildkit-analyze` (incl. applied top remediations) | **1 block** | this command-chain is exactly this block |
| `/buildkit-implement` | a **series** of subagent sessions, each 1 block | fewest practical; per-subagent re-run (US3) |
| review | 1 block | |

## Per-stage hook protocol

For each stage the skill performs, in order:

1. **Locate** — `marathon resume` (objective position; never a summary).
2. **Gate** — if the block is mutating, `marathon gate` presents the plan and blocks for
   approval; on resume an existing approval short-circuits (FR-004/005/022).
3. **Run** — execute the stage as one Workflow run (workflow-composition.md), checkpointing
   throughout.
4. **Status** — emit the ~5-min standardized report during active work (FR-013).
5. **Checkpoint + commit/push** — final block checkpoint, then preauthorized commit/push
   staging only the block's files (FR-014/015).
6. **Boundary** — for specify, restart after the block (cadence).

## Memory-chain rooting (FR-018)

The harness is the owner named in CLAUDE.md's *Multi-Stage Task Persistence &
Restart-Resume* section. The resume order it implements MUST match that section verbatim:
**roadmap (`buildkit-roadmap next`) → buildkit pipeline state (DBOS+PGLite) → spec/plan/
tasks (WIP position)**. The `<!-- BUILDKIT START/END -->` block in CLAUDE.md points at the
active plan; the harness keeps the durable position, not a hand-written restart prompt.

## Auto-mode block-points (FR-022 / D11)

Within an approved block everything proceeds automatically. The harness blocks for Gabi at
exactly two kinds of point: (a) the per-stage plan-approval gate; (b) escalations
(non-retryable failure, store divergence, blocked push, stage-flagged decision). On either
while unattended → durably checkpoint and wait; never auto-approve.

## Standing preauthorizations (FR-023 / D10)

Two grants recorded at `marathon start`, both revocable: commit+push per block, and the
Workflow-tool opt-in. Neither relaxes the gate or any escalation.
