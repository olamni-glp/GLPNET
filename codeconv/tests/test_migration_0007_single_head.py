"""T005 — migration 0007 keeps the chain single-head & linear.

Feature 019 (codeconv-codegen) appends ``0007_codegen`` (revision
``0007``, down_revision ``0006``) carrying the ``codeconv.dart_codegen``
DDL. Per ``contracts/codegen_schema.md`` § Invariants: ``alembic upgrade
head`` MUST reach exactly one head (``0007``); no dup/multi-head. This
mirrors ``test_migration_single_head.py`` (feature 018) extended one
revision: the offline assertions are authoritative (Alembic
``ScriptDirectory``); the bridge-gated test confirms a live ``codeconv
migrate`` reaches + idempotently re-applies head 0007.
"""

from __future__ import annotations

from pathlib import Path

from .conftest import needs_bridge, run_codeconv


def _script_dir():
    from alembic.config import Config
    from alembic.script import ScriptDirectory

    import codeconv.cli as _cli

    here = Path(_cli.__file__).parent
    cfg = Config()
    cfg.set_main_option(
        "script_location", str((here / "db" / "migrations").resolve())
    )
    return ScriptDirectory.from_config(cfg)


def test_0007_is_interior_not_head_offline() -> None:
    """Bridge-free: after feature 020 appended ``0008``, ``0007`` is an
    INTERIOR node — present in the graph but no longer a head. (The
    authoritative single-head assertion now lives in
    ``test_migration_single_head`` / ``test_migration_0008_single_head``.)"""
    sd = _script_dir()
    assert "0007" not in sd.get_heads(), sd.get_heads()
    assert "0007" in {r.revision for r in sd.walk_revisions()}


def test_0007_revision_metadata_offline() -> None:
    """``0007`` revises ``0006`` (the documented down_revision) and is
    itself revised by ``0008`` — the linkage feature 019 introduced is
    intact under the 020 append."""
    sd = _script_dir()
    rev = sd.get_revision("0007")
    assert rev.down_revision == "0006", rev.down_revision
    assert sd.get_revision("0008").down_revision == "0007"


@needs_bridge
def test_upgrade_head_reaches_0007_and_idempotent(discover_repo: Path) -> None:
    """Fresh cluster: ``codeconv migrate`` exits 0 (reaches head 0007),
    and a second run is a clean no-op (CREATE TABLE IF NOT EXISTS)."""
    p1 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p1.returncode == 0, f"first migrate failed: {p1.stderr}"
    p2 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p2.returncode == 0, f"re-migrate not idempotent: {p2.stderr}"
