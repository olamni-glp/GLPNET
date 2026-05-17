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
    pre_launch: Optional[Any] = None,
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
    # PGLite has only the ``postgres`` database; DBOS's default behaviour
    # of creating a sibling ``<dbname>_dbos_sys`` database fails. Point
    # both DBOS roles (system + application) at the same ``postgres`` DB,
    # and isolate DBOS's tables in the ``dbos`` schema (FR-015).
    #
    # Pool sizing: ``pglite_engine_kwargs`` defaults to ``pool_size=1`` —
    # safe for codeconv's own sequential migrations but DEADLOCKS DBOS,
    # whose ``run_migrations`` holds one connection across an inner
    # ``ensure_dbos_schema`` call that needs a second. Override to >= 5
    # for both DBOS engines. The bridge's ``globalWorkChain`` still
    # serialises queries on the PGLite side (FR-005 invariant preserved);
    # SQLAlchemy multi-connection only buys concurrency at the client.
    dbos_kwargs = pglite_engine_kwargs(application_name=application_name)
    dbos_kwargs["pool_size"] = 5
    dbos_kwargs["max_overflow"] = 5
    cfg_kwargs = dict(
        name=application_name,
        database_url=url,
        application_database_url=url,
        system_database_url=url,
        sys_db_pool_size=5,
        dbos_system_schema="dbos",
        db_engine_kwargs=dbos_kwargs,
        # PGLite does not support PostgreSQL's NOTIFY mechanism end-to-end;
        # disable LISTEN/NOTIFY-based polling.
        use_listen_notify=False,
    )
    config = DBOSConfig(**cfg_kwargs)
    dbos = DBOS(config=config)
    # Feature-018 (additive, D2-safe): a ``pre_launch`` hook runs AFTER
    # construction but BEFORE ``launch()`` so durable workflows/steps are
    # registered while DBOS can still wire them into the executor for
    # recovery. ``migrate`` (and every pre-018 caller) passes nothing →
    # identical behaviour. Failure here is fatal (no silent half-launch).
    if pre_launch is not None:
        pre_launch(dbos)
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
