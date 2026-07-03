"""T049 [US6] — the client wizard flow against a local responder (FR-018/027/029/030/031/SC-007)."""

from __future__ import annotations

from glp_quick.rcopy.filter import ExclusionFilter
from glp_quick.rcopy.responder import Responder
from glp_quick.rcopy.wizard import (
    DirectResponderProxy, FileSpec, TransferRequest, disk_reader, gather_files, run_transfer,
)

PEER = "alice-U1"


def _responder(tmp_path, quota=None):
    quota_cfg = {"kind": "bytes", "limit": quota} if quota is not None else None
    roots = [{"name": "docs", "path": str(tmp_path / "share"), "permitted_peers": [PEER], "quota": quota_cfg}]
    return Responder.init(tmp_path / "resp", roots)


def _src(tmp_path, files: dict):
    src = tmp_path / "src"
    src.mkdir()
    for name, content in files.items():
        (src / name).write_text(content)
    return src


def test_no_service_from_unpermitted_peer_reports_and_zero_transfers(tmp_path):
    r = _responder(tmp_path)
    src = _src(tmp_path, {"a.txt": "hi"})
    req = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter())], "force")
    res = run_transfer("mallory-U9", req, DirectResponderProxy(r), disk_reader(src))
    assert res.no_service and res.outcomes == []


def test_end_to_end_mixed_outcomes_and_synchronise_then_force(tmp_path):
    r = _responder(tmp_path)
    src = _src(tmp_path, {"new.txt": "brand new", "skip.tmp": "excluded", "same.txt": "identical"})
    spec = FileSpec(gather_files(src), ExclusionFilter(name_globs=("*.tmp",)))
    req = TransferRequest("docs", "f", [spec], "synchronise")

    res = run_transfer(PEER, req, DirectResponderProxy(r), disk_reader(src))
    om = {o.rel: (o.outcome, o.reason) for o in res.outcomes}
    assert om["skip.tmp"] == ("filtered_out", None)     # excluded ⇒ never sent (FR-028)
    assert om["new.txt"][0] == "transferred"
    assert om["same.txt"][0] == "transferred"

    # re-run synchronise: identical files are now skipped (FR-034)
    req2 = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter(name_globs=("*.tmp",)))],
                           "synchronise")
    res2 = run_transfer(PEER, req2, DirectResponderProxy(r), disk_reader(src))
    om2 = {o.rel: o.outcome for o in res2.outcomes}
    assert om2["new.txt"] == "skipped_identical" and om2["same.txt"] == "skipped_identical"

    # force overwrites regardless (FR-030)
    req3 = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter(name_globs=("*.tmp",)))],
                           "force")
    res3 = run_transfer(PEER, req3, DirectResponderProxy(r), disk_reader(src))
    assert {o.rel: o.outcome for o in res3.outcomes}["new.txt"] == "transferred"


def test_quota_exceeding_file_reports_rejected(tmp_path):
    r = _responder(tmp_path, quota=5)
    src = _src(tmp_path, {"big.txt": "way too many bytes"})
    req = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter())], "force")
    res = run_transfer(PEER, req, DirectResponderProxy(r), disk_reader(src))
    assert {o.rel: (o.outcome, o.reason) for o in res.outcomes}["big.txt"] == ("rejected", "quota")


def test_exactly_one_outcome_per_selected_file(tmp_path):
    r = _responder(tmp_path)
    src = _src(tmp_path, {"a.txt": "a", "b.tmp": "b", "c.txt": "c"})
    req = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter(name_globs=("*.tmp",)))],
                          "force")
    res = run_transfer(PEER, req, DirectResponderProxy(r), disk_reader(src))
    rels = [o.rel for o in res.outcomes]
    assert sorted(rels) == ["a.txt", "b.tmp", "c.txt"]     # every selected file (SC-007)
    assert len(rels) == len(set(rels))                     # exactly one outcome each


def test_fingerprint_off_still_transfers(tmp_path):
    r = _responder(tmp_path)
    src = _src(tmp_path, {"a.txt": "data"})
    req = TransferRequest("docs", "f", [FileSpec(gather_files(src), ExclusionFilter())],
                          "force", fingerprint=False)
    res = run_transfer(PEER, req, DirectResponderProxy(r), disk_reader(src))
    assert res.outcomes[0].outcome == "transferred"
