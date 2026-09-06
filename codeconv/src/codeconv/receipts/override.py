"""Override — a recorded, scoped, EXPIRING way past a refusal (FR-012).

A refusal MUST NOT be suppressible by ordinary configuration. Where an engineer must proceed
regardless, the only path is an explicit recorded override that reuses the established
informed-consent shape (briefing + explicit acknowledgement + rationale + scope + a MANDATORY
expiry — there is no indefinite override) and remains visible in the receipt. An override never
applies beyond its recorded scope, so one recorded once can never silently authorise every
future refusal of its kind.

Overrides are engineer decisions, never agent decisions — this module records them; it does not
grant them. Covers task T022; research decision D6.

FEATURE 109 (2026-09-06), engineer ruling ``Q-olg17-02``: the RULES moved to
``<repo>/scripts/lib/adoption_gate.py`` so that the stdlib-only evidence-signal audit can apply
the *same* implementation instead of writing a second one (feature 108 FR-006b forbids a second
override mechanism; FR-014 forbids making the audit venv-dependent). This module is now a
re-export. Its public names, signatures and semantics are UNCHANGED, and feature 078's existing
tests are the regression proof of the move — 078 is not re-opened semantically (``Q-olg15-09``).

Do not re-implement anything here. A copy would be the second mechanism FR-006b exists to
prevent, and ``scripts/tests/test_evidence_signal_audit.py::test_fr013_the_adoption_and_override_rules_have_exactly_ONE_implementation`` asserts these are the SAME
function objects, so a copy fails the suite rather than drifting silently.
"""

from __future__ import annotations

from ._shared import gate as _gate

OverrideInvalid = _gate.OverrideInvalid
Scope = _gate.Scope
Override = _gate.Override
record = _gate.record
applies = _gate.applies

__all__ = ["OverrideInvalid", "Scope", "Override", "record", "applies"]
