"""Verification receipts — no check may pass without proving it ran (feature 078).

Every check emits a *receipt* alongside its verdict, naming the resolved target,
how much was examined, and when; a verdict without a conforming receipt is refused
rather than read as a pass. Outcomes are classified as exactly one of
PASS / EMPTY / UNREAD / UNSEARCHABLE / FAIL, and the three "nothing found" cases
never collapse.

This is the glpnet-side reference implementation. The receipt *contract* is owned
by buildkit and bound here by version (FR-024, :mod:`.bind`); glpnet delivers the
consumer, the four glpnet-area adoptions, and the conformance fixture.

Implements ``specs/078-verification-receipts/`` (plan.md, data-model.md, contracts/).
"""

from __future__ import annotations

from .outcome import Outcome, worst
from .receipt import Receipt, ReceiptInvalid, Skip, Target, Truncation, classify, emit, load, validate
from .consumer import Reading, Verdict, VerdictRefused, aggregate, read
from .manifest import (
    GLPNET_AREAS,
    MissingDeclaration,
    UndeclaredRun,
    declare_expected,
    load_adoption,
    load_expected,
    missing_checks,
)
from .override import Override, OverrideInvalid, Scope, applies, record

__all__ = [
    "Outcome", "worst",
    "Receipt", "ReceiptInvalid", "Skip", "Target", "Truncation",
    "classify", "emit", "load", "validate",
    "Reading", "Verdict", "VerdictRefused", "aggregate", "read",
    "GLPNET_AREAS", "MissingDeclaration", "UndeclaredRun",
    "declare_expected", "load_adoption", "load_expected", "missing_checks",
    "Override", "OverrideInvalid", "Scope", "applies", "record",
]
