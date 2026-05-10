"""SQLAlchemy + DBOS engine factories for the unified PGLite bridge.

Per ``specs/012-codeconv-runner/contracts/codeconv_runner_cli.md``
("Engine + connection sequence") the runner builds two engines:

1. A plain SQLAlchemy engine for codeconv's own ORM / Alembic use,
   patched with :func:`apply_to_engine` so ``timestamptz`` reads do not
   crash psycopg.
2. A DBOS instance configured for PGLite (``schema='dbos'``,
   ``db_engine_kwargs=pglite_engine_kwargs(...)``); after
   ``dbos.launch()`` the DBOS-managed engine is also patched.

The strict ordering — ``_apply_pglite_compat_patch()`` BEFORE
``dbos.launch()``, ``apply_to_engine()`` AFTER — is enforced by both
helper functions returning the ready-to-use object.
"""

from __future__ import annotations

from typing import Any, Optional

from sqlalchemy import create_engine
from sqlalchemy.engine import Engine

from codeconv._vendor.pglite_compat_loaders import apply_to_engine
from codeconv._vendor.pglite_engine_kwargs import pglite_engine_kwargs
from codeconv._vendor.dbos_pglite_patch import _apply_pglite_compat_patch
from codeconv.bridge_client import BridgeEndpoint


def build_url(endpoint: BridgeEndpoint) -> str:
    """Compose the psycopg-URL for the bridge endpoint."""
    return (
        f"postgresql+psycopg://postgres:postgres@"
        f"{endpoint.host}:{endpoint.port}/postgres"
    )


def build_engine(
    endpoint: BridgeEndpoint,
    *,
    application_name: str = "codeconv",
) -> Engine:
    """Construct a SQLAlchemy engine pointed at the unified bridge.

    Wires :func:`apply_to_engine` so newly checked-out connections install
    the safe psycopg loaders (substrate fix for the PGLite + psycopg
    timestamptz crash, see ``prereq-patterns/pglite/applicability.md``
    § psycopg type-loader patch).
    """
    url = build_url(endpoint)
    engine = create_engine(
        url,
        **pglite_engine_kwargs(application_name=application_name),
    )
    apply_to_engine(engine)
    return engine


def setup_dbos(
    endpoint: BridgeEndpoint,
    *,
    application_name: str = "codeconv",
) -> Any:
    """Initialise DBOS against the unified bridge.

    Strict order enforced by this function:

    1. ``_apply_pglite_compat_patch()`` — strips
       ``CREATE EXTENSION "uuid-ossp"`` from DBOS's migration text so
       startup against a fresh PGLite cluster does not fail.
    2. Construct ``DBOS(config=DBOSConfig(...))`` with
       ``schema='dbos'``, ``db_engine_kwargs=pglite_engine_kwargs(...)``.
    3. ``dbos.launch()``.
    4. ``apply_to_engine(dbos.app_db.engine)`` — installs the safe
       loaders on the DBOS-managed engine too.

    Returns the launched DBOS instance.
    """
    # 1. Patch BEFORE launch.
    _apply_pglite_compat_patch()

    # 2 + 3. Construct + launch.
    from dbos import DBOS, DBOSConfig  # imported lazily; heavy

    url = build_url(endpoint)
    cfg_kwargs = dict(
        database_url=url,
        db_engine_kwargs=pglite_engine_kwargs(application_name=application_name),
    )
    # ``schema`` is supported on DBOS >= 0.6; pass it iff DBOSConfig accepts.
    try:
        config = DBOSConfig(schema="dbos", **cfg_kwargs)
    except TypeError:
        config = DBOSConfig(**cfg_kwargs)
    dbos = DBOS(config=config)
    dbos.launch()

    # 4. Patch DBOS-side engine AFTER launch (it doesn't exist before).
    app_engine: Optional[Engine] = None
    try:
        app_engine = dbos.app_db.engine  # type: ignore[attr-defined]
    except Exception:
        app_engine = None
    if app_engine is not None:
        apply_to_engine(app_engine)

    return dbos


__all__ = ["build_engine", "build_url", "setup_dbos"]
