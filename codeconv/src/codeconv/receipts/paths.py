"""Conventional, documented receipt + expected-set locations (FR-022, research D2).

A receipt is a sidecar file at a path derived from *area* + *run*, so that a
consumer enforcing FR-008 has a determinate place to look and "no receipt" is a
fact rather than a judgement. The per-run expected-check set (FR-023) lives
beside the run's receipts so a run's receipts are enumerable and reconcilable.

Layout::

    <root>/<area>/<run-id>/<check-id>.receipt.json     # one receipt (FR-022)
    <root>/<run-id>/expected.json                       # per-run expected set (FR-023)

Implements ``specs/078-verification-receipts/contracts/manifest-and-expected.md``
and ``.../data-model.md``.
"""

from __future__ import annotations

from pathlib import Path

# The checked-in per-repo adoption manifest — the sole authority for whether
# FR-008 binds (FR-019). One manifest per repo (research D3).
ADOPTION_MANIFEST = Path(".specify") / "receipts" / "adoption.json"

# Default receipts root for a repo's runs. Callers pass an explicit ``root`` in
# tests (a tmp dir); production uses this documented location.
DEFAULT_RECEIPTS_ROOT = Path(".specify") / "receipts" / "runs"


def receipt_path(root: str | Path, area: str, run_id: str, check_id: str) -> Path:
    """Canonical sidecar path for one check's receipt (FR-022)."""
    return Path(root) / area / run_id / f"{check_id}.receipt.json"


def expected_set_path(root: str | Path, run_id: str) -> Path:
    """Path of a run's declared expected-check set (FR-023)."""
    return Path(root) / run_id / "expected.json"


def run_receipts(root: str | Path, run_id: str) -> list[Path]:
    """Every receipt file produced under ``run_id`` (any area).

    Used to reconcile the per-run expected set against what actually ran
    (FR-013): an expected check with no file here is a *missing* check.
    """
    base = Path(root)
    if not base.exists():
        return []
    return sorted(base.glob(f"*/{run_id}/*.receipt.json"))
