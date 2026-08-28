"""Adoption manifest (FR-019/020/021) + per-run expected-set (FR-023).

One absence-is-an-error rule at two granularities (research D7): an *area* that
never declares its adoption, and a *run* that never declares its expected checks.
Both refuse rather than default to a pass, so a check that silently stops existing
is as loud as an area that silently never adopts.

Covers tasks T020 and T021. Implements
``specs/078-verification-receipts/contracts/manifest-and-expected.md``.
"""

from __future__ import annotations

import json
from pathlib import Path

from . import paths, receipt as receipt_mod

# The FR-017 areas in glpnet's scope (the buildkit-side 3rtask/codexreview live in
# buildkit's own manifest — research D3). ``reference`` is the MVP proof target.
GLPNET_AREAS = ("build-gate", "coop", "roadmap-sync", "test-harness", "reference")


class MissingDeclaration(Exception):
    """An area is absent from the adoption manifest — an error, never a pass (FR-020)."""


class UndeclaredRun(Exception):
    """A run declared no expected-check set — an unverifiable run refuses (FR-023)."""


# ---- adoption manifest (FR-019/020/021) -----------------------------------

def load_adoption(path: str | Path = paths.ADOPTION_MANIFEST) -> dict[str, str]:
    """Load the per-repo adoption manifest as ``{area: state}``.

    Enforces FR-019's enumeration requirement: every GLPNET area MUST appear.
    A missing manifest, or a manifest omitting any area, raises — absence is an
    error (FR-020), and SC-002's denominator is the full enumeration (FR-021).
    """
    p = Path(path)
    if not p.exists():
        raise MissingDeclaration(f"adoption manifest not found at {p} — FR-019 requires it checked in")
    data = json.loads(p.read_text(encoding="utf-8"))
    entries = {e["area"]: e["state"] for e in data.get("areas", [])}
    missing = [a for a in GLPNET_AREAS if a not in entries]
    if missing:
        raise MissingDeclaration(
            f"adoption manifest at {p} omits area(s) {missing} — every FR-017 area MUST be "
            f"enumerated (FR-019/020); an unlisted area is an error, not non-adoption"
        )
    return entries


def adoption_state(manifest: dict[str, str], area: str) -> str:
    """The declared state of ``area``; raise if unlisted (FR-020)."""
    if area not in manifest:
        raise MissingDeclaration(f"area {area!r} is not declared — absence is an error (FR-020)")
    return manifest[area]


# ---- per-run expected-check set (FR-023) ----------------------------------

def declare_expected(root: str | Path, run_id: str, expected_checks: list[str]) -> Path:
    """Write a run's expected-check set in advance (FR-023)."""
    path = paths.expected_set_path(root, run_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"run_id": run_id, "expected_checks": expected_checks}, indent=2), encoding="utf-8")
    return path


def load_expected(root: str | Path, run_id: str) -> list[str]:
    """Load a run's expected-check set; a run with none refuses (FR-023)."""
    path = paths.expected_set_path(root, run_id)
    if not path.exists():
        raise UndeclaredRun(
            f"run {run_id!r} declared no expected-check set at {path} — an unverifiable run "
            f"refuses rather than reports (FR-023)"
        )
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except ValueError as exc:
        raise UndeclaredRun(
            f"run {run_id!r}: expected-check set at {path} is not readable JSON ({exc}) — "
            f"an unverifiable run refuses rather than reports (FR-023)"
        ) from exc
    if not isinstance(data, dict):
        raise UndeclaredRun(
            f"run {run_id!r}: expected-check set at {path} is not an object — "
            f"an unverifiable run refuses rather than reports (FR-023)"
        )
    # A declaration that belongs to a DIFFERENT run is not this run's declaration.
    declared_run = data.get("run_id")
    if declared_run != run_id:
        raise UndeclaredRun(
            f"run {run_id!r}: expected-check set at {path} declares run {declared_run!r} — "
            f"another run's declaration is not this run's (FR-023); refusing rather than reporting"
        )
    # FR-023: "a run with no declared set is not a run in which nothing was expected —
    # it is an unverifiable run". So a missing key, a non-list, or an EMPTY list is a
    # refusal, not an empty expected-set that makes missing_checks() vacuously clean.
    checks = data.get("expected_checks")
    if not isinstance(checks, list) or not checks:
        raise UndeclaredRun(
            f"run {run_id!r}: expected-check set at {path} declares no checks ({checks!r}) — "
            f"a run in which nothing is expected is unverifiable, not clean (FR-023)"
        )
    return list(checks)


def missing_checks(root: str | Path, run_id: str) -> list[str]:
    """Expected ``check_id``s with no receipt under the run — reported loud (FR-013).

    A check that did not run must not be indistinguishable from one that passed.
    """
    expected = set(load_expected(root, run_id))
    return sorted(expected - _ran(root, run_id))


def _ran(root: str | Path, run_id: str) -> set[str]:
    """The check_ids a run can PROVE it ran — by loading each receipt, not by name.

    A correctly-named file is not evidence: FR-001 requires proof a check executed,
    and a name is trivially producible without running anything. A receipt that will
    not load, will not validate, or names a different check/run does not count its
    filename's check as having run — it is exactly the absence FR-013 makes loud.
    """
    ran: set[str] = set()
    for p in paths.run_receipts(root, run_id):
        try:
            r = receipt_mod.load(p)
            receipt_mod.validate(r)
        except Exception:
            continue  # unreadable/invalid ⇒ no proof it ran ⇒ still missing (FR-013)
        if r.run_id != run_id:
            continue  # another run's receipt sitting in this run's dir proves nothing
        if r.check_id != p.name[: -len(".receipt.json")]:
            continue  # the filename disagrees with the content it claims to be
        ran.add(r.check_id)
    return ran
