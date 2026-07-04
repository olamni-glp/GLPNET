"""T041 [US8] — responder permission / quota / path-safety verdicts (FR-033/FR-038)."""

from __future__ import annotations

from glp_quick.rcopy import transfer
from glp_quick.rcopy.responder import Responder


def _responder(tmp_path, quota_limit=None):
    share = tmp_path / "share" / "docs"
    quota = {"kind": "bytes", "limit": quota_limit} if quota_limit is not None else None
    roots = [{"name": "docs", "path": str(share), "permitted_peers": ["alice-U1"], "quota": quota}]
    return Responder.init(tmp_path / "resp", roots), share


def test_offer_lists_only_permitted_roots(tmp_path):
    r, _ = _responder(tmp_path)
    assert [name for name, _f, _q in r.offer("alice-U1")] == ["docs"]
    assert r.offer("mallory-U9") == []   # not permitted ⇒ no service (wizard stops)


def test_unpermitted_peer_is_rejected_perm_and_writes_nothing(tmp_path):
    r, share = _responder(tmp_path)
    v = r.verdict("mallory-U9", "docs", "f", [("a.txt", 10, "sha")], "force")
    assert v[0].verdict == "reject" and v[0].reason == "perm"
    # nothing written outside (or inside) a permitted root
    assert not (share / "xfer").exists()


def test_path_traversal_is_rejected_path(tmp_path):
    r, _ = _responder(tmp_path)
    v = r.verdict("alice-U1", "docs", "f", [("../../escape.txt", 10, "sha")], "force")
    assert v[0].verdict == "reject" and v[0].reason == "path"


def test_quota_exceeding_files_are_rejected_quota_per_file(tmp_path):
    r, _ = _responder(tmp_path, quota_limit=100)
    manifest = [("a.txt", 60, "sa"), ("b.txt", 60, "sb"), ("c.txt", 10, "sc")]
    v = r.verdict("alice-U1", "docs", "f", manifest, "force")
    kinds = [(x.verdict, x.reason) for x in v]
    assert kinds[0] == ("need", None)             # 60 ≤ 100
    assert kinds[1] == ("reject", "quota")        # 60+60 > 100
    assert kinds[2] == ("need", None)             # 60+10 ≤ 100 (b excluded)


def test_synchronise_skips_identical_force_needs_all(tmp_path):
    r, _ = _responder(tmp_path)
    data = b"hello world"
    sha = transfer.sha256_bytes(data)
    r.commit("alice-U1", "docs", "f", "a.txt", data, sha)   # now catalogued
    sync = r.verdict("alice-U1", "docs", "f", [("a.txt", len(data), sha)], "synchronise")
    assert sync[0].verdict == "skip_identical"
    force = r.verdict("alice-U1", "docs", "f", [("a.txt", len(data), sha)], "force")
    assert force[0].verdict == "need"


def test_every_manifested_file_gets_receive_provenance(tmp_path):
    r, _ = _responder(tmp_path, quota_limit=50)
    r.verdict("alice-U1", "docs", "f",
              [("ok.txt", 10, "s1"), ("../../esc.txt", 10, "s2"), ("big.txt", 999, "s3")], "force")
    outcomes = {(p.outcome, p.reason) for p in r.provenance("docs")}
    assert ("rejected", "path") in outcomes and ("rejected", "quota") in outcomes
