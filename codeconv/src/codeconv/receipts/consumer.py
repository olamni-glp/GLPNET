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
            return Reading(outcome=Outcome.UNREAD, receipt=None, successful=False, non_adoption=True)

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
    except (ReceiptInvalid, KeyError, ValueError) as exc:
        raise VerdictRefused(
            f"check {verdict.check_id!r}: malformed/invalid receipt at {path} ({exc}) — "
            f"treated as UNREAD, not a pass (FR-008)"
        ) from exc

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
