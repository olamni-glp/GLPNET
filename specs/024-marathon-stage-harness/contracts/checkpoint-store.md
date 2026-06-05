# Contract: Dual Checkpoint Store

The store is the heart of restart-safe resume. It presents ONE logical interface backed
by two physical stores — **primary** (DBOS-on-PGLite, schema `marathon`) and **fallback**
(on-disk JSON) — reconciled by strictly-monotonic `sequence_no`.

## Interface (`codeconv.marathon.store`)

```text
write_checkpoint(block_id, *, stage, wip_unit, completed_units, remaining_units,
                 workflow_run_id, budget_spent) -> Checkpoint
    # Allocates the next marathon-wide sequence_no, appends to the active store,
    # and (when both reachable) mirrors to the other. Append-only; never overwrites.

read_position(marathon_id) -> Position | None
    # Returns the max(sequence_no) checkpoint across whichever store(s) are reachable.
    # The objective resume source (FR-002). None = no checkpoints yet (cold start).

reconcile(marathon_id) -> ReconcileResult
    # Compares the two stores' max sequence_no:
    #   equal            -> in sync, no-op
    #   one strictly >   -> fast-forward the lower to the higher (FR-021)
    #   both advanced past last common checkpoint -> FORK -> escalate (no silent pick)

active_store(marathon_id) -> "primary" | "fallback"
    # primary if the bridge is reachable; else fallback, and surfaces fallback mode.
```

## Invariants

| # | Invariant | FR / SC |
|---|---|---|
| I1 | `sequence_no` is **strictly monotonic across the whole marathon**, never reused. | FR-021 / D5 |
| I2 | Checkpoints are **append-only**; a resume never mutates a prior checkpoint. | FR-001/003 |
| I3 | `read_position` returns the checkpoint with the **maximum** `sequence_no`. | FR-002 / SC-001 |
| I4 | Completed units in the returned position are **never re-executed**. | FR-003 / SC-002 |
| I5 | When the primary is unreachable, writes go to fallback with **no loss** of resume capability, and `active_store` reports `fallback`. | FR-020 / SC-007 |
| I6 | Reconciliation: strictly-higher sequence is authoritative and fast-forwards the stale store. | FR-021 / SC-007 |
| I7 | A true fork (both advanced past the last common checkpoint) **stops and escalates** — never a silent pick. | FR-021 / D5 |
| I8 | Position is derived from durable state only — **never** from a conversation/compaction summary. | FR-002 / edge case |
| I9 | Interruption exactly at a block boundary neither double-executes nor skips the boundary unit. | edge case |

## Behavioral scenarios (test oracle)

1. **Cross-session resume** (SC-001): write N checkpoints, drop the process, restart →
   `read_position` returns the Nth; resume re-executes 0 completed units (SC-002).
2. **Fallback episode** (SC-007): make the bridge unreachable mid-run → writes land in
   JSON (`active_store == fallback`); restore the bridge → `reconcile` fast-forwards the
   primary to the JSON sequence; no resume loss.
3. **Fork** (I7): advance primary to seq K and JSON to seq K independently past a common
   seq K-1 → `reconcile` returns FORK → escalation row written, exit `2`.
4. **Boundary interruption** (I9): kill exactly between block B's last checkpoint and
   B+1's first → resume starts B+1's boundary unit exactly once.

DBOS provides the replay-safe step durability underneath `write_checkpoint`; the store
layer adds the cross-store sequence arbitration and the JSON fallback that DBOS alone does
not give (FR-010).
