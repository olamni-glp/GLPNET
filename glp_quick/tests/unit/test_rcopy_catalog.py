"""T040 [US8] — catalog projection + synchronise SHA-256 compare (FR-034/FR-035)."""

from __future__ import annotations

from glp_quick.rcopy.catalog import CatalogEntry, PerRootCatalog
from glp_quick.rcopy.wal import WalJournal, WalRecord


def _cat_with(peer="alice-U1"):
    e = CatalogEntry("f/a.txt", 100, "sha_a", 111, peer, "f")
    return PerRootCatalog({(peer, "f/a.txt"): e})


def test_is_identical_true_on_matching_sha():
    cat = _cat_with()
    assert cat.is_identical("alice-U1", "f/a.txt", "sha_a")       # synchronise skips it


def test_is_identical_false_on_sha_mismatch_or_missing():
    cat = _cat_with()
    assert not cat.is_identical("alice-U1", "f/a.txt", "sha_DIFFERENT")   # changed → needs transfer
    assert not cat.is_identical("alice-U1", "f/missing.txt", "sha")       # new → needs transfer
    assert not cat.is_identical("other-U2", "f/a.txt", "sha_a")           # a different peer's landing


def test_byte_accounting_for_quota():
    peer = "alice-U1"
    cat = PerRootCatalog({
        (peer, "f/a.txt"): CatalogEntry("f/a.txt", 100, "sa", 0, peer, "f"),
        (peer, "f/b.txt"): CatalogEntry("f/b.txt", 250, "sb", 0, peer, "f"),
    })
    assert cat.total_bytes() == 350 and cat.peer_bytes(peer) == 350


def test_apply_put_then_remove():
    cat = PerRootCatalog()
    rec = WalRecord("put", "f/a.txt", 10, "sa", 0, "alice-U1", "docs", "f", 0)
    cat.apply(rec)
    assert cat.get("alice-U1", "f/a.txt") is not None
    cat.apply(WalRecord("remove", "f/a.txt", 0, "", 0, "alice-U1", "docs", "f", 0))
    assert cat.get("alice-U1", "f/a.txt") is None


def test_snapshot_save_load_round_trip(tmp_path):
    cat = _cat_with()
    cat.save(tmp_path / "catalog.json")
    loaded = PerRootCatalog.load(tmp_path / "catalog.json")
    assert loaded.get("alice-U1", "f/a.txt").sha256 == "sha_a"
    assert loaded.total_bytes() == 100


def test_from_wal_matches_incremental_apply(tmp_path):
    wal = WalJournal(tmp_path / "wal.log")
    incr = PerRootCatalog()
    for i in range(3):
        rec = WalRecord("put", f"f/{i}", i + 1, f"s{i}", 0, "alice-U1", "docs", "f", 0)
        wal.append(rec)
        incr.apply(rec)
    assert PerRootCatalog.from_wal(wal).entries() == incr.entries()
