# Contract: Auto-Mode Escalation & Preauthorization Policy

Encodes FR-022/FR-023/D10/D11. In auto-mode the harness is autonomous **inside an
approved block** and blocks for Gabi at exactly two kinds of point.

## The two block-points

### (a) Plan-approval gate (FR-004)
Before any mutating stage-block runs, the harness presents the block plan and waits for an
explicit `approve` / `change`. Recorded durably; honored on resume without re-ask
(FR-005 / SC-004). Never auto-approved.

### (b) Escalations (`marathon.escalations`)

| kind | Trigger | FR |
|---|---|---|
| `non_retryable_failure` | a failure that cannot be auto-retried (e.g. retry budget exhausted, deterministic error) | FR-022 |
| `store_divergence` | reconcile detects a true fork (not a clean fast-forward) | FR-021 |
| `push_blocked` | preauthorized push hits conflict / non-fast-forward | FR-015 |
| `stage_flagged` | a stage explicitly flags a decision as needing Gabi | FR-022 |

On any escalation while unattended: **durably checkpoint, write the escalation row, and
wait** (exit code `2`). Never auto-resolve.

## What proceeds automatically

Everything else within an already-approved block: subagent fan-out, retryable failures
(re-run from checkpoint), checkpoint writes, status emission, and — under the standing
grants — Workflow runs and per-block commit/push.

## Standing preauthorizations (the ONLY two)

1. **commit + push per logical block** — stages only that block's files, never
   force-pushes, never bypasses git hooks (FR-014/015).
2. **Workflow-tool opt-in** — granted once at marathon start, applied to every
   stage-block run without a per-run prompt (FR-023).

Both: recorded durably (`marathons.preauth_*`), granted at marathon start, revocable by
Gabi (`preauth_revoked_at`). Neither relaxes the plan-approval gate or any escalation.
There are no other standing grants.

## Decision table

| Situation | Auto-proceed? | Action |
|---|---|---|
| Subagent transient failure, retry budget remains | yes | re-run from checkpoint (FR-006/007) |
| Subagent failure, retry budget exhausted | no | escalate `non_retryable_failure` |
| Push fast-forwards cleanly | yes (grant #1) | commit+push block files |
| Push non-fast-forward / conflict | no | escalate `push_blocked` (never force) |
| Reconcile fast-forward | yes | fast-forward stale store |
| Reconcile true fork | no | escalate `store_divergence` |
| Mutating block, no approval yet | no | present gate, wait |
| Mutating block, approval on record | yes | proceed (no re-ask) |
| Budget ceiling reached | no | safe checkpoint, then halt/escalate (0 overrun) |
