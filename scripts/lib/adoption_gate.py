# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
# SPDX-License-Identifier: MIT
"""The ONE implementation of feature 078's adoption and informed-consent-override rules.

WHY THIS MODULE EXISTS, AND WHY IT IS HERE RATHER THAN IN `codeconv`
--------------------------------------------------------------------
Feature 108's FR-006b says a refusal must be overridable *only* through feature 078's
informed-consent override, and that this "MUST reuse 078's override machinery rather than
introduce a second one -- two override mechanisms is how an override becomes unauditable."

Feature 109 has to make the evidence-signal audit REFUSE, so the audit needs those rules. But:

  * 078's rules lived in `codeconv/src/codeconv/receipts/{override,manifest}.py`, importable
    only with the codeconv virtual environment on `sys.path`; and
  * `scripts/evidence_signal_audit.py` is deliberately **stdlib-only**, because "an audit that
    cannot run because a dependency is missing is the failure mode it exists to prevent" -- and
    a tool that did not run being read as "nothing to report" is measured instance 4.

Satisfying both by copying the rules into the audit would create the second implementation
FR-006b forbids -- arriving as duplicated *logic* rather than a duplicated *file*, which is
worse, because it drifts silently. Engineer ruling **`Q-olg17-02`** (2026-09-06) settled it:
**extract a stdlib-only reader that BOTH consume.**

So this module is the canonical implementation. `codeconv.receipts.override` and
`codeconv.receipts.manifest` DELEGATE to it and keep their public signatures, so feature 078's
existing tests are the regression proof of the move and 078 is not re-opened semantically
(`Q-olg15-09`). The audit imports it directly, by relative path, with no venv.

It reads 078's existing ON-DISK FORMATS unchanged. The formats are the contract; only the code
that applies them moved.

STDLIB ONLY. Adding a third-party import here silently re-breaks the audit on any host without
that dependency, which is the exact failure this file's placement exists to prevent.
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

# ---------------------------------------------------------------------------
# Adoption (078 FR-019/020/021)
# ---------------------------------------------------------------------------

#: The FR-017 areas in glpnet's scope. The buildkit-side `3rtask`/`codexreview` areas live in
#: buildkit's own manifest (engineer ruling Q-GLPNETS12-02, "Split 11 / 55").
GLPNET_AREAS = ("build-gate", "coop", "roadmap-sync", "test-harness", "reference")

#: The ONLY legal adoption states. An unrecognised value is not a third state to be interpreted
#: -- it is a broken declaration.
ADOPTION_STATES = ("adopted", "non-adopted")

ADOPTION_MANIFEST_REL = os.path.join(".specify", "receipts", "adoption.json")

# WHERE RECORDED OVERRIDES LIVE. Feature 109 shipped `record()` and `applies()` with no store and
# no reader, so a refusal could never actually be cleared by an override in the running tool --
# only in tests that constructed one in-process. That is exactly the "simulator in the test
# harness, not enforcement in the tool" shape the feature exists to remove, so the path belongs
# HERE, beside the rules, and not in whichever consumer happens to need it first.
#
# The file is OPTIONAL. Its absence means "no overrides recorded", which is the healthy state; it
# is never an error, because making the healthy state an error is how a gate gets switched off.
OVERRIDES_REL = os.path.join(".specify", "receipts", "overrides.json")


class MissingDeclaration(Exception):
    """An area is absent from the adoption manifest -- an error, never a pass (FR-020)."""


class UndeclaredState(Exception):
    """An area declared a state that is not one of ``ADOPTION_STATES``.

    The consumer's gate is ``state == "non-adopted"`` -- a single equality against ONE of the
    two states. Every other string therefore falls through to *adopted* semantics, so a typo
    (``"non-adopred"``, ``"pending"``) does not disable the gate loudly, it turns a receipt
    GREEN. Absence is already an error; a malformed declaration must be one for the same reason,
    or the enumeration requirement can be satisfied by nonsense.
    """


def load_adoption(path: str, *, required_areas: tuple[str, ...] = GLPNET_AREAS) -> dict[str, str]:
    """Load the per-repo adoption manifest as ``{area: state}``.

    Enforces FR-019's enumeration requirement: every required area MUST appear. A missing
    manifest, or one omitting any area, raises -- absence is an error (FR-020).
    """
    if not os.path.exists(path):
        raise MissingDeclaration(
            f"adoption manifest not found at {path} -- FR-019 requires it checked in")
    with open(path, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    raw = data.get("areas", [])

    # A dict comprehension over `raw` would let a repeated area silently win by being last --
    # two contradictory declarations, one of which is invisible.
    entries: dict[str, str] = {}
    duplicates: list[str] = []
    for e in raw:
        area = e["area"]
        if area in entries:
            duplicates.append(area)
        entries[area] = e["state"]
    if duplicates:
        raise UndeclaredState(
            f"adoption manifest at {path} declares area(s) {sorted(set(duplicates))} more than "
            f"once -- a repeated area means two states are declared and only one is read "
            f"(FR-019); declare each area exactly once")

    illegal = {a: s for a, s in entries.items() if s not in ADOPTION_STATES}
    if illegal:
        raise UndeclaredState(
            f"adoption manifest at {path} declares state(s) {illegal} that are not one of "
            f"{list(ADOPTION_STATES)} -- the consumer gates on equality with 'non-adopted', so "
            f"any other value silently takes ADOPTED semantics and turns a receipt green "
            f"(FR-019/020)")

    missing = [a for a in required_areas if a not in entries]
    if missing:
        raise MissingDeclaration(
            f"adoption manifest at {path} omits area(s) {missing} -- every FR-017 area MUST be "
            f"enumerated (FR-019/020); an unlisted area is an error, not non-adoption")
    return entries


def adoption_state(manifest: dict[str, str], area: str) -> str:
    """The declared state of ``area``; raise if unlisted (FR-020)."""
    if area not in manifest:
        raise MissingDeclaration(
            f"area {area!r} is not declared -- absence is an error (FR-020)")
    return manifest[area]


# ---------------------------------------------------------------------------
# Override (078 FR-012)
# ---------------------------------------------------------------------------

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
    expiry: str  # ISO8601; MANDATORY -- no indefinite override

    def to_json(self) -> dict[str, Any]:
        return {
            "briefing": self.briefing,
            "acknowledged": self.acknowledged,
            "rationale": self.rationale,
            "scope": {"area": self.scope.area, "check": self.scope.check,
                      "reason": self.scope.reason},
            "expiry": self.expiry,
        }


def record(*, area: str, check: str, reason: str, briefing: str, rationale: str,
           acknowledged: bool, expiry: str, now: datetime | None = None) -> Override:
    """Record an engineer override; reject an incomplete one (FR-012, SC-006).

    Rejection happens HERE, at the point of recording -- not at the point of reliance. An
    override with no expiry that is only rejected when someone tries to use it has already
    been written down and believed.

    ``now`` MIRRORS ``applies()``, and is here for the same reason it is there: a caller with a
    pinned clock (a test, a replay of a recorded decision) must be able to ask the question as of
    that clock rather than as of wall time. Defaulting to wall time keeps every existing caller's
    behaviour, which is what makes the completeness check below an EXTENSION of 078 rather than a
    re-opening of it (109 FR-024).
    """
    if not acknowledged:
        raise OverrideInvalid("an override requires explicit acknowledgement (FR-012)")
    if not str(briefing).strip():
        raise OverrideInvalid(
            "an override requires a briefing -- informed consent needs the information (FR-012)")
    if not rationale.strip():
        raise OverrideInvalid(
            "an override requires a rationale -- no silent suppressions (SC-006)")
    if not expiry.strip():
        raise OverrideInvalid(
            "an override requires a mandatory expiry -- no indefinite override (FR-012)")
    # ...and the expiry has to BE an expiry. Accepting any non-blank string meant `expires_on:
    # "soon"` was recorded successfully and then silently inert at `applies()`, so an engineer
    # was told the override was recorded and discovered at the next refusal that it never
    # applied. That is validation at RELIANCE, which is what the paragraph above forbids.
    try:
        _exp = datetime.fromisoformat(expiry.strip())
    except ValueError:
        raise OverrideInvalid(
            f"expiry {expiry!r} is not an ISO-8601 timestamp -- an unparseable expiry is inert, "
            "and an inert override is a suppression nobody can audit (FR-012)")
    if _exp.tzinfo is None:
        _exp = _exp.replace(tzinfo=timezone.utc)
    if _exp <= (now or datetime.now(timezone.utc)):
        raise OverrideInvalid(
            f"expiry {expiry!r} is in the past -- an override that has already expired cannot be "
            "recorded, for the same reason one with no expiry cannot (FR-012)")
    return Override(
        briefing=briefing, acknowledged=acknowledged, rationale=rationale,
        scope=Scope(area=area, check=check, reason=reason), expiry=expiry,
    )


def applies(override: Override, area: str, check: str, reason: str,
            now: datetime | None = None) -> bool:
    """True iff the override covers this area+check+REASON and has not expired (FR-012).

    Outside its recorded scope or past its expiry the override is inert and the underlying
    refusal stands.

    ``reason`` is part of the recorded scope, not decoration: FR-012 defines scope as "the area,
    check and reason it covers" and forbids an override applying beyond it. Matching on
    area+check alone lets one override -- recorded for one specific refusal -- silently authorise
    **every other refusal that check can raise** until its expiry, which is precisely the
    "recorded once, authorises everything after" failure FR-012 exists to prevent.
    """
    if override.scope.area != area or override.scope.check != check:
        return False
    if override.scope.reason != reason:
        return False
    now = now or datetime.now(timezone.utc)
    try:
        exp = datetime.fromisoformat(override.expiry)
    except ValueError:
        return False
    if exp.tzinfo is None:
        exp = exp.replace(tzinfo=timezone.utc)
    return now <= exp
