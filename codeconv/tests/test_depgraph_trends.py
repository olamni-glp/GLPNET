"""Tests for ``codeconv depgraph trends`` (feature 062, US1 / T010).

Two layers:

* **Pure** (no bridge): :func:`codeconv.tools.depgraph.trends.compute_trends`
  is deterministic (input-order-independent, byte-identical on unchanged
  inputs), refuses <2 runs, and secret-redacts string fields.
* **End-to-end** (``@needs_bridge``): two recorded compute runs produce a
  byte-identical trend report on re-run; a single run is refused with exit 1.

Maps to ``specs/062-.../contracts/depgraph-cli.md`` § trends, spec FR-002.
"""

from __future__ import annotations

import json

import pytest

from codeconv.tools.depgraph.trends import (
    TrendError,
    _redact,
    compute_trends,
)

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree


def _run(rid: str, started_at: str, **metrics) -> dict:
    base = {
        "files_total": 0,
        "ready_count": 0,
        "in_progress_count": 0,
        "converted_count": 0,
        "cycle_count": 0,
    }
    base.update(metrics)
    return {"id": rid, "started_at": started_at, **base}


# ---------------------------------------------------------------------------
# Pure trend logic (bridge-free)
# ---------------------------------------------------------------------------


def test_compute_trends_refuses_single_run() -> None:
    with pytest.raises(TrendError, match="at least two runs required"):
        compute_trends([_run("r1", "2026-07-29T00:00:00Z")])


def test_compute_trends_computes_first_last_and_step_deltas() -> None:
    runs = [
        _run("r1", "2026-07-29T00:00:00Z", ready_count=1, converted_count=0),
        _run("r2", "2026-07-29T01:00:00Z", ready_count=3, converted_count=2),
        _run("r3", "2026-07-29T02:00:00Z", ready_count=2, converted_count=5),
    ]
    report = compute_trends(runs)
    assert report["run_count"] == 3
    rc = report["metric_deltas"]["ready_count"]
    assert rc["first"] == 1 and rc["last"] == 2 and rc["delta"] == 1
    assert rc["series"] == [1, 3, 2]
    assert rc["step_deltas"] == [2, -1]
    cc = report["metric_deltas"]["converted_count"]
    assert cc["delta"] == 5 and cc["step_deltas"] == [2, 3]


def test_compute_trends_is_input_order_independent() -> None:
    a = _run("r1", "2026-07-29T00:00:00Z", ready_count=1)
    b = _run("r2", "2026-07-29T01:00:00Z", ready_count=4)
    forward = json.dumps(compute_trends([a, b]), sort_keys=True)
    reversed_ = json.dumps(compute_trends([b, a]), sort_keys=True)
    assert forward == reversed_


def test_compute_trends_byte_identical_on_unchanged_inputs() -> None:
    runs = [
        _run("r1", "2026-07-29T00:00:00Z", ready_count=1),
        _run("r2", "2026-07-29T01:00:00Z", ready_count=4),
    ]
    first = json.dumps(compute_trends(runs), sort_keys=True, indent=2)
    second = json.dumps(compute_trends(runs), sort_keys=True, indent=2)
    assert first == second


def test_redact_masks_secret_like_tokens() -> None:
    assert _redact("token=abcDEF1234567890secretvalue") == "[REDACTED]"
    # A run-id-shaped uuid is not a secret; short plain strings pass through.
    assert _redact("2026-07-29T00:00:00Z") == "2026-07-29T00:00:00Z"
    assert _redact(42) == 42


# ---------------------------------------------------------------------------
# End-to-end (bridge-gated)
# ---------------------------------------------------------------------------


@needs_bridge
def test_trends_two_runs_byte_identical_on_rerun(discover_repo) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0

    p1 = run_codeconv(discover_repo, "depgraph", "trends")
    assert p1.returncode == 0, p1.stderr
    p2 = run_codeconv(discover_repo, "depgraph", "trends")
    assert p2.returncode == 0, p2.stderr
    # The report body is the deterministic deliverable — byte-identical.
    assert p1.stdout == p2.stdout
    report = json.loads(p1.stdout)
    assert report["run_count"] == 2
    assert "ready_count" in report["metric_deltas"]


@needs_bridge
def test_trends_single_run_refused_exit_1(discover_repo) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    proc = run_codeconv(discover_repo, "depgraph", "trends", "--json")
    assert proc.returncode == 1, (
        f"single run must exit 1; got {proc.returncode} "
        f"{proc.stdout}{proc.stderr}"
    )
    assert "two runs" in (proc.stdout + proc.stderr).lower()
