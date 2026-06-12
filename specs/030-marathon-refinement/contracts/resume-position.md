# Contract — Resume position (US1/US2 ordering)

`resume(run)` returns a `ResumePosition` derived **solely from durable rows** (run + stage + checkpoint +
open issue + approval/commit state). It is identical with full conversation context or after total context
loss (SC-008). The pure core `derive_position(run, stages, checkpoints, open_issues)` is unit-tested
bridge-free.

## Fields
```
ResumePosition(run_id, done, total, outstanding_issues, budget_spent, budget_unit, next_action, status)
```
- `done` = number of `stage` rows with `status == complete`.
- `total` = number of `stage` rows for the run — **current** count (grows on append/capture) (FR-003).
- `next_action` = the single exact next thing to do (string), per the ordering rules below.

## Ordering rules
1. Stages are considered in `order_key` ascending (ties broken by `stage_index`).
2. The next action is the **first non-complete stage** in that order, with these refinements:
   - A stage `complete` but with a checkpoint whose `commit_sha IS NULL` ⇒ next action is
     **"re-drive scoped commit for <stage>"** (crash-window guard, D9 / FR-018) — *before* any new work.
   - A stage `awaiting_approval` with `approval_state != approved` ⇒ next action is **"approve gate for
     <stage>"**; an already-`approved` gate is short-circuited (not re-asked) (FR-020).
   - A mini-stage (`item_id` set) ⇒ next action names the **item's next incomplete mini-stage**, e.g.
     "run mini-plan for item-7" (FR-009 advisory; the harness never auto-advances).
3. **Blocking missing-prerequisite** mini-stages have `order_key` strictly **less than** the blocked
   stage's `order_key` (placed there at capture, FR-010), so rule 2 surfaces them *before* the blocked
   stage automatically. Stacked blocking items against the same stage get distinct fractional `order_key`s
   ⇒ deterministic, collision-free ordering (edge case).
4. **Non-blocking** items' mini-stages have `order_key` after the current max ⇒ surface after the current
   stage (FR-010).
5. **Empty stage list** ⇒ `total == 0`, next action = **"register stages"** (not "finalise") (edge case).
6. All stages complete ⇒ next action = **"finalise run"**; a later append re-opens the run (edge case
   "append during finalisation").
7. **Store fork** (reconcile detects divergence) ⇒ resume exits with the escalation signal (CLI exit 2),
   never silently picking a store (FR-024).

## Determinism
No timestamps, no random, no conversation memory enter the computation — only durable row content and
`order_key`/`status`. This is the SC-008 guarantee.
