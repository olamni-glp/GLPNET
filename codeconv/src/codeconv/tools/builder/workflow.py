"""codeconv-builder workflow registration + in-process DBOS bootstrap.

Feature 018, T021 + the T054-gate integration fix.

Unlike the six pre-existing tools (whose ``register()`` was a no-op
until T015 delegated it to :func:`codeconv.durable.activate`), the
builder *drives* the durable layer. Two responsibilities:

- ``register(dbos_app)`` — the feature-012 auto-discovery hook;
  delegates to the shared :func:`codeconv.durable.activate`.
- ``bootstrap_dbos(repo_root, data_dir)`` — launches DBOS **in the
  builder's own process** with the durable workflows/steps decorated
  **before** ``dbos.launch()`` (via ``setup_dbos``'s additive
  ``pre_launch`` hook), so the executor can recover them. ``migrate``
  launches DBOS in a *different* process, so a fresh ``builder run``
  must launch its own — this was the integration gap the T054 gate
  surfaced. Behaviour of every wrapped 015/016/017 entrypoint is
  unchanged (D2 hard gate) — the builder only orchestrates them.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any, Optional


def register(dbos_app: Any) -> Any:
    """Feature-012 auto-discovery hook → shared durable activation
    (idempotent; all tools delegate here = one activation/process)."""
    from codeconv.durable import activate

    return activate(dbos_app)


def bootstrap_dbos(
    repo_root: Path, data_dir: Optional[Path]
) -> dict:
    """Ensure DBOS is launched IN THIS PROCESS with the durable
    workflows registered pre-launch; return the bound
    ``{"steps":{...}, "workflows":{"outer":..,"child":..}}``.

    Idempotent: if a DBOS singleton already exists (e.g. tests that
    launched via ``migrate`` in-process), reuse it and just (re)activate
    (cached). Otherwise acquire the shared bridge, ``setup_dbos`` with
    ``pre_launch=activate`` (decorate-before-launch — the recovery
    correctness fix), and install the singleton.
    """
    from codeconv.durable import activate
    from codeconv.runner import get_dbos, set_dbos

    try:
        existing = get_dbos()
    except RuntimeError:
        existing = None

    if existing is not None:
        return activate(existing)

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import setup_dbos

    endpoint = acquire_or_discover(
        repo_root, ready_timeout=60.0, data_dir=data_dir
    )
    # pre_launch=activate ⇒ durable steps/workflows are decorated on the
    # DBOS instance BEFORE launch() so DBOS can wire them for recovery
    # (the T054-gate fix). activate() caches; the post-launch call below
    # returns the same bound handles.
    dbos = setup_dbos(endpoint, pre_launch=activate)
    set_dbos(dbos)

    # R12 step-transaction fix (option #1): pre-warm the process-cached
    # engine HERE — outside any DBOS step's transaction context — so the
    # one-time SQLAlchemy dialect-init / psycopg pg_type introspection
    # (which issues a ROLLBACK the single-writer PGLite bridge forbids
    # mid-Transaction) completes now. Every later build_engine() call
    # inside a DBOS step returns this same warmed engine, so no
    # fresh-engine first-connect rollback ever lands inside a DBOS
    # transaction. One throwaway round-trip; failure is non-fatal
    # (the steps would surface any real bridge problem themselves).
    try:
        from sqlalchemy import text as _text

        from codeconv.db.engine import build_engine

        _eng = build_engine(endpoint)
        with _eng.connect() as _c:
            _c.execute(_text("SELECT 1"))
    except Exception:
        pass

    return activate(dbos)


__all__ = ["bootstrap_dbos", "register"]
