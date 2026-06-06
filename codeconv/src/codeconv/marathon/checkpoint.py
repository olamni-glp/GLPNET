"""Objective resume-locate + skip-completed + boundary safety (FR-002/003).

**Objective position location (FR-002/D4, I8).** The CLAUDE.md Restart-Resume
order is: roadmap (`buildkit-roadmap next` → *what feature + stage*) → buildkit
pipeline state (*where in the feature*) → spec/plan/tasks (*the WIP unit*). The
harness encodes the *result* of that walk durably: the marathon row fixes the
feature, and the **max(sequence_no) checkpoint** fixes the exact WIP unit +
completed/remaining units. :func:`resume` therefore reads ONLY durable state —
**never** a conversation/compaction summary (I8).

**Skip-completed (FR-003/SC-002).** :func:`units_to_execute` removes already-
completed units exactly once, so a resumed run re-executes none of them and
recorded decisions stay in effect.

**Boundary safety (I9).** Because completed_units are append-only, a unit is
skipped *only* if a checkpoint recorded it complete. A boundary unit (the first
unit of the next block) has no checkpoint yet, so it is neither skipped nor
double-executed — it runs exactly once.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from typing import Any, Optional


@dataclass
class ResumeReport:
    """The objective resume picture (contracts/cli.md ``resume`` shape)."""

    found: bool
    diverged: bool = False
    escalation_id: Optional[int] = None
    stage: Optional[str] = None
    block_id: Optional[str] = None
    block_kind: Optional[str] = None
    wip_unit: Optional[str] = None
    completed_units: list[Any] = field(default_factory=list)
    remaining_units: list[Any] = field(default_factory=list)
    workflow_run_id: Optional[str] = None
    store_origin: Optional[str] = None
    approval_state: Optional[str] = None
    block_complete: bool = False
    commit_push_pending: bool = False
    sequence_no: int = 0

    def as_dict(self) -> dict[str, Any]:
        return {
            "found": self.found,
            "diverged": self.diverged,
            "escalation_id": self.escalation_id,
            "stage": self.stage,
            "block_id": self.block_id,
            "block_kind": self.block_kind,
            "wip_unit": self.wip_unit,
            "completed_units": self.completed_units,
            "remaining_units": self.remaining_units,
            "workflow_run_id": self.workflow_run_id,
            "store_origin": self.store_origin,
            "approval_state": self.approval_state,
            "block_complete": self.block_complete,
            "commit_push_pending": self.commit_push_pending,
            "sequence_no": self.sequence_no,
        }


def resume(store: Any, marathon_id: str) -> ResumeReport:
    """Locate the resume position objectively from durable state (FR-002).

    Order: reconcile the two stores first (D5) — a true fork stops and
    escalates (``diverged``, exit 2); else read the max(sequence_no)
    checkpoint and report stage / block / WIP / completed / remaining /
    approval state. Never reads a summary (I8)."""
    from .gate import approval_state

    rec = store.reconcile(marathon_id)
    if rec.status == "fork":
        return ResumeReport(
            found=True, diverged=True, escalation_id=rec.escalation_id
        )

    pos = store.read_position(marathon_id)
    if pos is None:
        return ResumeReport(found=False)  # cold start — nothing checkpointed

    block = store.read_block(pos.block_id)
    block_complete = len(pos.remaining_units) == 0
    # Crash-window guard (FR-014/SC-010): finalize_block writes the FINAL
    # checkpoint (remaining=[]), THEN commits, THEN pushes. A crash between the
    # checkpoint and the commit/push would leave block_complete=True with the
    # block's git checkpoint silently lost — the next session would skip the
    # commit. Cross-check the git_blocks record so the skill re-drives commit/
    # push when it did not durably land.
    commit_push_pending = False
    if block_complete:
        from .gitblock import read_git_block

        gb = read_git_block(store, pos.block_id)
        if gb is None:
            commit_push_pending = True  # no commit at all (crash before commit)
        elif not gb.get("pushed") and gb.get("escalation") is None:
            commit_push_pending = True  # committed but unpushed & not escalated
    return ResumeReport(
        found=True,
        stage=pos.stage,
        block_id=pos.block_id,
        block_kind=block.block_kind if block is not None else None,
        wip_unit=pos.wip_unit,
        completed_units=pos.completed_units,
        remaining_units=pos.remaining_units,
        workflow_run_id=pos.workflow_run_id,
        store_origin=pos.store_origin,
        approval_state=approval_state(store, pos.block_id),
        block_complete=block_complete,
        commit_push_pending=commit_push_pending,
        sequence_no=pos.sequence_no,
    )


def _unit_key(unit: Any) -> str:
    """Stable identity for a unit (skip-completed comparison), tolerant of
    scalars and structured (dict/list) units."""
    try:
        return json.dumps(unit, sort_keys=True, default=str)
    except TypeError:
        return str(unit)


def units_to_execute(
    all_units: list[Any], completed_units: list[Any]
) -> list[Any]:
    """The units still to run, in order, with completed ones removed exactly
    once (FR-003/SC-002). A not-yet-completed boundary unit is preserved, so
    it runs exactly once (I9)."""
    done = {_unit_key(u) for u in completed_units}
    return [u for u in all_units if _unit_key(u) not in done]


__all__ = ["ResumeReport", "resume", "units_to_execute"]
