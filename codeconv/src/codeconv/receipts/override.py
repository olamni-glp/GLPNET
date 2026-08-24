"""Override — a recorded, scoped, EXPIRING way past a refusal (FR-012).

A refusal MUST NOT be suppressible by ordinary configuration. Where an engineer
must proceed regardless, the only path is an explicit recorded override that
reuses the established informed-consent shape (briefing + explicit acknowledgement
+ rationale + scope + a MANDATORY expiry — there is no indefinite override) and
remains visible in the receipt. An override never applies beyond its recorded
scope, so one recorded once can never silently authorise every future refusal of
its kind.

Overrides are engineer decisions, never agent decisions — this module records
them; it does not grant them. Covers task T022; research decision D6.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any


class OverrideInvalid(Exception):
    """An override missing acknowledgement, rationale, scope or expiry (FR-012)."""


@dataclass
class Scope:
    area: str
    check: str
    reason: str


@dataclass
class Override:
    briefing: str
    acknowledged: bool
    rationale: str
    scope: Scope
    expiry: str  # ISO8601; MANDATORY — no indefinite override

    def to_json(self) -> dict[str, Any]:
        return {
            "briefing": self.briefing,
            "acknowledged": self.acknowledged,
            "rationale": self.rationale,
            "scope": {"area": self.scope.area, "check": self.scope.check, "reason": self.scope.reason},
            "expiry": self.expiry,
        }


def record(*, area: str, check: str, reason: str, briefing: str, rationale: str,
           acknowledged: bool, expiry: str) -> Override:
    """Record an engineer override; reject an incomplete one (FR-012, SC-006)."""
    if not acknowledged:
        raise OverrideInvalid("an override requires explicit acknowledgement (FR-012)")
    if not rationale.strip():
        raise OverrideInvalid("an override requires a rationale — no silent suppressions (SC-006)")
    if not expiry.strip():
        raise OverrideInvalid("an override requires a mandatory expiry — no indefinite override (FR-012)")
    return Override(
        briefing=briefing, acknowledged=acknowledged, rationale=rationale,
        scope=Scope(area=area, check=check, reason=reason), expiry=expiry,
    )


def applies(override: Override, area: str, check: str, now: datetime | None = None) -> bool:
    """True iff the override covers this area+check and has not expired (FR-012).

    Outside its recorded scope or past its expiry the override is inert and the
    underlying refusal stands.
    """
    if override.scope.area != area or override.scope.check != check:
        return False
    now = now or datetime.now(timezone.utc)
    try:
        exp = datetime.fromisoformat(override.expiry)
    except ValueError:
        return False
    if exp.tzinfo is None:
        exp = exp.replace(tzinfo=timezone.utc)
    return now <= exp
