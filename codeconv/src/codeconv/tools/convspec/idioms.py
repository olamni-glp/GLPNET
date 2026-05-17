"""Conversion-idiom KB + research-provenance cache (feature 018, T031).

Per ``contracts/convspec_idiom_schema.md`` (FR-012/013/014/024 ·
SC-007/008). ``codeconv.conversion_idioms`` = the persistent
codebase-scoped idiom KB; ``codeconv.research_findings`` = the
official-docs-authoritative provenance cache (``construct_key`` UNIQUE
⇒ a construct is **never re-researched** — offline-reproducible after
first research).

This module is the **deterministic KB layer** (DB reads/writes + the
pure decision-order predicate). The actual research (web) is spawned by
the ``/codeconv-convspec`` skill on a KB miss — never here, never in a
DBOS step (replay-safety, R3).
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any, Optional

from sqlalchemy import text

# Decision-order outcomes (convspec_idiom_schema.md § "Decision order").
REUSE = "reuse"  # active idiom hit → reuse verbatim, NO research
ESCALATE = "escalate"  # conflicted/escalated idiom, or undecidable
CACHED_RESEARCH = "cached_research"  # research_findings hit → use cached
NEEDS_RESEARCH = "needs_research"  # miss → skill must spawn research


@dataclass(frozen=True)
class IdiomHit:
    id: int
    construct_key: str
    source_form: str
    target_form: str
    status: str  # active | conflicted | escalated


def normalise_construct(raw: str) -> str:
    """Construct → stable ``construct_key`` (lookup key). Deterministic:
    lowercase, collapse whitespace, strip — so the same construct maps
    to the same key across files (cross-file consistency, SC-007)."""
    return re.sub(r"\s+", " ", raw.strip().lower())


def lookup_idiom(engine: Any, construct_key: str) -> Optional[IdiomHit]:
    with engine.connect() as conn:
        r = conn.execute(
            text(
                "SELECT id, construct_key, source_form, target_form, status "
                "FROM codeconv.conversion_idioms WHERE construct_key = :k"
            ),
            {"k": construct_key},
        ).first()
    if r is None:
        return None
    return IdiomHit(r[0], r[1], r[2], r[3], r[4])


def lookup_research(engine: Any, construct_key: str) -> Optional[dict]:
    with engine.connect() as conn:
        r = conn.execute(
            text(
                "SELECT id, authoritative_source, conclusion, is_authoritative "
                "FROM codeconv.research_findings WHERE construct_key = :k"
            ),
            {"k": construct_key},
        ).first()
    if r is None:
        return None
    return {
        "id": r[0],
        "authoritative_source": r[1],
        "conclusion": r[2],
        "is_authoritative": bool(r[3]),
    }


def decide(engine: Any, construct_key: str) -> str:
    """The MANDATORY decision order (convspec_idiom_schema.md). Pure
    w.r.t. its inputs (the two cached tables) — deterministic, no
    research/network here.

    1. idiom hit active        → REUSE (no research, no re-derive)
    2. idiom hit conflicted/esc → ESCALATE (FR-014, never guess)
    3. research_findings hit    → CACHED_RESEARCH (FR-024 never re-research)
       - cached but not authoritative → ESCALATE (FR-024)
    4. miss                     → NEEDS_RESEARCH (skill spawns research)
    """
    idiom = lookup_idiom(engine, construct_key)
    if idiom is not None:
        return REUSE if idiom.status == "active" else ESCALATE
    finding = lookup_research(engine, construct_key)
    if finding is not None:
        return CACHED_RESEARCH if finding["is_authoritative"] else ESCALATE
    return NEEDS_RESEARCH


def record_research(
    engine: Any,
    *,
    construct_key: str,
    query: str,
    authoritative_source: str,
    conclusion: str,
    is_authoritative: bool,
    corroborating: Optional[list] = None,
) -> None:
    """Insert-once cache (``ON CONFLICT (construct_key) DO NOTHING`` —
    FR-012/FR-024: never re-researched, no duplicate/ambiguous rows
    under parallel agents). Never updated."""
    import json as _json

    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.research_findings "
                "(construct_key, query, authoritative_source, "
                " corroborating_sources, conclusion, is_authoritative) "
                "VALUES (:k, :q, :a, CAST(:c AS jsonb), :cc, :ia) "
                "ON CONFLICT (construct_key) DO NOTHING"
            ),
            {
                "k": construct_key,
                "q": query,
                "a": authoritative_source,
                "c": _json.dumps(corroborating or []),
                "cc": conclusion,
                "ia": is_authoritative,
            },
        )


def record_idiom(
    engine: Any,
    *,
    construct_key: str,
    source_form: str,
    target_form: str,
    rationale: str,
    first_seen_path: str,
    research_finding_id: Optional[int] = None,
) -> str:
    """Record a new idiom on first resolution, OR flag a conflict.

    FR-014: if an active idiom already exists with a DIFFERENT
    ``target_form``, do NOT silently overwrite — mark it ``conflicted``
    and return ESCALATE. A matching existing idiom ⇒ idempotent REUSE.
    A fresh key ⇒ insert active, return REUSE.
    """
    existing = lookup_idiom(engine, construct_key)
    if existing is not None:
        if existing.status != "active":
            return ESCALATE
        if existing.target_form != target_form:
            with engine.begin() as conn:
                conn.execute(
                    text(
                        "UPDATE codeconv.conversion_idioms "
                        "SET status = 'conflicted' WHERE construct_key = :k"
                    ),
                    {"k": construct_key},
                )
            return ESCALATE
        return REUSE  # identical decision already recorded — idempotent
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.conversion_idioms "
                "(construct_key, source_form, target_form, rationale, "
                " research_finding_id, first_seen_path, status) "
                "VALUES (:k, :s, :t, :r, :rf, :p, 'active') "
                "ON CONFLICT (construct_key) DO NOTHING"
            ),
            {
                "k": construct_key,
                "s": source_form,
                "t": target_form,
                "r": rationale,
                "rf": research_finding_id,
                "p": first_seen_path,
            },
        )
    return REUSE


__all__ = [
    "CACHED_RESEARCH",
    "ESCALATE",
    "NEEDS_RESEARCH",
    "REUSE",
    "IdiomHit",
    "decide",
    "lookup_idiom",
    "lookup_research",
    "normalise_construct",
    "record_idiom",
    "record_research",
]
