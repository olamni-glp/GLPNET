"""Feature 024 — migration 0010 stays an intact, non-head INTERIOR link.

When the marathon stage-harness added ``0010_marathon_schema`` (revision
``0010``, down_revision ``0009``) it became the single head. Feature 035
(``0011_enrich_provenance``) has since advanced the head to ``0011``, so
``0010`` is now an interior link, not the head. This test was repurposed
(T005 / analyze D1): the AUTHORITATIVE single-head + linear-chain
assertions moved to ``test_migration_0011_single_head.py``; here we only
assert that ``0010`` is preserved as a non-destructively-rewritten interior
link (Constitution VI-a — prior heads are never destructively rewritten)
and still revises ``0009``.
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


def test_0010_is_a_non_head_interior_link_offline() -> None:
    """``0010`` is no longer the head (035's ``0011`` is) but remains in the
    chain as ``0011``'s down_revision — a non-destructive interior link."""
    sd = _script_dir()
    heads = sd.get_heads()
    assert heads == ["0011"], f"expected single head 0011, got {heads}"
    assert "0010" not in heads
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}
    assert chain["0011"] == "0010", chain
    assert chain["0010"] == "0009", chain


def test_0010_revision_metadata_offline() -> None:
    """``0010`` revises ``0009`` (the documented down_revision; unchanged)."""
    sd = _script_dir()
    rev = sd.get_revision("0010")
    assert rev.down_revision == "0009", rev.down_revision
