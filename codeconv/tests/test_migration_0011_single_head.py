"""Feature 035 — migration 0011 stays an intact, non-head INTERIOR link.

When semantic-tombstone-enrichment added ``0011_enrich_provenance``
(revision ``0011``, down_revision ``0010``) it became the single head.
Feature 063 (``0012_msmesh_schema``) has since advanced the head to
``0012``, so ``0011`` is now an interior link, not the head. This test was
repurposed (the same convention applied to ``test_migration_0010_single_head``
when 035 landed): the AUTHORITATIVE single-head + linear-chain assertions
moved to ``test_migration_0012_single_head.py``; here we only assert that
``0011`` is preserved as a non-destructively-rewritten interior link
(Constitution VI-a — prior heads are never destructively rewritten) and
still revises ``0010``.
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


def test_0011_is_a_non_head_interior_link_offline() -> None:
    """``0011`` is no longer the head (063's ``0012`` is) but remains in the
    chain as ``0012``'s down_revision — a non-destructive interior link."""
    sd = _script_dir()
    heads = sd.get_heads()
    assert heads == ["0012"], f"expected single head 0012, got {heads}"
    assert "0011" not in heads
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}
    assert chain["0012"] == "0011", chain
    assert chain["0011"] == "0010", chain


def test_0011_revision_metadata_offline() -> None:
    """``0011`` revises ``0010`` (the documented down_revision; unchanged)."""
    sd = _script_dir()
    rev = sd.get_revision("0011")
    assert rev.down_revision == "0010", rev.down_revision
