# Contract — Checkpoint commit boundary (US4)

Reuses the project's existing scoped-commit mechanism (the 024 `gitblock` discipline), reconciled with the
data-driven + mini stage model — not duplicated (FR-017).

## Scoped commit (FR-017, SC-007)
Every completed block (ordinary, dynamically-appended, or mini) is a commit boundary:
- Stage **only** the block's explicitly-named `committed_paths` (`git add -- <paths>`). **Never** `git add -A`
  / `git add .`, never `--force`/`--force-with-lease`, never history rewrite, never `--no-verify`.
- Requires the run's `preauth_commit_push` standing grant (and not revoked).
- Record `commit_sha` + `committed_paths` on the **checkpoint** row (commit folded onto the checkpoint, D9).
- Push (if enabled) never forces; a non-fast-forward rejection writes a `push_blocked` escalation and sets
  `push_escalation` — it does not retry with force (FR-017).

## Durable write order + re-drive (FR-018, crash-window guard)
1. Append the checkpoint with `remaining_units=[]` (block durably complete) and `commit_sha=NULL`.
2. Attempt the scoped commit; on success set `commit_sha`.
3. If the process dies between (1) and (2): on resume the position detects a complete checkpoint with
   `commit_sha IS NULL` and re-drives **only** that one scoped commit before any new work begins.

## Status cadence (FR-019)
At every stage boundary, and on demand, `emit_status` writes a `status_report` and prints the parseable
status line ([`status-line.md`](./status-line.md)) — done/total against the **current** total, open issues,
budget spent, single next action.
