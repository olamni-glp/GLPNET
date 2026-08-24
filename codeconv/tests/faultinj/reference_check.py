"""A purpose-built reference check the MVP mechanism is proven against (T019, D8).

The first ship-gate ships US1+US2+US3 proven against THIS check, not yet the six
real areas (those are the US4 retrofits). It examines a directory of items and
emits a conforming receipt; its knobs let the fault-injection suite deliberately
induce each silent-success mode (US3).
"""

from __future__ import annotations

from pathlib import Path

from codeconv.receipts import Receipt, Skip, Target, emit


def run_reference_check(
    *,
    root: str | Path,
    run_id: str,
    target_dir: str | Path,
    check_id: str = "reference.check",
    area: str = "reference",
    intended_identity: str | None = None,
    target_removed: bool = False,
    suppress_output: bool = False,
    falsify_count: bool = False,
    skipped: list[Skip] | None = None,
) -> Receipt:
    """Run the reference check and emit its receipt.

    Fault knobs (US3): ``target_removed`` (US3.1), ``suppress_output`` (US3.2 /
    instance 2), ``intended_identity`` mismatch (US3.4 / instance 9),
    ``falsify_count`` (US3.5).
    """
    tdir = Path(target_dir)
    requested = intended_identity or str(tdir)
    resolved = tdir.exists() and not target_removed

    # US3.1 — target could not be resolved at all.
    if not resolved:
        target = Target(
            kind="path", identity=str(tdir), requested=requested, resolved=False,
            unresolved_reason="target directory absent",
        )
        return emit(check_id=check_id, area=area, target=target,
                    examined_count=0, total_count=None, run_id=run_id, root=root, skipped=skipped or [])

    # US3.4 / instance 9 — resolved to a different location than intended.
    if intended_identity is not None and str(tdir) != intended_identity:
        target = Target(
            kind="path", identity=str(tdir), requested=intended_identity, resolved=False,
            unresolved_reason=f"target mismatch: resolved {tdir}, intended {intended_identity}",
        )
        return emit(check_id=check_id, area=area, target=target,
                    examined_count=0, total_count=None, run_id=run_id, root=root, skipped=skipped or [])

    items = sorted(p.name for p in tdir.iterdir()) if tdir.is_dir() else []
    total = len(items)
    target = Target(kind="path", identity=str(tdir), requested=requested)

    # US3.5 — deliberately claim to have examined more than exist.
    if falsify_count:
        return emit(check_id=check_id, area=area, target=target,
                    examined_count=total + 89, total_count=total, run_id=run_id, root=root)

    # US3.2 / instance 2 — the output/findings block was suppressed: nothing was
    # actually examined, so this is UNREAD, never "0 findings".
    examined_count = 0 if suppress_output else total
    examined = [] if suppress_output else items
    return emit(check_id=check_id, area=area, target=target,
                examined_count=examined_count, total_count=total, examined=examined,
                run_id=run_id, root=root, skipped=skipped or [])
