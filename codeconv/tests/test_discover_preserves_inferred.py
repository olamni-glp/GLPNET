"""Feature 035 / US2 (T014) — discover preserves inferred provenance (FR-008).

Three cases (contracts/discover_preservation.md):
- (a) ``discover`` re-run on an UNCHANGED enriched file preserves
  ``purpose``/``key_idea``/``*_source: inferred`` (SC-003, 100%).
- (b) a SOURCE CHANGE → ``discover`` re-seeds + resets ``*_source`` (FR-007).
- (c) drop the ``dart_files`` row (rebuilt inventory) → ``discover`` restores
  the inferred values from the tombstone, not blanks them (R-002 case a).
"""

from __future__ import annotations

from pathlib import Path

from sqlalchemy import text

from .conftest import BRIDGE_SCRIPT, fake_infer_fn, needs_bridge, run_codeconv
from codeconv.tools.discover.tombstone import read_tombstone, tombstone_path


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "blank.dart").write_text(
        "class Blank {\n  int v = 1;\n}\n", encoding="utf-8"
    )
    return sub


def _discover(repo_root: Path, sub: Path) -> None:
    assert run_codeconv(
        repo_root, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0


def _enrich(repo_root: Path) -> dict:
    from codeconv.tools.enrich.workflow import run_enrich

    return run_enrich(repo_root, infer_fn=fake_infer_fn, bridge_script=BRIDGE_SCRIPT)


@needs_bridge
def test_discover_preserves_inferred_on_unchanged(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    _discover(discover_repo, sub)
    _enrich(discover_repo)

    troot = discover_repo / ".codeconv" / "tombstones"
    tomb = tombstone_path(troot, "lib/blank.dart")
    before = read_tombstone(tomb)
    assert before["purpose_source"] == "inferred"

    # Re-discover on the unchanged file → inferred values intact (SC-003).
    _discover(discover_repo, sub)
    after = read_tombstone(tomb)
    assert after["purpose"] == before["purpose"]
    assert after["key_idea"] == before["key_idea"]
    assert after["purpose_source"] == "inferred"
    assert after["key_idea_source"] == "inferred"

    from codeconv.db.engine import connect

    engine = connect(discover_repo)
    with engine.begin() as conn:
        row = conn.execute(
            text(
                "SELECT purpose, purpose_source, key_idea_source "
                "FROM codeconv.dart_files WHERE path = :p"
            ),
            {"p": "lib/blank.dart"},
        ).first()
    assert row is not None and row[0] == before["purpose"]
    assert row[1] == "inferred" and row[2] == "inferred"


@needs_bridge
def test_discover_reblanks_on_source_change(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    _discover(discover_repo, sub)
    _enrich(discover_repo)

    troot = discover_repo / ".codeconv" / "tombstones"
    tomb = tombstone_path(troot, "lib/blank.dart")
    assert read_tombstone(tomb)["purpose_source"] == "inferred"

    (sub / "lib" / "blank.dart").write_text(
        "class Blank {\n  int v = 99;\n  int q = 0;\n}\n", encoding="utf-8"
    )
    _discover(discover_repo, sub)
    after = read_tombstone(tomb)
    # FR-007: stale inference discarded — re-seeded blank, provenance reset.
    assert after["purpose"] == ""
    assert after["key_idea"] == ""
    assert after["purpose_source"] == "absent"
    assert after["key_idea_source"] == "absent"


@needs_bridge
def test_discover_restores_inferred_from_tombstone_when_row_absent(
    discover_repo: Path,
) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    _discover(discover_repo, sub)
    _enrich(discover_repo)

    troot = discover_repo / ".codeconv" / "tombstones"
    tomb = tombstone_path(troot, "lib/blank.dart")
    before = read_tombstone(tomb)
    assert before["purpose_source"] == "inferred"

    # Simulate a rebuilt inventory: drop the dart_files row (tombstone is the
    # durable record). discover must RESTORE the inferred values, not blank them.
    from codeconv.db.engine import connect

    engine = connect(discover_repo)
    with engine.begin() as conn:
        conn.execute(
            text("DELETE FROM codeconv.dart_files WHERE path = :p"),
            {"p": "lib/blank.dart"},
        )

    _discover(discover_repo, sub)

    after = read_tombstone(tomb)
    assert after["purpose"] == before["purpose"]
    assert after["key_idea"] == before["key_idea"]
    assert after["purpose_source"] == "inferred"
    assert after["key_idea_source"] == "inferred"

    with engine.begin() as conn:
        row = conn.execute(
            text(
                "SELECT purpose, key_idea, purpose_source, key_idea_source "
                "FROM codeconv.dart_files WHERE path = :p"
            ),
            {"p": "lib/blank.dart"},
        ).first()
    assert row is not None
    assert row[0] == before["purpose"] and row[1] == before["key_idea"]
    assert row[2] == "inferred" and row[3] == "inferred"
