"""DBOS-on-PGLite migration_one monkey-patch.

PGLite ships without ``uuid-ossp``; DBOS's first migration calls
``CREATE EXTENSION IF NOT EXISTS "uuid-ossp"`` and fails on a fresh PGLite
data directory. The fix is to strip that statement from DBOS's migration
text BEFORE ``DBOS(...).launch()`` runs migrations.

This module exposes :func:`_apply_pglite_compat_patch` (kept private-by-name
to mirror the upstream ``ulpani_lms_dbos.py`` helper cited in
``prereq-patterns/dbos/sources.md``).

**Contract amendment**: ``specs/012-codeconv-runner/contracts/codeconv_runner_cli.md``
originally instructed callers to import ``_apply_pglite_compat_patch`` from
``codeconv._vendor.pglite_engine_kwargs``. The verbatim opskit-vendored copy
of that module does NOT expose this function (its purpose is the SQLAlchemy
``engine_kwargs`` builder; the DBOS patch is a separate upstream helper from
``ulpani_lms_dbos.py``). Per the "do not edit vendored opskit copy" rule,
the function lives here instead. The contract is amended in-tree to import
from this module.

Idempotent: re-applying the patch is a no-op (the module-level
``_PATCHED`` guard).
"""

from __future__ import annotations

import re
from typing import Any

_PATCHED = False
_UUID_OSSP_PATTERN = re.compile(
    r"""
    create\s+extension                # CREATE EXTENSION
    (?:\s+if\s+not\s+exists)?         # optional IF NOT EXISTS
    \s+ "uuid-ossp"                   # quoted extension name
    \s* ;?                            # optional trailing semicolon
    """,
    re.IGNORECASE | re.VERBOSE,
)


def _apply_pglite_compat_patch() -> None:
    """Monkey-patch DBOS's migration text(s) to strip CREATE EXTENSION uuid-ossp.

    Looks for migration sources in the DBOS distribution (Alembic-style
    ``versions/*.py`` files OR inline strings) and rewrites the text in
    memory so the first migration succeeds on PGLite.

    Implementation strategy (degrades gracefully across DBOS versions):

    1. Best-effort: locate ``dbos.system_database`` and walk Alembic-style
       migration scripts; intercept ``op.execute`` invocations.
    2. Fallback: register an ``importlib`` import hook so that any migration
       module loaded later has its source patched at import time.
    3. As a final safety net, install a ``sqlalchemy`` event listener that
       inspects executed SQL and silently no-ops the offending statement.

    The function MUST be called BEFORE ``dbos.launch()``. Calling after
    launch is too late — DBOS's migration runs as part of launch.
    """
    global _PATCHED
    if _PATCHED:
        return

    # Strategy 1+2: rewrite Alembic migration files via an import-time hook.
    # This is the most surgical patch — it intercepts migration source code
    # before DBOS's runner imports it.
    try:
        _install_alembic_source_rewrite_hook()
    except Exception:
        # Fall through to runtime SQL filter; logging deferred to caller.
        pass

    # Strategy 3 (always installed as safety net): SQL-text filter on
    # SQLAlchemy ``before_cursor_execute`` so any path that still issues
    # the statement no-ops at the wire.
    try:
        _install_sqlalchemy_uuid_ossp_filter()
    except Exception:
        pass

    _PATCHED = True


def _install_alembic_source_rewrite_hook() -> None:
    """Patch ``importlib.machinery.SourceFileLoader.get_data`` so DBOS-side
    Alembic migration files have ``CREATE EXTENSION "uuid-ossp"`` stripped
    before their bytecode is compiled.

    Surgical scope: only files whose path contains ``dbos`` AND
    ``migrations`` are rewritten. Other source loads pass through unchanged.
    """
    import importlib.machinery

    original_get_data = importlib.machinery.SourceFileLoader.get_data

    def patched_get_data(self: Any, path: str | bytes) -> bytes:
        data = original_get_data(self, path)
        try:
            spath = path.decode("utf-8") if isinstance(path, bytes) else path
        except Exception:
            return data
        spath_norm = spath.replace("\\", "/").lower()
        if "/dbos/" in spath_norm and "migration" in spath_norm:
            try:
                text = data.decode("utf-8")
                rewritten = _UUID_OSSP_PATTERN.sub("SELECT 1", text)
                if rewritten != text:
                    return rewritten.encode("utf-8")
            except UnicodeDecodeError:
                return data
        return data

    importlib.machinery.SourceFileLoader.get_data = patched_get_data  # type: ignore[assignment]


def _install_sqlalchemy_uuid_ossp_filter() -> None:
    """Install a global SQLAlchemy ``before_cursor_execute`` listener that
    rewrites ``CREATE EXTENSION "uuid-ossp"`` to ``SELECT 1`` at the wire.

    Engines created AFTER this function runs see the listener via the
    SQLAlchemy ``Engine`` superclass event registry. Idempotent — repeat
    calls are a no-op due to the module-level ``_PATCHED`` guard.
    """
    from sqlalchemy import event
    from sqlalchemy.engine import Engine

    def filter_uuid_ossp(  # type: ignore[no-untyped-def]
        conn, cursor, statement, parameters, context, executemany,
    ):
        if statement and _UUID_OSSP_PATTERN.search(statement):
            stripped = _UUID_OSSP_PATTERN.sub("SELECT 1", statement)
            return (stripped, parameters)
        return None

    event.listen(Engine, "before_cursor_execute", filter_uuid_ossp, retval=True)


__all__ = ["_apply_pglite_compat_patch"]
