"""Consumer refusal — an unearned green cannot be read as a pass (FR-008/009/011).

A component that reads a check verdict MUST refuse a verdict lacking a conforming
receipt rather than defaulting to treating it as a pass. Absence or malformation
is treated as UNREAD (the receipt mechanism is subject to its own invariant). An
aggregate cannot be clean while any constituent is UNREAD/UNSEARCHABLE (FR-009).

Covers tasks T012 and T017. Implements
``specs/078-verification-receipts/contracts/consumer-refusal.md``.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from . import bind, receipt as receipt_mod
from .outcome import Outcome, worst
from .receipt import Receipt, ReceiptInvalid


class VerdictRefused(Exception):
    """A verdict was refused: names what was expected, what was found, and where (FR-011)."""


@dataclass
class Verdict:
    """The thing a consumer reads: a claim plus a pointer to its receipt (FR-022)."""

    check_id: str
    area: str
    receipt_pointer: str | None  # None ⇒ the check produced no receipt
    run_id: str | None = None    # the run this verdict belongs to — REQUIRED to accept a receipt


@dataclass
class Reading:
    outcome: Outcome
    receipt: Receipt | None
    successful: bool
    non_adoption: bool = False  # area declared non-adopted: usable behind a visible marker


def read(verdict: Verdict, adoption: dict[str, str] | None = None) -> Reading:
    """Read a verdict, refusing an unearned green (FR-008, contract C1/C3).

    ``adoption`` maps area -> "adopted" | "non-adopted"; when supplied it is the
    per-repo manifest's authority. An area absent from a supplied manifest is an
    error (FR-020). When ``adoption`` is None, FR-008 binds unconditionally.
    """
    if adoption is not None:
        state = adoption.get(verdict.area)
        if state is None:
            raise VerdictRefused(
                f"area {verdict.area!r} is not declared in the adoption manifest — "
                f"absence is an error, not a pass (FR-020); declare it before its verdicts are read"
            )
        if state == "non-adopted":
            # C1: the verdict "remains usable but MUST carry a visible non-adoption
            # marker". Discarding its receipt would disable verdicts instead of
            # phasing adoption — every real glpnet area starts non-adopted. Where a
            # receipt exists it is read and KEPT behind the marker; where the area
            # has not started emitting yet, UNREAD behind the marker. Never successful.
            if not verdict.receipt_pointer:
                return Reading(outcome=Outcome.UNREAD, receipt=None, successful=False, non_adoption=True)
            reading = _read_receipt(verdict)
            return Reading(outcome=reading.outcome, receipt=reading.receipt,
                           successful=False, non_adoption=True)

    return _read_receipt(verdict)


def _read_receipt(verdict: Verdict) -> Reading:
    """Resolve, validate and IDENTITY-BIND the receipt behind a verdict (C1)."""
    if not verdict.receipt_pointer:
        raise VerdictRefused(
            f"check {verdict.check_id!r} in area {verdict.area!r} produced a verdict with no receipt — "
            f"refused as incomplete (FR-008); expected a receipt, found none"
        )
    path = Path(verdict.receipt_pointer)
    if not path.exists():
        raise VerdictRefused(
            f"check {verdict.check_id!r}: expected receipt at {path}, found none — "
            f"treated as UNREAD, not a silent pass (FR-008)"
        )
    try:
        r = receipt_mod.load(path)
        receipt_mod.validate(r)
    except VerdictRefused:
        raise
    except Exception as exc:
        # A malformed SHAPE (a string where an object belongs, a null count) raises
        # TypeError/AttributeError out of load(); catching only ReceiptInvalid/
        # KeyError/ValueError let those escape as a CRASH instead of the named
        # UNREAD refusal C1.2 requires. Any failure to read is UNREAD, never a pass.
        raise VerdictRefused(
            f"check {verdict.check_id!r}: malformed/invalid receipt at {path} "
            f"({type(exc).__name__}: {exc}) — treated as UNREAD, not a pass (FR-008)"
        ) from exc

    # Identity binding (FR-002 — a receipt is evidence for EXACTLY ONE verdict).
    # Without this a check reuses another check's, another area's, or a prior run's
    # PASS: the receipt is present, valid and conforming — just not ITS receipt.
    if r.check_id != verdict.check_id or r.area != verdict.area:
        raise VerdictRefused(
            f"check {verdict.check_id!r} in area {verdict.area!r}: receipt at {path} is evidence for "
            f"check {r.check_id!r} in area {r.area!r} — a receipt binds to exactly one verdict "
            f"(FR-002); treated as UNREAD, not a pass"
        )
    # An OPTIONAL run binding is not a binding: with run_id left None the check
    # below was skipped and a prior run's PASS for the same check+area was
    # accepted as this run's evidence. A receipt-backed verdict must SAY which
    # run it belongs to; declining to say is refused, not waived.
    if verdict.run_id is None:
        raise VerdictRefused(
            f"check {verdict.check_id!r} in area {verdict.area!r}: a receipt-backed verdict must "
            f"declare its run_id so the receipt can be bound to THIS run (FR-002); none was given — "
            f"treated as UNREAD, not a pass"
        )
    if r.run_id != verdict.run_id:
        raise VerdictRefused(
            f"check {verdict.check_id!r}: receipt at {path} was produced by run {r.run_id!r}, "
            f"not run {verdict.run_id!r} — a prior run's PASS is not this run's evidence (FR-002); "
            f"treated as UNREAD, not a pass"
        )

    if bind.major(r.contract_version) != bind.major(bind.resolve_contract()[0]):
        raise VerdictRefused(
            f"check {verdict.check_id!r}: receipt contract {r.contract_version!r} is an "
            f"unrecognised MAJOR — treated as UNREAD, not accepted (FR-024)"
        )
    return Reading(outcome=r.outcome, receipt=r, successful=r.outcome.is_successful)


def aggregate(children: Iterable[Outcome]) -> Outcome:
    """The outcome of a parent over its children — worst wins (FR-009, contract C2).

    Closes instance 13: an aggregate cannot report clean while a constituent
    reported a non-success outcome.
    """
    return worst(children)
