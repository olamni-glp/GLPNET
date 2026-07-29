"""Feature 063 US2 — migration 0012 keeps the chain single-head & linear.

Durable first-hop mesh messaging appends ``0012_msmesh_schema`` (revision
``0012``, down_revision ``0011``) creating the ``msmesh`` schema and its six
domain tables. The runner MUST report exactly one head (``0012``); no
branch / multi-head.

This is now the AUTHORITATIVE single-head + linear-chain assertion family
(Constitution VI-a is machine-checkable — the head is asserted by this
``test_migration_*_single_head.py`` test, NOT by counting ``versions/``
files). Mirrors ``test_migration_0011_single_head.py`` extended one
revision: the offline assertions are authoritative (Alembic
``ScriptDirectory``); the bridge-gated test confirms a live ``codeconv
migrate`` reaches + idempotently re-applies head ``0012`` (all DDL ``IF NOT
EXISTS``).
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
    """Authoritative, bridge-free: the script graph has ONE head 0012 (no branch)."""
    sd = _script_dir()
    heads = sd.get_heads()
    assert heads == ["0012"], f"expected single head 0012, got {heads}"


def test_linear_chain_through_0012_offline() -> None:
    """The chain stays strictly linear 0001→…→0012 (no branch/merge),
    so ``alembic upgrade head`` is unambiguous."""
    sd = _script_dir()
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}
    assert chain["0012"] == "0011" and chain["0011"] == "0010", chain
    assert chain == {
        "0012": "0011",
        "0011": "0010",
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


def test_0012_revision_metadata_offline() -> None:
    """``0012`` revises ``0011`` (the documented down_revision)."""
    sd = _script_dir()
    rev = sd.get_revision("0012")
    assert rev.down_revision == "0011", rev.down_revision


@needs_bridge
def test_upgrade_head_reaches_0012_and_idempotent(discover_repo: Path) -> None:
    """Fresh cluster: ``codeconv migrate`` exits 0 (reaches head 0012),
    and a second run is a clean no-op (CREATE ... IF NOT EXISTS)."""
    p1 = run_codeconv(discover_repo, "migrate", timeout=240.0)
    assert p1.returncode == 0, f"first migrate failed: {p1.stderr}"
    p2 = run_codeconv(discover_repo, "migrate", timeout=240.0)
    assert p2.returncode == 0, f"re-migrate not idempotent: {p2.stderr}"
