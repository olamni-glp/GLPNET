"""Row + operational dataclasses for the refined marathon harness (T003).

Data-driven rewrite (feature 030): one dataclass per ``marathon.*`` table in the
**per-run isolated** store (``contracts/store-schema.sql``) plus the operational
result/value types the library returns (:class:`ResumePosition`,
:class:`ReconcileResult`, :class:`Endpoint`, :class:`MarathonEnv`).

The hard-coded ``STAGES`` tuple and the ``_STAGE_TO_BLOCK_KIND`` /
``_CANONICAL_STAGE`` canonical-collapse cadence maps of 024 are **gone** (FR-001,
D3): the stage vocabulary is now *data* — an ordered, growable ``stage`` table —
so there is no enumerated stage CHECK anywhere. The vocabulary tuples below are
only the closed value-domains the schema CHECK-constraints accept.

Pure module — importable with no bridge/DBOS so the position/approval/budget/
reconcile derivations can be unit-tested bridge-free. ``jsonb`` columns map to
plain ``list``/``dict``; ``timestamptz`` to :class:`datetime.datetime` (or ``None``
while a row is half-written). Field order mirrors the schema tables.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Optional

# --- closed value-domains (mirror the schema CHECK-constraints) --------------
# NOT a stage vocabulary: the stage *names* are arbitrary data (FR-001/D3). These
# are the fixed enums the per-run store's CHECKs accept.

RUN_STATUSES: tuple[str, ...] = ("in_progress", "finalized")

STAGE_ORIGINS: tuple[str, ...] = ("registered", "dynamic", "mini")

STAGE_STATUSES: tuple[str, ...] = (
    "pending",
    "awaiting_approval",
    "running",
    "complete",
    "failed",
    "escalated",
)

# The five mini-pipeline stages a captured item expands into — feeding the
# marathon's single ``implement`` stage; there is NO per-item implement (D4).
MINI_KINDS: tuple[str, ...] = (
    "mini_specify",
    "mini_clarify",
    "mini_plan",
    "mini_tasks",
    "mini_analyze",
)

ITEM_KINDS: tuple[str, ...] = (
    "latent_requirement",
    "issue",
    "bug",
    "missing_prerequisite",
)

ITEM_STATUSES: tuple[str, ...] = ("open", "done")

ISSUE_STATUSES: tuple[str, ...] = ("open", "resolved")

APPROVAL_OUTCOMES: tuple[str, ...] = ("approve", "change")

STORE_ORIGINS: tuple[str, ...] = ("primary", "fallback")

TRACE_DECISIONS: tuple[str, ...] = ("accept", "reject")

ESCALATION_KINDS: tuple[str, ...] = (
    "non_retryable_failure",
    "store_divergence",
    "push_blocked",
    "budget_exceeded",
    "stage_flagged",
    "prereq_against_completed_stage",
)


# --- table rows (one per marathon.* table) ----------------------------------


@dataclass
class MarathonRun:
    """``marathon.run`` — one workload-agnostic marathon driven across sessions."""

    id: str
    title: str = ""
    status: str = "in_progress"
    head_commit: Optional[str] = None
    budget_ceiling: Optional[int] = None
    budget_spent: int = 0
    budget_unit: Optional[str] = None
    preauth_commit_push: bool = False
    preauth_workflow_optin: bool = False
    preauth_revoked_at: Optional[datetime] = None
    auto_mode: bool = False
    created_at: Optional[datetime] = None


@dataclass
class StageRow:
    """``marathon.stage`` — one named stage; ordered by fractional ``order_key``.

    ``origin`` ∈ :data:`STAGE_ORIGINS`. A mini-stage carries BOTH ``item_id`` and
    ``mini_kind`` (an ordinary stage carries neither — the ``stage_mini_pair_ck``
    invariant). ``order_key`` is ``double precision`` so blocking-prerequisite
    mini-stages can be inserted *between* existing keys (FR-010)."""

    run_id: str
    stage_index: int
    order_key: float
    name: str
    origin: str
    status: str = "pending"
    item_id: Optional[int] = None
    mini_kind: Optional[str] = None
    started_at: Optional[datetime] = None
    completed_at: Optional[datetime] = None
    last_sequence_no: int = 0
    id: Optional[int] = None


@dataclass
class Item:
    """``marathon.item`` — captured emergent work (``kind`` ∈ :data:`ITEM_KINDS`).

    A ``missing_prerequisite`` may set ``blocks_stage_id`` (its mini-stages route
    ahead of that stage). ``artifacts_dir`` is ``<store_root>/items/<id>/``."""

    run_id: str
    kind: str
    title: str
    artifacts_dir: str
    description: Optional[str] = None
    blocks_stage_id: Optional[int] = None
    status: str = "open"
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class CheckpointRow:
    """``marathon.checkpoint`` — append-only durable position; commit folded in.

    Sole source of truth for resume. ``remaining_units == []`` flips the stage
    complete. ``commit_sha``/``committed_paths``/``pushed``/``push_escalation``
    fold the per-block commit boundary onto the checkpoint (D9)."""

    run_id: str
    stage_id: int
    sequence_no: int
    store_origin: str = "primary"
    wip_unit: Optional[str] = None
    completed_units: list[Any] = field(default_factory=list)
    remaining_units: list[Any] = field(default_factory=list)
    budget_spent: int = 0
    committed_paths: list[str] = field(default_factory=list)
    commit_sha: Optional[str] = None
    pushed: bool = False
    push_escalation: Optional[str] = None
    workflow_run_id: Optional[str] = None
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class Issue:
    """``marathon.issue`` — an open concern surfaced at a stage."""

    run_id: str
    summary: str
    stage_id: Optional[int] = None
    status: str = "open"
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class Approval:
    """``marathon.approval`` — append-only per-stage gate decision (supersedes
    chain). ``outcome`` ∈ :data:`APPROVAL_OUTCOMES` (or ``None`` = awaiting)."""

    stage_id: int
    presented_plan_ref: str
    outcome: Optional[str] = None
    decided_by: Optional[str] = None
    decided_at: Optional[datetime] = None
    supersedes_id: Optional[int] = None
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class VerificationTrace:
    """``marathon.verification_trace`` — append-only iteration substrate
    (UNIQUE ``(run_id, subject, refine_seq)``; never overwritten)."""

    run_id: str
    subject: str
    refine_seq: int
    decision: str = "accept"
    experiment_input: dict[str, Any] = field(default_factory=dict)
    metric_score: Optional[float] = None
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class Escalation:
    """``marathon.escalation`` — durable auto-mode block-point for Gabi
    (``kind`` ∈ :data:`ESCALATION_KINDS`)."""

    run_id: str
    kind: str
    stage_id: Optional[int] = None
    detail: dict[str, Any] = field(default_factory=dict)
    resolved_at: Optional[datetime] = None
    created_at: Optional[datetime] = None
    id: Optional[int] = None


@dataclass
class StatusReport:
    """``marathon.status_report`` — standardized four-field snapshot."""

    run_id: str
    stage_id: Optional[int] = None
    done: list[Any] = field(default_factory=list)
    issues: list[Any] = field(default_factory=list)
    tokens_spent: int = 0
    tokens_remaining: Optional[int] = None
    todo: list[Any] = field(default_factory=list)
    created_at: Optional[datetime] = None
    id: Optional[int] = None


# --- operational value/result types -----------------------------------------


@dataclass
class ResumePosition:
    """The objective resume position, derived solely from durable rows
    (``contracts/resume-position.md``; SC-008 determinism).

    ``done`` = #stages ``complete``; ``total`` = #stages for the run (current
    count, grows on append/capture); ``next_action`` = the single exact next
    thing to do. ``diverged`` signals a store fork (resume → CLI exit 2,
    never a silent pick — rule 7/FR-024)."""

    run_id: str
    done: int
    total: int
    next_action: str
    status: str = "in_progress"
    outstanding_issues: int = 0
    budget_spent: int = 0
    budget_unit: Optional[str] = None
    diverged: bool = False


@dataclass
class ReconcileResult:
    """Outcome of comparing the per-run PGLite store with its JSON mirror by
    ``sequence_no`` (D6). ``fork`` writes a ``store_divergence`` escalation and
    never silently picks (FR-024)."""

    status: str  # "in_sync" | "fast_forward" | "fork"
    primary_seq: int
    fallback_seq: int
    winner: Optional[str] = None  # "primary" | "fallback" when fast_forward
    escalation_id: Optional[int] = None  # set when status == "fork"


@dataclass
class Endpoint:
    """A reachable per-run bridge endpoint surfaced by the keeper (US3)."""

    host: str
    port: int
    pid: int
    data_dir: str


@dataclass
class MarathonEnv:
    """Per-run environment: the off-repo ``store_root`` (isolated PGLite cluster +
    JSON mirror), the lazily-acquired ``engine``, and the working ``repo_dir``
    where scoped commits land. Resolved by ``env.resolve_env`` (FR-027)."""

    run_id: str
    store_root: Path
    repo_dir: Path
    engine: Optional[Any] = None


__all__ = [
    # value-domains
    "APPROVAL_OUTCOMES",
    "ESCALATION_KINDS",
    "ISSUE_STATUSES",
    "ITEM_KINDS",
    "ITEM_STATUSES",
    "MINI_KINDS",
    "RUN_STATUSES",
    "STAGE_ORIGINS",
    "STAGE_STATUSES",
    "STORE_ORIGINS",
    "TRACE_DECISIONS",
    # table rows
    "Approval",
    "CheckpointRow",
    "Escalation",
    "Issue",
    "Item",
    "MarathonRun",
    "StageRow",
    "StatusReport",
    "VerificationTrace",
    # operational
    "Endpoint",
    "MarathonEnv",
    "ReconcileResult",
    "ResumePosition",
]
