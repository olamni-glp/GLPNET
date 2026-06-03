"""Stage 4 — pure unit tests for the first-class ``no_emit`` status.

``no_emit`` is orthogonal to escalated/built (feature 020 Stage 4): a
source file intentionally NOT emitted to C# (e.g. a Dart ``export`` with
no types). It takes PRECEDENCE over escalation in classification, and a
``no_emit`` file is SATISFIED for readiness (never ready/pending; never
blocks its downstream). Pure — no bridge / no DB.
"""

from __future__ import annotations

from datetime import datetime, timezone

from codeconv.tools.codegen.readiness import (
    CODEGEN_DONE,
    CODEGEN_PENDING,
    CODEGEN_READY,
    CodegenRow,
    DepNode,
    classify_all,
)
from codeconv.tools.codegen.workflow import _classify_codegen_row


_NOW = datetime(2026, 6, 3, tzinfo=timezone.utc)


# ---------------------------------------------------------------------------
# _classify_codegen_row — precedence + each category
# ---------------------------------------------------------------------------


def test_no_emit_precedence_over_escalation() -> None:
    """no_emit wins even with open escalations (no_emit checked FIRST)."""
    assert (
        _classify_codegen_row(
            no_emit=True,
            open_escalation_count=5,
            completed_at=None,
            promoted=False,
        )
        == "no_emit"
    )


def test_no_emit_wins_over_completed_promoted() -> None:
    """no_emit beats a built+promoted row too."""
    assert (
        _classify_codegen_row(
            no_emit=True,
            open_escalation_count=0,
            completed_at=_NOW,
            promoted=True,
        )
        == "no_emit"
    )


def test_escalated_when_open_escalations() -> None:
    assert (
        _classify_codegen_row(
            no_emit=False,
            open_escalation_count=1,
            completed_at=_NOW,
            promoted=True,
        )
        == "escalated"
    )


def test_converted_when_completed_and_promoted() -> None:
    assert (
        _classify_codegen_row(
            no_emit=False,
            open_escalation_count=0,
            completed_at=_NOW,
            promoted=True,
        )
        == "converted"
    )


def test_built_when_completed_not_promoted() -> None:
    assert (
        _classify_codegen_row(
            no_emit=False,
            open_escalation_count=0,
            completed_at=_NOW,
            promoted=False,
        )
        == "built"
    )


def test_in_progress_when_no_completed() -> None:
    assert (
        _classify_codegen_row(
            no_emit=False,
            open_escalation_count=0,
            completed_at=None,
            promoted=False,
        )
        == "in_progress"
    )


# ---------------------------------------------------------------------------
# readiness — a no_emit file is SATISFIED (done) and unblocks downstream
# ---------------------------------------------------------------------------


def _nodes(spec):
    return {p: DepNode(topo_level=t, cycle_group_id=c) for p, (t, c) in spec.items()}


def test_no_emit_row_classified_done_not_in_progress() -> None:
    """A no_emit row (no codegen_completed_at) is DONE, not in_progress."""
    nodes = _nodes({"a.dart": (0, 1)})
    cross = {"a.dart": frozenset()}
    rows = {"a.dart": CodegenRow(completed=False, no_emit=True)}
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["a.dart"] == CODEGEN_DONE


def test_no_emit_dep_unblocks_downstream() -> None:
    """A downstream dep that is no_emit counts as satisfied for readiness."""
    nodes = _nodes({"dep.dart": (0, 1), "use.dart": (1, 2)})
    cross = {"dep.dart": frozenset(), "use.dart": frozenset({"dep.dart"})}

    # dep with no row at all ⇒ use pending.
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows={})
    assert states["use.dart"] == CODEGEN_PENDING

    # dep marked no_emit ⇒ use becomes ready (no_emit unblocks downstream).
    rows = {"dep.dart": CodegenRow(completed=False, no_emit=True)}
    states = classify_all(nodes=nodes, cross_scc_deps=cross, rows=rows)
    assert states["dep.dart"] == CODEGEN_DONE
    assert states["use.dart"] == CODEGEN_READY
