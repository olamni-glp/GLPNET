"""Unified workspace read facade (feature 018 — FR-006 / FR-022, D2 / R8).

One shared accessor over the feature-016 workspace tables
(``codeconv.workspace_settings``, ``codeconv.excluded_directories``,
``codeconv.phase_sequence``, ``codeconv.phase_status``).

**D2 governing principle (Gabi, emphatic 2026-05-17).** This module is a
*read facade only*. It reproduces the EXACT SQL the feature-016 tools
already issue — it does NOT change what any tool reads, write any table,
or introduce a new workspace model. The builder/convspec consume the
workspace through this single accessor; existing tool modules keep their
own (identical) reads unchanged. Unifying the *concept* (one place that
knows the workspace table shapes) is the only refactor here, per
research R8 ("``workspace.py`` is a read facade over existing tables").

Schema source of truth: migration ``0003_d2net_into_codeconv`` —
``workspace_settings(key text PK, value text)``,
``excluded_directories(path text PK, kind text CHECK in
('tool','pattern','manual'))``, ``phase_sequence(phase text PK,
sequence integer)``, ``phase_status(phase text PK, status text,
last_updated timestamptz)``.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from sqlalchemy import text


@dataclass(frozen=True)
class ExcludedDirectory:
    """One row of ``codeconv.excluded_directories``."""

    path: str
    kind: str  # 'tool' | 'pattern' | 'manual'


@dataclass(frozen=True)
class PhaseEntry:
    """One conversion phase: its ordering and current status.

    ``sequence``/``status`` are ``None`` when the phase exists in only one
    of ``phase_sequence`` / ``phase_status`` (the two tables are seeded
    independently by ``codeconv init``).
    """

    phase: str
    sequence: int | None
    status: str | None
    last_updated: Any | None


def read_settings(engine: Any) -> dict[str, str]:
    """Return ``codeconv.workspace_settings`` as a flat ``{key: value}``.

    Byte-identical query to ``tools/init/workflow.py::_read_settings`` —
    the canonical existing read (D2: same SQL, not a new one).
    """
    with engine.connect() as conn:
        rows = conn.execute(
            text("SELECT key, value FROM codeconv.workspace_settings")
        ).all()
    return {k: v for k, v in rows}


def get_setting(engine: Any, key: str, default: str | None = None) -> str | None:
    """Convenience single-key lookup over :func:`read_settings`."""
    return read_settings(engine).get(key, default)


def read_excluded_directories(engine: Any) -> list[ExcludedDirectory]:
    """Return all ``codeconv.excluded_directories`` rows (path, kind).

    Same projection as ``tools/init/workflow.py`` (``SELECT path, kind
    FROM codeconv.excluded_directories``); ordered by ``path`` for a
    deterministic result.
    """
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT path, kind FROM codeconv.excluded_directories "
                "ORDER BY path"
            )
        ).all()
    return [ExcludedDirectory(path=p, kind=k) for p, k in rows]


def read_phases(engine: Any) -> list[PhaseEntry]:
    """Return the conversion phases joined across ``phase_sequence`` and
    ``phase_status``, ordered by ``sequence`` then ``phase``.

    A FULL OUTER JOIN so a phase present in only one table still appears
    (the two are seeded independently by ``codeconv init``); read-only,
    no mutation (D2).
    """
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                """
                SELECT
                    COALESCE(seq.phase, st.phase)  AS phase,
                    seq.sequence                   AS sequence,
                    st.status                      AS status,
                    st.last_updated                AS last_updated
                FROM codeconv.phase_sequence AS seq
                FULL OUTER JOIN codeconv.phase_status AS st
                    ON seq.phase = st.phase
                ORDER BY seq.sequence NULLS LAST,
                         COALESCE(seq.phase, st.phase)
                """
            )
        ).all()
    return [
        PhaseEntry(
            phase=r.phase,
            sequence=r.sequence,
            status=r.status,
            last_updated=r.last_updated,
        )
        for r in rows
    ]


def workspace_initialised(engine: Any) -> bool:
    """True iff a workspace has been configured (``init`` has run).

    Mirrors the existing tools' "is there a workspace?" check: a
    non-empty ``workspace_settings`` table. Read-only.
    """
    with engine.connect() as conn:
        n = conn.execute(
            text("SELECT count(*) FROM codeconv.workspace_settings")
        ).scalar()
    return bool(n and n > 0)


__all__ = [
    "ExcludedDirectory",
    "PhaseEntry",
    "get_setting",
    "read_excluded_directories",
    "read_phases",
    "read_settings",
    "workspace_initialised",
]
