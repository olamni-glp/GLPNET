"""T042 [US8] — commit-on-complete / all-or-nothing per file (FR-037/FR-039)."""

from __future__ import annotations

from glp_quick.rcopy import transfer
from glp_quick.rcopy.responder import Responder


def _responder(tmp_path, quota_limit=None):
    share = tmp_path / "share" / "docs"
    quota = {"kind": "bytes", "limit": quota_limit} if quota_limit is not None else None
    roots = [{"name": "docs", "path": str(share), "permitted_peers": ["alice-U1"], "quota": quota}]
    return Responder.init(tmp_path / "resp", roots), share


def test_commit_lands_file_under_peer_landing_and_records_all_three(tmp_path):
    r, share = _responder(tmp_path)
    data = b"payload bytes"
    sha = transfer.sha256_bytes(data)
    out = r.commit("alice-U1", "docs", "reports", "note.txt", data, sha)
    assert out.outcome == "transferred"
    landed = share / "xfer" / "in" / "alice-U1" / "reports" / "note.txt"   # FR-033 landing dir
    assert landed.exists() and landed.read_bytes() == data
    assert r.catalog("docs").get("alice-U1", "reports/note.txt").sha256 == sha
    assert any(p.outcome == "transferred" and p.reason is None for p in r.provenance("docs"))


def test_interrupted_verify_failure_leaves_no_trace_but_records_provenance(tmp_path):
    r, share = _responder(tmp_path)
    out = r.commit("alice-U1", "docs", "f", "bad.txt", b"data", "wrong_sha")
    assert out.outcome == "rejected" and out.reason == "verify"
    assert not (share / "xfer" / "in" / "alice-U1" / "f" / "bad.txt").exists()  # nothing committed
    assert len(r.catalog("docs")) == 0                                          # no catalog/quota trace
    assert any(p.outcome == "rejected" and p.reason == "verify" for p in r.provenance("docs"))  # audited


def test_catalog_survives_loss_via_wal_after_commits(tmp_path):
    r, _ = _responder(tmp_path)
    for i in range(3):
        d = f"file{i}".encode()
        assert r.commit("alice-U1", "docs", "f", f"{i}.txt", d, transfer.sha256_bytes(d)).outcome == "transferred"
    r.reload_catalog_from_wal("docs")  # models catalog.json loss + restart (SC-010)
    assert len(r.catalog("docs")) == 3
    for i in range(3):
        assert r.catalog("docs").get("alice-U1", f"f/{i}.txt") is not None


def test_quota_reflects_committed_bytes(tmp_path):
    r, _ = _responder(tmp_path, quota_limit=20)
    d = b"0123456789"  # 10 bytes
    assert r.commit("alice-U1", "docs", "f", "a.txt", d, transfer.sha256_bytes(d)).outcome == "transferred"
    big = b"x" * 15
    v = r.verdict("alice-U1", "docs", "f", [("b.txt", 15, transfer.sha256_bytes(big))], "force")
    assert v[0].verdict == "reject" and v[0].reason == "quota"   # 10 committed + 15 > 20
