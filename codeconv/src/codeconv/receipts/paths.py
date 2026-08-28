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


class UnsafeReceiptPath(ValueError):
    """A path component would escape the receipts root (traversal or absolute)."""


def _safe_component(value: str, *, field: str) -> str:
    """One path segment that CANNOT escape the root.

    ``area``, ``run_id`` and ``check_id`` reach here from receipt content and
    from callers; a value like ``../..`` or ``C:/x`` would place a "receipt"
    anywhere on disk and let a consumer be pointed at a file the run never
    wrote. A receipt whose location is attacker-chosen is not evidence.
    """
    # COERCION IS THE HOLE, not a convenience. str(0) == "0" would write the
    # receipt under a "0" directory while the receipt itself kept the int 0 —
    # and Verdict(run_id=0) is not None, so it passes the run binding and then
    # matches any other run that also used 0. Reusing it recreates exactly the
    # prior-run PASS reuse this module exists to stop. Demand a real string.
    if not isinstance(value, str):
        raise UnsafeReceiptPath(
            f"{field} must be a string, got {type(value).__name__} {value!r} — "
            f"coercing it would let a non-string identity be reused across runs (FR-002/022)"
        )
    text = value
    if not text.strip() or text in (".", ".."):
        raise UnsafeReceiptPath(f"{field} {value!r} is not a usable path component")
    if "/" in text or chr(92) in text or chr(0) in text:
        raise UnsafeReceiptPath(f"{field} {value!r} contains a path separator — refused (FR-022)")
    if Path(text).is_absolute() or Path(text).drive or Path(text).anchor:
        raise UnsafeReceiptPath(f"{field} {value!r} is an absolute path — refused (FR-022)")
    return text


def _confine(root: str | Path, *parts: str) -> Path:
    """Join ``parts`` under ``root`` and PROVE the result stays beneath it.

    DEFENCE IN DEPTH, AND VERIFIED AT ITS OWN BOUNDARY. ``_safe_component``
    rejects every escape vector reachable from this module's public callers, so
    this containment check cannot be exercised through ``receipt_path`` /
    ``expected_set_path`` — which is why it survived mutation testing on
    2026-08-26 and was published then as NOT-VERIFIED. A backstop whose contract
    is never asserted is indistinguishable from a no-op, so it is now tested
    directly: ``tests/faultinj/test_receipt_reuse.py::test_confine_*`` covers the
    escape, the descendant, the root-is-its-own-root edge and the unresolvable
    path. 2026-08-26 (session 9): both mutants — neutering the containment raise,
    and letting the ``OSError`` branch return silently — are killed.

    It remains the backstop if a component guard is ever loosened, or if ``root``
    itself resolves through a symlink; ``_safe_component`` is still the
    load-bearing guard on the public path.
    """
    base = Path(root)
    candidate = base.joinpath(*parts)
    try:
        resolved_base = base.resolve()
        resolved = candidate.resolve()
    except OSError as exc:  # unresolvable ⇒ cannot be proven contained
        raise UnsafeReceiptPath(f"receipt path {candidate} could not be resolved ({exc})") from exc
    if resolved != resolved_base and resolved_base not in resolved.parents:
        raise UnsafeReceiptPath(
            f"receipt path {resolved} escapes the receipts root {resolved_base} — refused (FR-022)"
        )
    return candidate


def receipt_path(root: str | Path, area: str, run_id: str, check_id: str) -> Path:
    """Canonical sidecar path for one check's receipt (FR-022), confined to ``root``."""
    return _confine(
        root,
        _safe_component(area, field="area"),
        _safe_component(run_id, field="run_id"),
        f"{_safe_component(check_id, field='check_id')}.receipt.json",
    )


def expected_set_path(root: str | Path, run_id: str) -> Path:
    """Path of a run's declared expected-check set (FR-023), confined to ``root``."""
    return _confine(root, _safe_component(run_id, field="run_id"), "expected.json")


def run_receipts(root: str | Path, run_id: str) -> list[Path]:
    """Every receipt file produced under ``run_id`` (any area).

    Used to reconcile the per-run expected set against what actually ran
    (FR-013): an expected check with no file here is a *missing* check.
    """
    base = Path(root)
    if not base.exists():
        return []
    return sorted(base.glob(f"*/{run_id}/*.receipt.json"))
