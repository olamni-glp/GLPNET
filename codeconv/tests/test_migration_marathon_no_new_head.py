"""Feature 030 — the refined marathon harness adds NO shared-cluster
migration (T053; Constitution VI-a, D2).

030's per-run isolated store creates its schema via
``codeconv.marathon.store.schema.ensure_schema`` against the run's own
PGLite cluster — never via Alembic against the shared repo cluster. The
shared-repo head therefore stays exactly ``0010`` (024's
``0010_marathon_schema``, now inert history). Offline, bridge-free,
authoritative (Alembic ``ScriptDirectory``).
"""

from __future__ import annotations

import re
from pathlib import Path


def _migrations_dir() -> Path:
    import codeconv.cli as _cli

    return Path(_cli.__file__).parent / "db" / "migrations"


def _script_dir():
    from alembic.config import Config
    from alembic.script import ScriptDirectory

    cfg = Config()
    cfg.set_main_option("script_location", str(_migrations_dir().resolve()))
    return ScriptDirectory.from_config(cfg)


def test_shared_repo_head_is_still_0010() -> None:
    """030 ships with the head 024 left: exactly one head, ``0010``."""
    heads = _script_dir().get_heads()
    assert heads == ["0010"], (
        f"expected the shared-repo Alembic head to remain 0010 — feature 030 "
        f"must not add a shared-cluster migration (VI-a, D2); got {heads}"
    )


def test_no_version_file_beyond_0010() -> None:
    """A branch can hide behind an unchanged head — no versions/ file may
    carry a numeric prefix beyond 0010 at all."""
    prefixes = sorted(
        int(m.group(1))
        for f in (_migrations_dir() / "versions").glob("*.py")
        if (m := re.match(r"^(\d{4})_", f.name))
    )
    assert prefixes and max(prefixes) == 10, (
        f"versions/ contains a migration beyond 0010: {prefixes}"
    )


def test_only_marathon_migration_is_inert_024_history() -> None:
    """The single marathon-named migration is 024's ``0010_marathon_schema``
    (inert history, VIII); 030's store schema is not Alembic-managed."""
    marathon_files = sorted(
        f.name
        for f in (_migrations_dir() / "versions").glob("*.py")
        if "marathon" in f.name.lower()
    )
    assert marathon_files == ["0010_marathon_schema.py"], marathon_files


def test_per_run_store_schema_is_not_alembic_managed() -> None:
    """030's DDL path is ``ensure_schema`` on the per-run store — the module
    must not reference Alembic (the shared-cluster migration machinery)."""
    import inspect

    from codeconv.marathon.store import schema

    assert callable(schema.ensure_schema)
    source = inspect.getsource(schema)
    # Prose may *mention* Alembic (the docstring says "NOT the shared-repo
    # Alembic chain") — what must never appear is the machinery itself.
    assert not re.search(r"^\s*(import alembic|from alembic)", source, re.M), (
        "marathon per-run store schema must not go through Alembic (VI-a, D2)"
    )
