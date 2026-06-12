"""Emergent work capture + 5-stage mini-pipeline (US2, T021–T023).

``capture_item(run_id, *, kind, title, description, blocks_stage, env)``
records a typed ``item`` (``kind ∈ ITEM_KINDS``; the CLI's hyphenated spelling
is normalised), creates ``<store_root>/items/<id>/`` (FR-008 — compact, no
``specs/NNN`` dir), and expands it into exactly five mini-stages
``mini_specify → mini_clarify → mini_plan → mini_tasks → mini_analyze``
(``origin='mini'``, stage name ``item-<id>:<mini_kind>``; **no per-item
implement** — D4/FR-006): the planning artifacts feed the marathon's single
``implement`` stage, and the item flips ``done`` when its ``mini_analyze``
checkpoints (handled in ``checkpoint.py``).

Routing (FR-010, ``contracts/emergent-intake.md``):
- **Blocking** (``kind == missing_prerequisite`` AND ``blocks_stage`` resolves
  to a not-yet-complete stage): fractional ``order_key``s evenly spaced in the
  gap *before* the blocked stage — the gap floor is the highest existing key
  below it, so stacked blocking items land in distinct, deterministic bands.
- **Blocking but the target is already complete**: never reorder finished
  work — write a ``prereq_against_completed_stage`` escalation and raise
  :class:`PrereqAgainstCompletedStage` (CLI exit 2). No item row is created.
- **Non-blocking**: ``order_key = max+1..max+5`` — after the current stage.

Capture and routing are the only automatic acts; the harness never
auto-advances a mini-stage (FR-009, advisory / default-deny).
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

from codeconv.marathon.env import resolve_env
from codeconv.marathon.models import (
    MINI_KINDS,
    ITEM_KINDS,
    Escalation,
    Item,
    MarathonEnv,
    StageRow,
)
from codeconv.marathon.store import Repository

__all__ = ["PrereqAgainstCompletedStage", "capture_item"]


class PrereqAgainstCompletedStage(RuntimeError):
    """A blocking missing-prerequisite was captured against an already-complete
    stage (edge case): surfaced as an escalation, never a silent reorder."""

    def __init__(self, message: str, escalation_id: Optional[int] = None) -> None:
        super().__init__(message)
        self.escalation_id = escalation_id


def capture_item(
    run_id: str,
    *,
    kind: str,
    title: str,
    description: Optional[str] = None,
    blocks_stage: Optional[str] = None,
    env: Optional[MarathonEnv] = None,
) -> Item:
    """Capture a typed emergent-work item and expand its mini-pipeline."""
    env = env if env is not None else resolve_env(run_id)
    repo = Repository(env)

    kind_norm = kind.replace("-", "_")
    if kind_norm not in ITEM_KINDS:
        raise ValueError(
            f"unknown item kind '{kind}'; expected one of "
            f"{[k.replace('_', '-') for k in ITEM_KINDS]}"
        )
    if blocks_stage is not None and kind_norm != "missing_prerequisite":
        raise ValueError(
            "--blocks applies only to kind missing-prerequisite (FR-010); "
            f"got kind '{kind}'"
        )

    run = repo.get_run(run_id)
    if run is None:
        raise ValueError(f"unknown run '{run_id}'; register it first")
    if run.status == "finalized":
        # Edge case "capture during finalisation": re-open cleanly (T017).
        repo.update_run(run_id, status="in_progress")

    blocked = None
    if blocks_stage is not None:
        blocked = repo.get_stage(run_id, blocks_stage)
        if blocked is None:
            raise ValueError(
                f"unknown stage '{blocks_stage}' in run '{run_id}'"
            )
        if blocked.status == "complete":
            esc = repo.insert_escalation(
                Escalation(
                    run_id=run_id,
                    kind="prereq_against_completed_stage",
                    stage_id=blocked.id,
                    detail={
                        "blocks_stage": blocked.name,
                        "title": title,
                        "kind": kind_norm,
                    },
                )
            )
            raise PrereqAgainstCompletedStage(
                f"stage '{blocked.name}' is already complete; finished work is "
                f"not reordered — escalation {esc.id} written for Gabi",
                escalation_id=esc.id,
            )

    item = repo.insert_item(
        Item(
            run_id=run_id,
            kind=kind_norm,
            title=title,
            artifacts_dir="",  # defaulted to <store_root>/items/<id> in-txn
            description=description,
            blocks_stage_id=blocked.id if blocked is not None else None,
        )
    )
    Path(item.artifacts_dir).mkdir(parents=True, exist_ok=True)  # FR-008

    stages = repo.list_stages(run_id)
    n = len(MINI_KINDS)
    if blocked is not None:
        # Fractional band strictly before the blocked stage: floor = highest
        # existing key below it (a prior item's last mini-stage stacks the next
        # band after it — distinct, deterministic, collision-free).
        below = [s.order_key for s in stages if s.order_key < blocked.order_key]
        lo = max(below) if below else blocked.order_key - 1.0
        hi = blocked.order_key
        keys = [lo + (hi - lo) * i / (n + 1) for i in range(1, n + 1)]
    else:
        base = max((s.order_key for s in stages), default=0.0)
        keys = [base + i for i in range(1, n + 1)]

    next_index = max((s.stage_index for s in stages), default=0)
    for offset, (mini_kind, order_key) in enumerate(zip(MINI_KINDS, keys)):
        repo.insert_stage(
            StageRow(
                run_id=run_id,
                stage_index=next_index + offset + 1,
                order_key=order_key,
                name=f"item-{item.id}:{mini_kind}",
                origin="mini",
                item_id=item.id,
                mini_kind=mini_kind,
            )
        )
    return item
