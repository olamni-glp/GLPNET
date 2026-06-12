"""Polish — feature 030 adds NO shared-cluster migration (T053).

Constitution VI-a / D2: the refined harness keeps its state in a per-run
isolated store provisioned by ``marathon/store/schema.py:ensure_schema``
(plain idempotent DDL), never by a shared-repo Alembic migration. The 024
schema (migration ``0010``) is inert history. So the shared-repo Alembic
head MUST still be exactly ``0010`` — no ``0011``, no branch — and the
marathon package must not grow an Alembic dependency.

Bridge-free and authoritative (Alembic ``ScriptDirectory`` over the checked-in
scripts); the live-upgrade behaviour is already covered by
``test_migration_0010_single_head.py``.
"""

from __future__ import annotations

from pathlib import Path


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


def test_head_is_still_0010() -> None:
    """The refined harness added no new head: still exactly ``0010``."""
    heads = _script_dir().get_heads()
    assert heads == ["0010"], f"feature 030 must add no migration; head: {heads}"


def test_no_revision_beyond_0010() -> None:
    """The revision set is exactly 0001…0010 — nothing was appended."""
    revisions = sorted(r.revision for r in _script_dir().walk_revisions())
    assert revisions == [f"{n:04d}" for n in range(1, 11)], revisions


def test_marathon_package_is_alembic_free() -> None:
    """The per-run store is provisioned by ``ensure_schema`` (idempotent DDL),
    not Alembic: no marathon module imports alembic, and the migrations tree
    contains no marathon-owned addition beyond the inert 024 ``0010``."""
    import codeconv.marathon as marathon_pkg

    pkg_dir = Path(marathon_pkg.__file__).parent
    for src in pkg_dir.rglob("*.py"):
        text = src.read_text(encoding="utf-8")
        assert "alembic" not in text, f"alembic reference in {src.name}"

    import codeconv.cli as _cli

    versions = Path(_cli.__file__).parent / "db" / "migrations" / "versions"
    marathon_files = [
        p.name for p in versions.glob("*.py")
        if "marathon" in p.name and p.name != "0010_marathon_schema.py"
    ]
    assert marathon_files == [], marathon_files
