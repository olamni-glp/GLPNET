"""convspec deterministic step + ingest (feature 018, T033/T034).

The convspec ``@DBOS.step`` body is **deterministic + replay-safe**
(contract ``dbos_workflow_model.md`` § needs_agent_work, R3): idiom-KB
lookup → if a valid agent-produced artifact is checked in, record the
two-phase completion and return ``specced``; else **return the
deterministic typed sentinel ``{"needs_agent_work": True, ...}``** — a
*successful* replay-safe step output, **never a raised exception**
(raising would be recorded by DBOS as a failed step and break the
normal first-time path). No LLM/web/network in this module — the
analysis + research sub-agents live in the ``/codeconv-convspec`` skill.

``run_convspec_ingest`` is the two-phase writer (``dart_convspecs``):
``convspec_started_at`` first; ``convspec_completed_at`` + ``spec_path``
ONLY on the terminal accept (FR-003 — a half-step is observably
incomplete). ``--respec`` re-opens a completed row on sha256 drift
(FR-019).
"""

from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Any, Optional

from sqlalchemy import text

from codeconv.tools.convspec import artefact as _art


def register(dbos_app: Any) -> Any:
    """Delegate to the shared durable activation (the convspec step is
    registered into ``durable`` via :mod:`codeconv.durable.steps`)."""
    from codeconv.durable import activate

    return activate(dbos_app)


def _sha256_of(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run_convspec_step(
    *,
    repo_root: Path,
    data_dir: Optional[Path],
    rel_path: str,
    respec: bool = False,
) -> dict:
    """Deterministic convspec step (replay-safe).

    Returns one of:
    - ``{"outcome": "specced", "path": rel}``           (artifact accepted)
    - ``{"needs_agent_work": True, "path": rel, ...}``   (no/invalid artifact)
    - ``{"outcome": "escalated", "path": rel, ...}``     (artifact has escalations)
    Never raises for the normal paths (R3).
    """
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    engine = build_engine(
        acquire_or_discover(repo_root, ready_timeout=60.0, data_dir=data_dir)
    )
    src = repo_root / rel_path
    sha = _sha256_of(src) if src.is_file() else None

    # Phase 1: mark started (idempotent INSERT … ON CONFLICT DO NOTHING).
    with engine.begin() as conn:
        conn.execute(
            text(
                "INSERT INTO codeconv.dart_convspecs "
                "(path, convspec_started_at, sha256_of_dart_at_spec_start) "
                "VALUES (:p, now(), :s) "
                "ON CONFLICT (path) DO NOTHING"
            ),
            {"p": rel_path, "s": sha},
        )
        # --respec: re-open a completed row on sha drift (FR-019).
        if respec:
            conn.execute(
                text(
                    "UPDATE codeconv.dart_convspecs "
                    "SET convspec_completed_at = NULL, spec_path = NULL, "
                    "    convspec_started_at = now(), "
                    "    sha256_of_dart_at_spec_start = :s "
                    "WHERE path = :p"
                ),
                {"p": rel_path, "s": sha},
            )

    art_path = _art.artifact_path(repo_root, rel_path)
    if not art_path.is_file():
        return {"needs_agent_work": True, "path": rel_path, "reason": "no artifact"}

    text_body = art_path.read_text(encoding="utf-8")
    errs = _art.validate_artifact(text_body, source_sha256=sha)
    if errs:
        return {
            "needs_agent_work": True,
            "path": rel_path,
            "reason": "artifact invalid",
            "errors": errs,
        }

    escalations = _art.open_escalations(text_body)
    spec_rel = str(art_path.relative_to(repo_root)).replace("\\", "/")

    # Phase 2 (terminal): record completion + spec_path + escalation
    # count. open_escalation_count > 0 ⇒ conversion blocked (NOT specing).
    with engine.begin() as conn:
        conn.execute(
            text(
                "UPDATE codeconv.dart_convspecs "
                "SET convspec_completed_at = now(), spec_path = :sp, "
                "    open_escalation_count = :n "
                "WHERE path = :p"
            ),
            {"p": rel_path, "sp": spec_rel, "n": len(escalations)},
        )

    if escalations:
        return {
            "outcome": "escalated",
            "path": rel_path,
            "escalations": escalations,
        }
    return {"outcome": "specced", "path": rel_path, "spec_path": spec_rel}


def run_convspec_ingest(
    repo_root: Path,
    *,
    rel_path: str,
    data_dir: Optional[Path] = None,
    respec: bool = False,
) -> dict:
    """CLI ``convspec ingest`` entrypoint — thin deterministic wrapper
    over :func:`run_convspec_step` (the same logic the DBOS step runs)."""
    return run_convspec_step(
        repo_root=Path(repo_root),
        data_dir=Path(data_dir) if data_dir else None,
        rel_path=rel_path,
        respec=respec,
    )


__all__ = ["register", "run_convspec_ingest", "run_convspec_step"]
