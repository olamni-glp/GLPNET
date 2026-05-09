# Vendored from D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_compat_loaders.py; do not edit locally.

"""psycopg type-loader compatibility shims for PGLite WASM.

PGLite WASM (verified up to ``@electric-sql/pglite@0.3.x``) emits
wire-format responses for some Postgres types that crash psycopg 3's
built-in loaders natively — Windows fatal exception, access violation
inside ``psycopg._cursor_base._select_current_result``. The pattern is:
the wire bytes are *valid* Postgres text output, but trip a bug in
psycopg's parser that does not surface on real Postgres servers.

The reproduction harness lives at
``scripts/repro/004a_psycopg_crash/`` (variants V10, V13, V15, V18).
V18 in particular shows that PGLite emits text timestamptz of the
shape ``'2026-05-09 12:44:46.707+00'`` (abbreviated tz hour-only), and
that swapping the OID-1184 loader on the connection eliminates the
crash entirely, including through SQLAlchemy and the ORM ``select()``
path.

This module is the **substrate fix** that all PGLite consumers in this
repo MUST install on every connection. The fix is wired automatically
by :func:`apply_to_engine`, which registers a SQLAlchemy ``connect``
event listener that patches the psycopg adapters map on every newly-
checked-out connection. Raw-psycopg consumers can call
:func:`register_pglite_compat_loaders` directly on
``connection.adapters``.

OIDs currently patched:
    1184  timestamptz  (TIMESTAMP WITH TIME ZONE)

Add new OIDs here only after demonstrating the crash with a fresh
harness variant, so the catalogue of "what PGLite gets wrong"
stays grounded in evidence rather than precaution.

This file is part of the PGlite prereq-pattern substrate. Per
``prereq-patterns/pglite/applicability.md``, sibling features that
vendor the bridge MUST also vendor this loader module — the canonical
copy is ``src/opskit/_vendor/pglite_compat_loaders.py`` and the
sibling copy is ``src/breenlake/_vendor/pglite_compat_loaders.py``.
Drift between the two copies is permitted only via reviewed PR.
"""

from __future__ import annotations

from datetime import datetime
from typing import Any

from psycopg.adapt import Loader

TIMESTAMPTZ_OID = 1184
TIMESTAMP_OID = 1114


def _parse_pg_timestamp(s: str | bytes | bytearray | memoryview) -> datetime:
    """Parse Postgres text-format timestamp/timestamptz to ``datetime``.

    Handles PGLite's abbreviated timezone (``+00``) and full forms
    (``+00:00``, ``+0530``). Falls back to a naive datetime for
    timestamp-without-tz columns.
    """
    if isinstance(s, (bytes, bytearray, memoryview)):
        s = bytes(s).decode("utf-8")
    s = s.replace(" ", "T", 1)
    # Normalize trailing tz forms to ISO-8601 ``+HH:MM`` / ``-HH:MM``.
    # Cases:
    #   ``...+00``    -> ``...+00:00``
    #   ``...+0530``  -> ``...+05:30``
    #   ``...+05:30`` (already canonical) -> unchanged
    #   ``...Z``      -> unchanged (fromisoformat handles it on 3.11+)
    if len(s) >= 3 and s[-3] in ("+", "-") and s[-2:].isdigit():
        s = s + ":00"
    elif (
        len(s) >= 5
        and s[-5] in ("+", "-")
        and s[-4:].isdigit()
    ):
        s = s[:-2] + ":" + s[-2:]
    return datetime.fromisoformat(s)


class PgliteTimestamptzTextLoader(Loader):
    """Safe text loader for ``timestamptz`` (OID 1184).

    Replaces psycopg 3's built-in OID-1184 text loader, which crashes
    natively when fed PGLite WASM's wire output (see module docstring).
    """

    def load(self, data: Any) -> datetime | None:
        if data is None:
            return None
        return _parse_pg_timestamp(data)


class PgliteTimestampTextLoader(Loader):
    """Safe text loader for ``timestamp`` (OID 1114, no tz).

    Defensive — applied alongside the timestamptz loader because
    naive-timestamp columns will hit the same parser code path on
    psycopg's side and likely have the same fragility. Cheaper to
    install once than to debug it later.
    """

    def load(self, data: Any) -> datetime | None:
        if data is None:
            return None
        return _parse_pg_timestamp(data)


def register_pglite_compat_loaders(adapters: Any) -> None:
    """Register the safe loaders on a psycopg ``AdaptersMap``.

    Call this on the raw psycopg connection (``conn.adapters``) for
    direct-psycopg consumers. SQLAlchemy consumers should use
    :func:`apply_to_engine` instead.
    """
    adapters.register_loader(TIMESTAMPTZ_OID, PgliteTimestamptzTextLoader)
    adapters.register_loader(TIMESTAMP_OID, PgliteTimestampTextLoader)


def apply_to_engine(engine: Any) -> None:
    """Wire a SQLAlchemy connect-event listener that installs the
    safe loaders on every newly-checked-out psycopg connection.

    Idempotent: safe to call repeatedly on the same engine — duplicate
    registrations are filtered by ``event.contains``.
    """
    from sqlalchemy import event

    if event.contains(engine, "connect", _on_psycopg_connect):
        return
    event.listen(engine, "connect", _on_psycopg_connect)


def _on_psycopg_connect(dbapi_conn: Any, conn_record: Any) -> None:
    """SQLAlchemy ``connect`` event handler — kept module-level so the
    ``event.contains`` idempotency check has a stable identity to test.
    """
    # The dbapi_conn is the raw ``psycopg.Connection`` for psycopg 3.
    # Real PG can use this same fix without harm — the loaders return
    # the same ``datetime`` shape.
    register_pglite_compat_loaders(dbapi_conn.adapters)


__all__ = [
    "TIMESTAMPTZ_OID",
    "TIMESTAMP_OID",
    "PgliteTimestamptzTextLoader",
    "PgliteTimestampTextLoader",
    "register_pglite_compat_loaders",
    "apply_to_engine",
]
