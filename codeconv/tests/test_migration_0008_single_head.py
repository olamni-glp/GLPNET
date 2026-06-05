"""T006 — migration 0008 keeps the chain single-head & linear.

Feature 020 (codeconv-equiv) appends ``0008_equivalence`` (revision
``0008``, down_revision ``0007``) carrying the ``codeconv.dart_equivalence``
DDL. Per ``contracts/equiv_schema.md`` § Migration linearization: the
runner MUST report exactly one head (``0008``); no branch / multi-head.
This mirrors ``test_migration_0007_single_head.py`` (feature 019) extended
one revision: the offline assertions are authoritative (Alembic
``ScriptDirectory``); the bridge-gated test confirms a live ``codeconv
migrate`` reaches + idempotently re-applies head 0008.
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


def test_exactly_one_head_offline() -> None:
    """Authoritative, bridge-free: the script graph has ONE head.

    Stage 4 appends ``0009_no_emit`` (revision 0009, down_revision 0008),
    so the single head advanced 0008 → 0009. (test_migration_0009_single_head
    owns the 0009 assertions; here we just track the current head.)"""
    sd = _script_dir()
    heads = sd.get_heads()
    assert heads == ["0010"], f"expected single head 0010, got {heads}"


def test_linear_chain_through_0008_offline() -> None:
    """The chain stays strictly linear 0001→…→0010 (no branch/merge),
    so ``alembic upgrade head`` is unambiguous."""
    sd = _script_dir()
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}
    assert chain == {
        "0010": "0009",
        "0009": "0008",
        "0008": "0007",
        "0007": "0006",
        "0006": "0005",
        "0005": "0004",
        "0004": "0003",
        "0003": "0002",
        "0002": "0001",
        "0001": None,
    }, chain


def test_0008_revision_metadata_offline() -> None:
    """``0008`` revises ``0007`` (the documented down_revision)."""
    sd = _script_dir()
    rev = sd.get_revision("0008")
    assert rev.down_revision == "0007", rev.down_revision


@needs_bridge
def test_upgrade_head_reaches_0008_and_idempotent(discover_repo: Path) -> None:
    """Fresh cluster: ``codeconv migrate`` exits 0 (reaches head 0008),
    and a second run is a clean no-op (CREATE TABLE IF NOT EXISTS)."""
    p1 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p1.returncode == 0, f"first migrate failed: {p1.stderr}"
    p2 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p2.returncode == 0, f"re-migrate not idempotent: {p2.stderr}"
