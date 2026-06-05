"""Stage → logical-block cadence mapping (FR-019, D9).

The cadence (verbatim FR-019 / the "logical block" clarification):

| stage(s)                         | block_kind          | count |
|----------------------------------|---------------------|-------|
| specify                          | ``specify``         | 1, then **restart** |
| clarify                          | ``clarify``         | 1 |
| plan + task + analyze            | ``plan_task_analyze``| 1 (the three collapse) |
| implement                        | ``implement_session``| N sessions, each 1 block |
| review                           | ``review``          | 1 |

Because plan/task/analyze collapse into one block, they canonicalize to a
single stage (``plan``) for the deterministic block id
``{marathon_id}:{canonical_stage}:{ordinal}`` (data-model.md). The
checkpoint boundary == the commit/push boundary == this block.
"""

from __future__ import annotations

from .models import STAGES

_STAGE_TO_BLOCK_KIND: dict[str, str] = {
    "specify": "specify",
    "clarify": "clarify",
    "plan": "plan_task_analyze",
    "task": "plan_task_analyze",
    "analyze": "plan_task_analyze",
    "implement": "implement_session",
    "review": "review",
}

# plan/task/analyze share one block, hence one canonical stage in the id.
_CANONICAL_STAGE: dict[str, str] = {"plan": "plan", "task": "plan", "analyze": "plan"}


def _require_stage(stage: str) -> None:
    if stage not in STAGES:
        raise ValueError(f"unknown stage {stage!r}; expected one of {STAGES}")


def canonical_stage(stage: str) -> str:
    """Map plan/task/analyze → ``plan`` (they are one block); else identity."""
    _require_stage(stage)
    return _CANONICAL_STAGE.get(stage, stage)


def block_kind_for_stage(stage: str) -> str:
    """The ``block_kind`` a stage maps to (data-model CHECK vocabulary)."""
    _require_stage(stage)
    return _STAGE_TO_BLOCK_KIND[stage]


def block_id(marathon_id: str, stage: str, ordinal: int) -> str:
    """Deterministic stage-block id ``{marathon_id}:{canonical_stage}:{ordinal}``.

    ``implement`` sessions increment ``ordinal``; every other stage uses
    ordinal ``1`` (single block)."""
    return f"{marathon_id}:{canonical_stage(stage)}:{ordinal}"


def is_implement_session(block_kind: str) -> bool:
    """Implement is the only stage that fans into a *series* of blocks (one per
    subagent session) — the rest are exactly one block (D9)."""
    return block_kind == "implement_session"


def restart_after_block(block_kind: str) -> bool:
    """specify's cadence is "1 block, then restart" (FR-019 / buildkit-hooks)."""
    return block_kind == "specify"


__all__ = [
    "block_id",
    "block_kind_for_stage",
    "canonical_stage",
    "is_implement_session",
    "restart_after_block",
]
