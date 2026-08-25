"""Receipt construction, classification, bounding, validation and emission.

The evidence a check ran, bound to exactly one verdict (FR-002/003/004). A check
that reports success/clean/zero-findings without emitting proof it executed
against its intended target violates FR-001; ``emit`` makes that impossible by
constructing and writing the receipt as part of producing the verdict.

Covers tasks T006–T011 and T014–T016. Implements
``specs/078-verification-receipts/data-model.md`` and ``contracts/receipt-schema.design.md``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence

from . import bind, paths
from .outcome import Outcome


class ReceiptInvalid(Exception):
    """A receipt violates a contract invariant (FR-005/010). Never silently accepted."""


@dataclass
class Target:
    """What a check examined, identified as ACTUALLY RESOLVED at run time (FR-003)."""

    kind: str          # path | revision | host | root | cursor | item-set
    identity: str      # the resolved identifier
    resolved: bool = True
    requested: str | None = None          # what was asked for, if it differs (FR-003)
    unresolved_reason: str | None = None   # required when resolved is False (FR-011)

    def to_json(self) -> dict[str, Any]:
        d: dict[str, Any] = {"kind": self.kind, "identity": self.identity, "resolved": self.resolved}
        if self.requested is not None and self.requested != self.identity:
            d["requested"] = self.requested
        if self.unresolved_reason is not None:
            d["unresolved_reason"] = self.unresolved_reason
        return d


@dataclass
class Skip:
    item: str
    reason: str

    def to_json(self) -> dict[str, Any]:
        return {"item": self.item, "reason": self.reason}


@dataclass
class Truncation:
    enumerations: bool = False
    dropped: int = 0
    byte_capped: list[str] = field(default_factory=list)

    def to_json(self) -> dict[str, Any]:
        return {"enumerations": self.enumerations, "dropped": self.dropped, "byte_capped": list(self.byte_capped)}


@dataclass
class Receipt:
    schema_version: str
    contract_version: str
    check_id: str
    area: str
    run_id: str                      # data-model R2: unique per (area, run_id)
    resolved_target: Target
    outcome: Outcome
    examined_count: int
    total_count: int | None          # None ⇒ "unknown"
    skipped: list[Skip]
    skipped_total: int
    examined: list[str]
    truncated: Truncation
    ran_at: str
    verdict_pointer: str
    override: dict[str, Any] | None = None

    def to_json(self) -> dict[str, Any]:
        return {
            "schema_version": self.schema_version,
            "contract_version": self.contract_version,
            "check_id": self.check_id,
            "area": self.area,
            "run_id": self.run_id,
            "resolved_target": self.resolved_target.to_json(),
            "outcome": self.outcome.value,
            "examined_count": self.examined_count,
            "total_count": "unknown" if self.total_count is None else self.total_count,
            "skipped": [s.to_json() for s in self.skipped],
            "skipped_total": self.skipped_total,
            "examined": list(self.examined),
            "truncated": self.truncated.to_json(),
            "ran_at": self.ran_at,
            "verdict_pointer": self.verdict_pointer,
            **({"override": self.override} if self.override is not None else {}),
        }


def classify(
    target: Target,
    examined_count: int,
    total_count: int | None,
    problems: Sequence[Any],
) -> Outcome:
    """Derive the one outcome from resolution + counts + problems (FR-006, D4).

    - not resolved            -> UNSEARCHABLE (never clean; instances 9, 10)
    - resolved, examined<total-> UNREAD (partial never presents as whole)
    - resolved, total unknown -> UNREAD (emptiness/fullness cannot be proven)
    - resolved, examined==total, problems -> FAIL
    - resolved, examined==total==0        -> EMPTY (legitimate pass)
    - resolved, examined==total>0, clean  -> PASS
    """
    if not target.resolved:
        return Outcome.UNSEARCHABLE
    if total_count is None:
        return Outcome.UNREAD
    if examined_count < total_count:
        return Outcome.UNREAD
    if problems:
        return Outcome.FAIL
    if examined_count == 0 and total_count == 0:
        return Outcome.EMPTY
    return Outcome.PASS


def _cap_enum(items: Sequence[str], truncation: Truncation) -> list[str]:
    """Cap an enumeration at the declared max, recording what was dropped (FR-005)."""
    capped = [_cap_field(str(x), truncation) for x in items[: bind.MAX_ENUM]]
    dropped = max(0, len(items) - bind.MAX_ENUM)
    if dropped:
        truncation.enumerations = True
        truncation.dropped += dropped
    return capped


def _cap_field(value: str, truncation: Truncation) -> str:
    """Byte-backstop a single field, recording that it was capped (FR-005)."""
    encoded = value.encode("utf-8")
    if len(encoded) <= bind.MAX_FIELD_BYTES:
        return value
    truncation.byte_capped.append(value[:24] + "…")
    return encoded[: bind.MAX_FIELD_BYTES].decode("utf-8", errors="ignore")


def validate(receipt: Receipt) -> None:
    """Enforce the contract invariants; raise ``ReceiptInvalid`` on any breach.

    A falsified or impossible receipt is detectable (FR-010) rather than trusted.
    """
    r = receipt
    if r.total_count is not None and r.examined_count > r.total_count:
        raise ReceiptInvalid(
            f"examined_count {r.examined_count} exceeds total_count {r.total_count} "
            f"for check {r.check_id!r} — impossible count (FR-010)"
        )
    # FR-010 reconciliation is over BOTH counts (data-model §2: examined_total +
    # skipped_total <= target_total). Checking examined alone accepts 5 examined /
    # 5 total / 1 skipped, which claims six outcomes from a five-item target.
    if r.total_count is not None and r.examined_count + r.skipped_total > r.total_count:
        raise ReceiptInvalid(
            f"examined_count {r.examined_count} + skipped_total {r.skipped_total} exceeds "
            f"total_count {r.total_count} for check {r.check_id!r} — self-inconsistent (FR-010)"
        )
    # PASS is earned, not assumed: without this branch a PASS with an unresolved
    # target or an unknown total validates and is then reported successful (FR-007).
    if r.outcome is Outcome.PASS and not (
        r.resolved_target.resolved and r.total_count is not None
        and r.examined_count == r.total_count and r.examined_count > 0
    ):
        raise ReceiptInvalid(
            f"PASS requires a resolved target fully examined with a known non-zero total "
            f"for {r.check_id!r} (FR-006/007); an unearned PASS is the failure this feature closes"
        )
    if r.outcome is Outcome.EMPTY and not (
        r.resolved_target.resolved and r.total_count is not None and r.examined_count == r.total_count
    ):
        raise ReceiptInvalid(f"EMPTY requires a fully-examined resolved target for {r.check_id!r}")
    if r.outcome is Outcome.UNSEARCHABLE and (r.resolved_target.resolved or not r.resolved_target.unresolved_reason):
        raise ReceiptInvalid(f"UNSEARCHABLE requires an unresolved target with a reason for {r.check_id!r}")
    if r.outcome is Outcome.UNREAD and r.resolved_target.resolved and r.total_count is not None:
        if r.examined_count >= r.total_count:
            raise ReceiptInvalid(f"UNREAD requires examined < total for {r.check_id!r}")
    if r.truncated.enumerations and r.truncated.dropped <= 0:
        raise ReceiptInvalid(f"a truncated receipt must record how many entries were dropped ({r.check_id!r})")


def emit(
    *,
    check_id: str,
    area: str,
    target: Target,
    examined_count: int,
    total_count: int | None,
    run_id: str,
    root: str | Path,
    problems: Sequence[Any] = (),
    skipped: Sequence[Skip] = (),
    examined: Sequence[str] = (),
    ran_at: str | None = None,
    override: dict[str, Any] | None = None,
    write: bool = True,
) -> Receipt:
    """Build, bound, validate and (by default) write a receipt beside its verdict.

    Returns the :class:`Receipt`; its ``verdict_pointer`` is what the caller
    attaches to the human/machine verdict (FR-022). Raises :class:`ReceiptInvalid`
    on an impossible receipt (e.g. a falsified count) — a check cannot emit an
    unearned green.
    """
    contract_version, _schema = bind.resolve_contract()
    outcome = classify(target, examined_count, total_count, problems)

    truncation = Truncation()
    capped_examined = _cap_enum(list(examined), truncation)
    capped_skipped = list(skipped)[: bind.MAX_ENUM]
    dropped_skipped = max(0, len(skipped) - bind.MAX_ENUM)
    if dropped_skipped:
        truncation.enumerations = True
        truncation.dropped += dropped_skipped

    pointer = str(paths.receipt_path(root, area, run_id, check_id))
    receipt = Receipt(
        schema_version=contract_version,
        contract_version=contract_version,
        check_id=check_id,
        area=area,
        run_id=run_id,
        resolved_target=target,
        outcome=outcome,
        examined_count=examined_count,
        total_count=total_count,
        skipped=list(capped_skipped),
        skipped_total=len(skipped),
        examined=capped_examined,
        truncated=truncation,
        ran_at=ran_at or datetime.now(timezone.utc).isoformat(),
        verdict_pointer=pointer,
        override=override,
    )
    validate(receipt)  # a falsified/impossible receipt raises before it is written
    if write:
        path = Path(pointer)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(receipt.to_json(), indent=2), encoding="utf-8")
    return receipt


def load(path: str | Path) -> Receipt:
    """Read a receipt sidecar back into a :class:`Receipt` (for consumers)."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    tc = data["total_count"]
    tgt = data["resolved_target"]
    return Receipt(
        schema_version=data["schema_version"],
        contract_version=data["contract_version"],
        check_id=data["check_id"],
        area=data["area"],
        run_id=data["run_id"],  # absent ⇒ KeyError ⇒ the consumer refuses it as malformed
        resolved_target=Target(
            kind=tgt["kind"], identity=tgt["identity"], resolved=tgt["resolved"],
            requested=tgt.get("requested"), unresolved_reason=tgt.get("unresolved_reason"),
        ),
        outcome=Outcome(data["outcome"]),
        examined_count=data["examined_count"],
        total_count=None if tc == "unknown" else tc,
        skipped=[Skip(item=s["item"], reason=s["reason"]) for s in data.get("skipped", [])],
        skipped_total=data.get("skipped_total", 0),
        examined=list(data.get("examined", [])),
        truncated=Truncation(**data.get("truncated", {})),
        ran_at=data["ran_at"],
        verdict_pointer=data["verdict_pointer"],
        override=data.get("override"),
    )
