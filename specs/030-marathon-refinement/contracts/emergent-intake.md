# Contract — Emergent intake + mini-pipeline (US2)

## Capture (FR-005)
`capture_item(run, kind, title, description=None, blocks_stage=None)`:
- `kind ∈ {latent-requirement, issue, bug, missing-prerequisite}` — durably recorded as a first-class
  `item` row carrying its type.
- Creates the item's artifact dir `<store_root>/items/<id>/` (compact; **no** top-level `specs/NNN`
  directory and **no** shared project-pipeline row — FR-008, SC-003).

## Mini-pipeline expansion (FR-006, D4) — FIVE stages
On capture, append exactly these mini-`stage` rows (`origin='mini'`, `item_id=<id>`):
```
mini_specify → mini_clarify → mini_plan → mini_tasks → mini_analyze
```
There is **no** per-item `implement` (intentional divergence from the sibling's six — clarification Q3).

## Routing (FR-010)
- **Blocking** (`kind == missing-prerequisite` AND `blocks_stage` resolves to a not-yet-complete stage):
  compute fractional `order_key`s evenly in the gap **before** the blocked stage so the five mini-stages sort
  strictly ahead of it. Stacked blocking items each get a distinct fractional band ⇒ no collision.
- **Blocking but the target stage is already complete**: do **not** reorder finished work; raise/escalate
  `prereq_against_completed_stage` and surface clearly (edge case).
- **Non-blocking** (any other item): `order_key = max(order_key)+1` ⇒ mini-stages follow the current stage.

## Advisory / default-deny (FR-009)
- Capture and routing are the only automatic acts. The harness **never auto-advances** a mini-stage.
- The resume position names the item's next incomplete mini-stage as the next action; the driving agent
  (in Claude) advances it explicitly via `stage-start` + `checkpoint`.

## Feeding implement + done (FR-007, D4)
- When an item's `mini_analyze` stage is checkpointed complete, its planning artifacts in
  `<store_root>/items/<id>/` are available to the **marathon's single `implement` stage**, and the item's
  `status` flips to `done`.

## Durability parity (FR-011)
- Items and their mini-stages are ordinary rows: same checkpoint, scoped-commit boundary, reconciliation,
  keeper isolation, and resume guarantees as any other stage. Interruption mid-mini-pipeline resumes at the
  exact next incomplete mini-stage, never re-running a complete one (edge case).
- Concurrent captures are serialised by single-writer (FR-015); the resulting stage total reflects both
  deterministically (edge case).
