"""Alembic environment for codeconv migrations against the unified PGLite bridge.

Per ``prereq-patterns/pglite/applicability.md`` § Alembic, Alembic engines
running against PGLite MUST use ``NullPool`` + ``AUTOCOMMIT`` and disable
prepared-statement caching. Default engine config will deadlock on a PGLite
session because PGLite is single-session WASM and Alembic's default
DDL-in-a-transaction wrapping interacts badly with that session model.

The Alembic invocation flow is:

1. The runner (or the operator's ``codeconv migrate`` command) constructs
   the unified-bridge URL via :mod:`codeconv.db.engine` and passes it via
   ``alembic.config.Config.set_main_option('sqlalchemy.url', ...)``.
2. This env reads that URL, builds an engine with the PGLite-compatible
   shape, and runs migrations in online mode.
"""

from __future__ import annotations

from logging.config import fileConfig

from alembic import context
from sqlalchemy import create_engine
from sqlalchemy.pool import NullPool

from codeconv._vendor.pglite_compat_loaders import apply_to_engine

# this is the Alembic Config object, which provides access to the values
# within the .ini file in use.
config = context.config

if config.config_file_name is not None:
    try:
        fileConfig(config.config_file_name)
    except Exception:
        # alembic.ini may be absent in programmatic invocations; that's fine.
        pass

# No declarative metadata yet — initial migration is hand-written DDL.
# Tools that introduce ORM models in later phases may set this.
target_metadata = None


def _build_engine(url: str):
    """Per applicability.md § Alembic: NullPool + AUTOCOMMIT, prepare_threshold=None."""
    engine = create_engine(
        url,
        poolclass=NullPool,
        isolation_level="AUTOCOMMIT",
        connect_args={
            "prepare_threshold": None,
            "application_name": "alembic",
        },
    )
    apply_to_engine(engine)
    return engine


def run_migrations_offline() -> None:
    """Run migrations in 'offline' mode (emits SQL to a file)."""
    url = config.get_main_option("sqlalchemy.url")
    context.configure(
        url=url,
        target_metadata=target_metadata,
        literal_binds=True,
        dialect_opts={"paramstyle": "named"},
    )
    with context.begin_transaction():
        context.run_migrations()


def run_migrations_online() -> None:
    """Run migrations in 'online' mode against the unified PGLite bridge."""
    url = config.get_main_option("sqlalchemy.url")
    if not url:
        raise RuntimeError(
            "Alembic env requires sqlalchemy.url to be set "
            "(usually by codeconv.cli before calling alembic upgrade)."
        )
    engine = _build_engine(url)
    with engine.connect() as connection:
        context.configure(
            connection=connection,
            target_metadata=target_metadata,
        )
        with context.begin_transaction():
            context.run_migrations()


if context.is_offline_mode():
    run_migrations_offline()
else:
    run_migrations_online()
