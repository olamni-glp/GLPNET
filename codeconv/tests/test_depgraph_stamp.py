"""stamp-tombstones — embeds the six feature-015 keys into tombstone YAML.

Test obligations from
``specs/015-codeconv-depgraph/contracts/tombstone_format_delta.md``
§ "Test obligations" / `test_depgraph_stamp.py` (5 items). T032.
Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from codeconv.tools.discover.tombstone import read_tombstone

from .conftest import needs_bridge, run_codeconv
from .test_depgraph_compute import _migrate_and_discover, _mk_chain_subtree

_TOMB_A = ("lib", "a.dart.md")
_SIX = (
    "topo_level",
    "cycle_group_id",
    "status",
    "conversion_started_at",
    "conversion_completed_at",
    "target_path",
)


def _tomb(repo_root: Path, *parts: str) -> Path:
    return repo_root.joinpath(".codeconv", "tombstones", *parts)


def _digest(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


@needs_bridge
def test_initial_stamp_adds_six_keys(discover_repo: Path) -> None:
    """A converted file's tombstone gains ALL six keys after stamp."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    fm0 = read_tombstone(_tomb(discover_repo, *_TOMB_A))
    assert not any(k in fm0 for k in _SIX), "fresh discover must have no 015 keys"

    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "out/A.cs",
    )
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    fm = read_tombstone(_tomb(discover_repo, *_TOMB_A))
    for k in _SIX:
        assert k in fm, f"{k} missing after stamp; got {sorted(fm)}"
    assert fm["status"] == "converted"
    assert fm["target_path"] == "out/A.cs"


@needs_bridge
def test_restamp_is_idempotent(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    before = _digest(_tomb(discover_repo, *_TOMB_A))
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    assert _digest(_tomb(discover_repo, *_TOMB_A)) == before, (
        "re-stamp on unchanged DB state must produce zero tombstone diff"
    )


@needs_bridge
def test_pending_status_yaml_representation(discover_repo: Path) -> None:
    """No dart_conversions row ⇒ status pending, conversion keys ABSENT
    (not null) per the null-vs-missing convention."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    # b.dart depends on a (not converted) ⇒ pending, no conversion row.
    fm = read_tombstone(_tomb(discover_repo, "lib", "b.dart.md"))
    assert fm["status"] == "pending"
    assert "conversion_started_at" not in fm
    assert "conversion_completed_at" not in fm
    assert "target_path" not in fm


@needs_bridge
def test_in_progress_status_yaml_representation(discover_repo: Path) -> None:
    """Row with completed_at NULL ⇒ status in_progress, started present,
    completed present-but-null, target present-but-null."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    fm = read_tombstone(_tomb(discover_repo, *_TOMB_A))
    assert fm["status"] == "in_progress"
    assert fm["conversion_started_at"]  # present + non-null
    assert "conversion_completed_at" in fm and fm["conversion_completed_at"] is None
    assert "target_path" in fm and fm["target_path"] is None


@needs_bridge
def test_converted_status_yaml_representation(discover_repo: Path) -> None:
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    run_codeconv(discover_repo, "depgraph", "mark-started", "lib/a.dart")
    run_codeconv(
        discover_repo, "depgraph", "mark-completed", "lib/a.dart",
        "--target", "out/A.cs",
    )
    assert run_codeconv(discover_repo, "depgraph", "compute").returncode == 0
    assert (
        run_codeconv(
            discover_repo, "depgraph", "stamp-tombstones"
        ).returncode
        == 0
    )
    fm = read_tombstone(_tomb(discover_repo, *_TOMB_A))
    assert fm["status"] == "converted"
    assert fm["conversion_started_at"]
    assert fm["conversion_completed_at"]  # present + non-null
    assert fm["target_path"] == "out/A.cs"
