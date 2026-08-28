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
    """Authoritative, bridge-free: the script graph has exactly ONE head.

    🔴 THE ASSERTION IS STRUCTURAL, NOT A HARD-CODED REVISION ID. It used to read
    ``assert heads == ["0010"]``. That is a restatement of "the newest migration is
    the one that existed when this test was written", so EVERY later migration broke
    it — and it did break: revisions 0011 and 0012 landed and both of this module's
    graph tests failed unconditionally, on every run, in a suite whose whole job is
    to be believed. A test that must be edited by each unrelated change is not
    protecting the invariant, it is reporting the calendar. The invariant this file
    owns is *single head, no branch*, and that is what is asserted now.
    """
    sd = _script_dir()
    heads = sd.get_heads()
    assert len(heads) == 1, f"expected exactly ONE head (no branch/merge), got {heads}"


def test_linear_chain_through_0008_offline() -> None:
    """The whole chain is strictly linear (no branch, no merge), and it passes
    through ``0008``, so ``alembic upgrade head`` is unambiguous.

    Linearity is checked as a property — one root, one head, every other revision
    with exactly one parent and one child — rather than by naming every revision,
    for the reason given in ``test_exactly_one_head_offline``."""
    sd = _script_dir()
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}

    branching = {rev: down for rev, down in chain.items() if isinstance(down, tuple)}
    assert not branching, f"merge revision(s) present, chain is not linear: {branching}"

    roots = [rev for rev, down in chain.items() if down is None]
    assert roots == ["0001"], f"expected the single root 0001, got {roots}"

    children: dict[str, list[str]] = {}
    for rev, down in chain.items():
        if down is not None:
            children.setdefault(down, []).append(rev)
    forks = {rev: kids for rev, kids in children.items() if len(kids) > 1}
    assert not forks, f"revision(s) with more than one child, chain is not linear: {forks}"

    assert "0008" in chain, f"0008 is missing from the chain: {sorted(chain)}"


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
