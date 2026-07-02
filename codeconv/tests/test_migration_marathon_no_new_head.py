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


def test_marathon_0010_remains_an_intact_interior_link() -> None:
    """024's ``0010_marathon_schema`` is preserved non-destructively (VI-a:
    prior heads are never rewritten) and still revises ``0009``.

    030 added NO shared-cluster migration; later features legitimately
    advance the Alembic head (035's ``0011_enrich_provenance`` is the head
    now), so this assertion is decoupled from the absolute head number and
    only verifies 024's marathon link is intact — what 030's invariant is
    actually about. The current-head assertion lives in
    ``test_migration_0011_single_head.py``."""
    sd = _script_dir()
    assert "0010" not in sd.get_heads(), "0010 should be an interior link, not head"
    rev = sd.get_revision("0010")
    assert rev.down_revision == "0009", rev.down_revision


def test_no_marathon_version_file_beyond_0010() -> None:
    """030/marathon added no shared-cluster migration: the ONLY ``marathon``-
    named ``versions/`` file is 024's ``0010_marathon_schema`` — none beyond
    0010. (A non-marathon migration beyond 0010, e.g. 035's ``0011_enrich_
    provenance``, is permitted — it is not a marathon migration.)"""
    marathon_prefixes = sorted(
        int(m.group(1))
        for f in (_migrations_dir() / "versions").glob("*.py")
        if "marathon" in f.name.lower() and (m := re.match(r"^(\d{4})_", f.name))
    )
    assert marathon_prefixes == [10], (
        f"a marathon migration beyond 0010 appeared (030/VI-a, D2): "
        f"{marathon_prefixes}"
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
