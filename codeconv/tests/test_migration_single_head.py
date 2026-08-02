"""T005 — migration chain is single-head & linear (FR-015 / SC-004).

The dual-``0003`` defect (016 vs 017) is resolved by feature-018
T003/T004: ``0003_dart_plans`` re-id'd to ``0004``, new
``0005_codeconv_builder`` chained on ``0004``. ``0006`` (2026-05-19
remediation) widens the ``builder_runs.outcome`` CHECK to allow
``needs_agent_work`` — forward-only, single linear head preserved.
Asserts exactly one head ``0006`` and the linear chain
``0001→0002→0003→0004→0005→0006`` (offline, authoritative via Alembic
``ScriptDirectory``), and — gated by the bridge — that ``alembic
upgrade head`` succeeds and is idempotent on a fresh cluster (re-run =
no-op; all DDL ``IF NOT EXISTS``).
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

    Feature 020 appends ``0008_equivalence`` (revision 0008, down_revision
    0007) then ``0009_no_emit`` (Stage 4; revision 0009, down_revision
    0008); feature 024 added ``0010_marathon_schema``; feature 035 added
    ``0011_enrich_provenance``; feature 063 added ``0012_msmesh_schema``
    (revision 0012, down_revision 0011), so the single head advanced
    0009 → 0010 → 0011 → 0012. The single-head + linear-chain invariant is
    unchanged. (test_migration_0012_single_head is the owner of the
    authoritative 0012 head assertions.)"""
    sd = _script_dir()
    heads = sd.get_heads()
    assert heads == ["0012"], f"expected single head 0012, got {heads}"


def test_linear_chain_offline() -> None:
    """The chain is strictly linear 0001→…→0012 (no branch/merge), so
    ``alembic upgrade head`` is unambiguous."""
    sd = _script_dir()
    chain = {r.revision: r.down_revision for r in sd.walk_revisions()}
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


@needs_bridge
def test_upgrade_head_succeeds_and_idempotent(discover_repo: Path) -> None:
    """Fresh cluster: ``codeconv migrate`` (alembic upgrade head + DBOS
    launch) exits 0, and a second run is a clean no-op (SC-004 — single
    head, no duplicate-revision / multiple-heads error)."""
    p1 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p1.returncode == 0, f"first migrate failed: {p1.stderr}"
    p2 = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert p2.returncode == 0, f"re-migrate not idempotent: {p2.stderr}"
