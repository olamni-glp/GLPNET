"""T039 [US8] — the per-root WAL journal + catalog rebuild after loss (FR-036/SC-010)."""

from __future__ import annotations

from glp_quick.rcopy.catalog import PerRootCatalog
from glp_quick.rcopy.wal import WalJournal, WalRecord


def _rec(rel, sha, size=10, op="put", peer="alice-U1", folder="f"):
    return WalRecord(op, rel, size, sha, 111, peer, "docs", folder, 222)


def test_append_and_replay_in_order(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    wal.append(_rec("f/a.txt", "sha_a"))
    wal.append(_rec("f/b.txt", "sha_b"))
    assert [r.rel for r in wal.replay()] == ["f/a.txt", "f/b.txt"]


def test_rebuild_catalog_from_wal(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    wal.append(_rec("f/a.txt", "sha_a"))
    cat = PerRootCatalog.from_wal(wal)
    assert len(cat) == 1
    assert cat.get("alice-U1", "f/a.txt").sha256 == "sha_a"


def test_remove_op_drops_the_entry(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    wal.append(_rec("f/a.txt", "sha_a"))
    wal.append(_rec("f/a.txt", "", op="remove"))
    assert PerRootCatalog.from_wal(wal).get("alice-U1", "f/a.txt") is None


def test_replay_is_idempotent(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    wal.append(_rec("f/a.txt", "sha_a"))
    wal.append(_rec("f/a.txt", "sha_a2"))  # re-put (last write wins)
    a, b = wal.rebuild(), wal.rebuild()
    assert a.keys() == b.keys()
    assert a[("alice-U1", "f/a.txt")].sha256 == "sha_a2"


def test_catalog_loss_is_fully_recreated_from_wal(tmp_path):
    # SC-010: build catalog, snapshot it, delete the snapshot, rebuild from WAL with 0 loss.
    wal = WalJournal(tmp_path / "wal.log")
    for i in range(5):
        wal.append(_rec(f"f/{i}.txt", f"sha{i}", size=i + 1))
    snap = tmp_path / "catalog.json"
    PerRootCatalog.from_wal(wal).save(snap)
    assert snap.exists()
    snap.unlink()  # lose the projection

    rebuilt = PerRootCatalog.from_wal(wal)
    assert len(rebuilt) == 5
    for i in range(5):
        assert rebuilt.get("alice-U1", f"f/{i}.txt").sha256 == f"sha{i}"


def test_torn_last_line_is_ignored_not_a_false_inventory(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    wal.append(_rec("f/a.txt", "sha_a"))
    with open(wal.path, "a", encoding="utf-8") as f:
        f.write('{"op":"put","rel":"f/b.txt"')  # a crash-torn partial line, no newline
    assert [r.rel for r in wal.replay()] == ["f/a.txt"]  # partial line dropped
