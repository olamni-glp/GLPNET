"""The SINGLE tiered fidelity scorer (FR-013, SC-004).

PURE, no I/O, no LM. Imported by BOTH the production gate (``tools/equiv``
promote/status) AND the GEPA optimizer metric (``tools/codegen_opt/metric.py``)
— one implementation ⇒ they agree by construction (SC-004). The score is
NEVER persisted in ``dart_equivalence`` (only its inputs are); it is
recomputed on read so the gate and GEPA can never drift (R4).

Contract: ``specs/020-trace-equivalence-fidelity/contracts/fidelity_metric.md``.

Tiers (verbatim from FR-013):
    0.0                      non-compile hard floor
    0.25                     compiling, no equivalence evidence (flat low band)
    0.5 + 0.5*frac (<1.0)    high band, monotonic in frac, clamped strictly < 1.0
    1.0                      total trace-equivalence ONLY (frac == 1.0 snap)
"""

from __future__ import annotations

import math
from dataclasses import dataclass


# The largest representable float strictly below 1.0. A file at frac=0.999…
# must never read as exactly 1.0 — only the frac==1.0 snap reaches 1.0.
NEXTBELOW_1: float = math.nextafter(1.0, 0.0)


@dataclass(frozen=True)
class FidelityInputs:
    """Inputs the scorer consumes — aggregated identically by the gate and
    the GEPA metric from ``dart_equivalence`` (fidelity_metric.md § Signature).

    ``trace_equivalent_sources`` counts outcome-equivalent rows for bonds
    (outcome mode) the same as trace-equivalent rows (trace mode); stale rows
    are excluded by the caller before building these inputs (FR-016).
    """

    builds: bool
    back_tested: bool
    trace_captured: bool
    in_scope_sources: int
    trace_equivalent_sources: int


def score(file_state: FidelityInputs) -> float:
    """Tiered fidelity in ``[0.0, 1.0]`` (fidelity_metric.md § Exact tiers)."""
    if not file_state.builds:
        return 0.0  # hard floor
    frac = (
        file_state.trace_equivalent_sources / file_state.in_scope_sources
        if file_state.in_scope_sources
        else 0.0
    )
    if not (file_state.back_tested and file_state.trace_captured):
        return 0.25  # flat low band — compiling, no equivalence evidence
    if frac >= 1.0:
        return 1.0  # total trace-equivalence ONLY
    return min(0.5 + 0.5 * frac, NEXTBELOW_1)  # high band, clamped strictly < 1.0
