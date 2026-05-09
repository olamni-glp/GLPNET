# Vendored from D:/BREENDEV/aigrid/AWS-Infra/src/opskit/_vendor/pglite_engine_kwargs.py; do not edit locally.

"""PGlite-compat kwargs for SQLAlchemy / psycopg consumers (opskit copy).

Verbatim function copy of `pglite_engine_kwargs(...)` from
`src/breenlake/_vendor/pglite_engine_kwargs.py`, itself adapted from
`D:/BSTDEV/lang/hatzinor_ai-ddp/src/ulpani_lms_pglite_compat.py`.

The upstream rationale (single-session WASM PGlite ⇒ QueuePool size=1
+ disabled prepared-statement cache) applies to opskit unchanged. opskit
uses its own repo-local PGLite at ``<repo>/.opskit-pglite/`` (distinct
from breenlake's host-wide PGLite); the dual-PGLite topology rationale
is in ``research.md`` Decision 3.

opskit defaults ``application_name='opskit'`` so connections show up
distinguishably in PG's ``pg_stat_activity``; callers may override
(e.g. ``application_name='opskit-migrate'`` for Alembic).

Per PR-001's "copy don't import" rule, this file MUST NOT import from
``src.breenlake._vendor``: drift between the two copies is permitted
only via reviewed PR.
"""

from __future__ import annotations

from typing import Any


def pglite_engine_kwargs(
    *,
    application_name: str = "opskit",
    pool_timeout: int = 300,
    extra_connect_args: dict[str, Any] | None = None,
) -> dict[str, Any]:
    connect_args: dict[str, Any] = {
        "application_name": application_name,
        "prepare_threshold": None,
    }
    if extra_connect_args:
        connect_args.update(extra_connect_args)
    return {
        "connect_args": connect_args,
        "pool_size": 1,
        "max_overflow": 0,
        "pool_timeout": pool_timeout,
        "pool_pre_ping": False,
    }


__all__ = ["pglite_engine_kwargs"]
