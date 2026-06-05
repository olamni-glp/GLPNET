"""Row + operational dataclasses for the marathon harness (T003).

One dataclass per ``marathon.*`` table (data-model.md) plus the two
operational result types the dual store returns (:class:`Position`,
:class:`ReconcileResult`). Pure — importable with no bridge/DBOS so the
unit tests that exercise id-derivation and JSON-mirror shapes need
neither.

The ``jsonb`` columns map to plain Python ``list``/``dict``; timestamptz
columns to :class:`datetime.datetime` (or ``None`` while a two-phase row
is half-written). Field order mirrors the data-model tables.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Optional

# --- block_kind / status / outcome / escalation-kind vocabularies -----------
# Centralised so the store, cadence, gate, and escalation modules agree on the
# exact strings the schema CHECK-constraints accept (data-model.md).

STAGES: tuple[str, ...] = (
    "specify",
    "clarify",
    "plan",
    "task",
    "analyze",
    "implement",
    "review",
)

BLOCK_KINDS: tuple[str, ...] = (
    "specify",
    "clarify",
    "plan_task_analyze",
    "implement_session",
    "review",
)

BLOCK_STATUSES: tuple[str, ...] = (
    "pending",
    "awaiting_approval",
    "running",
    "done",
    "failed",
    "escalated",
)

APPROVAL_OUTCOMES: tuple[str, ...] = ("approve", "change")

STORE_ORIGINS: tuple[str, ...] = ("primary", "fallback")

ESCALATION_KINDS: tuple[str, ...] = (
    "non_retryable_failure",
    "store_divergence",
    "push_blocked",
    "stage_flagged",
)

TRACE_DECISIONS: tuple[str, ...] = ("accept", "reject")


@dataclass
class Marathon:
    """``marathon.marathons`` — the long multi-stage feature being driven."""

    id: str
    feature_slug: str
    feature_branch: str
    budget_ceiling: Optional[int] = None
    budget_spent: int = 0
    preauth_commit_push: bool = False
    preauth_workflow_optin: bool = False
    preauth_revoked_at: Optional[datetime] = None
    auto_mode: bool = False
    created_at: Optional[datetime] = None


@dataclass
class StageBlock:
    """``marathon.stage_blocks`` — one logical block == one Workflow run."""

    id: str
    marathon_id: str
    stage: str
    block_kind: str
    ordinal: int
    status: str = "pending"
    workflow_run_id: Optional[str] = None
    last_sequence_no: int = 0
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None


@dataclass
class Checkpoint:
    """``marathon.checkpoints`` — append-only durable position snapshot."""

    block_id: str
    marathon_id: str
    sequence_no: int
    stage: str
    completed_units: list[Any] = field(default_factory=list)
    remaining_units: list[Any] = field(default_factory=list)
    budget_spent: int = 0
    store_origin: str = "primary"
    wip_unit: Optional[str] = None
    workflow_run_id: Optional[str] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


@dataclass
class Approval:
    """``marathon.approvals`` — append-only engineer decision per block."""

    block_id: str
    presented_plan_ref: str
    outcome: Optional[str] = None
    decided_by: Optional[str] = None
    decided_at: Optional[datetime] = None
    supersedes_id: Optional[int] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


@dataclass
class StatusReport:
    """``marathon.status_reports`` — standardized four-field snapshot."""

    marathon_id: str
    done: list[Any] = field(default_factory=list)
    issues: list[Any] = field(default_factory=list)
    tokens_spent: int = 0
    tokens_remaining: Optional[int] = None
    todo: list[Any] = field(default_factory=list)
    block_id: Optional[str] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


@dataclass
class VerificationTrace:
    """``marathon.verification_traces`` — append-only iteration substrate."""

    marathon_id: str
    subject: str
    refine_seq: int
    experiment_input: dict[str, Any] = field(default_factory=dict)
    decision: str = "accept"
    metric_score: Optional[float] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


@dataclass
class GitBlock:
    """``marathon.git_blocks`` — per-block commit/push outcome."""

    block_id: str
    staged_files: list[str] = field(default_factory=list)
    commit_sha: Optional[str] = None
    pushed: bool = False
    escalation: Optional[str] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


@dataclass
class Escalation:
    """``marathon.escalations`` — durable auto-mode block-point for Gabi."""

    marathon_id: str
    kind: str
    detail: dict[str, Any] = field(default_factory=dict)
    block_id: Optional[str] = None
    resolved_at: Optional[datetime] = None
    id: Optional[int] = None
    created_at: Optional[datetime] = None


# --- operational result types (dual-store interface) ------------------------


@dataclass
class Position:
    """The objective resume position — the max(sequence_no) checkpoint
    across the reachable store(s) (contracts/checkpoint-store.md I3)."""

    marathon_id: str
    block_id: str
    stage: str
    sequence_no: int
    completed_units: list[Any] = field(default_factory=list)
    remaining_units: list[Any] = field(default_factory=list)
    wip_unit: Optional[str] = None
    workflow_run_id: Optional[str] = None
    budget_spent: int = 0
    store_origin: str = "primary"


@dataclass
class ReconcileResult:
    """Outcome of comparing the two stores by ``sequence_no`` (D5)."""

    status: str  # "in_sync" | "fast_forward" | "fork"
    primary_seq: int
    fallback_seq: int
    winner: Optional[str] = None  # "primary" | "fallback" when fast_forward
    escalation_id: Optional[int] = None  # set when status == "fork"


__all__ = [
    "APPROVAL_OUTCOMES",
    "Approval",
    "BLOCK_KINDS",
    "BLOCK_STATUSES",
    "Checkpoint",
    "ESCALATION_KINDS",
    "Escalation",
    "GitBlock",
    "Marathon",
    "Position",
    "ReconcileResult",
    "STAGES",
    "STORE_ORIGINS",
    "StageBlock",
    "StatusReport",
    "TRACE_DECISIONS",
    "VerificationTrace",
]
