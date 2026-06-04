"""Tutorial identifier matching (D5, FR-002, SC-003).

PURE — operates on the in-memory chapter list; no I/O. Case-insensitive,
non-interactive (FR-004 spirit). Matching is tiered and stops at the first tier
that yields candidates:

  1. exact chapter id (``ch03``);
  2. zero-pad-normalized id (``ch3`` / ``3`` → ``ch03``);
  3. id prefix (``ch1`` already resolved by tier 2; ``ch`` → all → ambiguous);
  4. substring of the chapter human title (``core`` → ch03 "GLP Core").

A tier with exactly one candidate is a match; a tier with two or more is
ambiguous (report candidates, exit 4); zero across all tiers is no-match
(report available ids, exit 3).
"""

from __future__ import annotations

import re
from dataclasses import dataclass

from .corpus import Tutorial

# ``ch03`` / ``CH3`` / ``3`` / ``003`` → canonical ``chNN``.
_NUMERIC = re.compile(r"^(?:ch)?0*(\d+)$", re.IGNORECASE)


@dataclass(frozen=True)
class MatchResult:
    """Outcome of matching a query against the chapter list."""

    kind: str  # "one" | "none" | "ambiguous"
    matched: Tutorial | None = None
    candidates: tuple[str, ...] = ()  # ids — for ambiguous (and "none" → available)


def _normalize_numeric(query: str) -> str | None:
    """``ch3`` / ``3`` → ``ch03``; None when the query is not purely numeric."""
    m = _NUMERIC.match(query)
    if m is None:
        return None
    return f"ch{int(m.group(1)):02d}"


def match_tutorial(query: str, chapters: list[Tutorial]) -> MatchResult:
    """Match ``query`` to a single chapter, or report none / ambiguous (D5)."""
    q = query.strip().lower()
    norm = _normalize_numeric(q)

    tiers: list[list[Tutorial]] = [
        [c for c in chapters if c.id.lower() == q],
        [c for c in chapters if norm is not None and c.id.lower() == norm],
        [c for c in chapters if c.id.lower().startswith(q)],
        [c for c in chapters if c.title and q in c.title.lower()],
    ]

    for tier in tiers:
        if len(tier) == 1:
            return MatchResult(kind="one", matched=tier[0])
        if len(tier) > 1:
            return MatchResult(
                kind="ambiguous",
                candidates=tuple(c.id for c in tier),
            )

    return MatchResult(kind="none", candidates=tuple(c.id for c in chapters))


__all__ = ["MatchResult", "match_tutorial"]
