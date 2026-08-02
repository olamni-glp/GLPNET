"""T023 — WAL replay, size policy, position durability, gap detection, FR-011 refusals.

Contract guarantees 1–3 (+ named-fault discipline). Bridge-free: everything
here exercises the WAL layer alone.
"""

from __future__ import annotations

import json

import pytest

from ms_message.wal import GapEvent, Wal, WalCorrupt


def test_replay_reconstructs_messages_and_states(tmp_path):
    wal = Wal(tmp_path)
    wal.accept("alice", 1, "news", "bob", b"one")
    wal.accept("alice", 2, "news", "bob", b"two")
    wal.accept("alice", 3, "news", "bob", b"three")
    wal.mark("alice", 1, "signalled")
    wal.mark("alice", 1, "fetched")
    wal.mark("alice", 2, "signalled")

    st = Wal(tmp_path).replay()
    assert len(st.messages) == 3
    assert st.messages[("alice", 1)].state == "fetched"
    assert st.messages[("alice", 2)].state == "signalled"
    assert st.messages[("alice", 3)].state == "journalled"
    assert st.gaps == []


def test_content_round_trips_for_every_placement_class(tmp_path):
    wal = Wal(tmp_path, target_file_size=1000)
    small = b"s" * 10                 # < half target → shared file
    ownish = b"o" * 600               # half..target → own file
    big = b"b" * 2500                 # > target → split parts
    m1 = wal.accept("a", 1, "m", "t", small)
    m2 = wal.accept("a", 2, "m", "t", ownish)
    m3 = wal.accept("a", 3, "m", "t", big)
    assert m1.content_ref.startswith("shared:")
    assert m2.content_ref.startswith("own:")
    assert m3.content_ref.startswith("split:") and m3.content_ref.endswith(":3")
    assert wal.read_content(m1.content_ref) == small
    assert wal.read_content(m2.content_ref) == ownish
    assert wal.read_content(m3.content_ref) == big


def test_shared_file_rotates_at_target_size(tmp_path):
    wal = Wal(tmp_path, target_file_size=100)
    refs = [wal.accept("a", i, "m", "t", b"x" * 30).content_ref for i in range(1, 6)]
    files = {r.split(":")[1] for r in refs}
    assert len(files) > 1, f"expected rotation across shared files, got {files}"
    for i, r in enumerate(refs, start=1):
        assert wal.read_content(r) == b"x" * 30, f"ref {i} corrupted by rotation"


def test_dense_sequence_hole_is_a_named_gap(tmp_path):
    wal = Wal(tmp_path)
    wal.accept("alice", 1, "m", "t", b"1")
    wal.accept("alice", 2, "m", "t", b"2")
    wal.accept("alice", 4, "m", "t", b"4")  # hole at 3
    st = wal.replay()
    assert GapEvent("alice", 3, 4) in st.gaps  # named, never silent (FR-010)


def test_delivery_position_survives_restart(tmp_path):
    """The exactly-once floor (R7): the advanced position is durable across a
    process restart, so refetched duplicates dedup instead of re-observing."""
    wal = Wal(tmp_path)
    wal.advance_position("alice", "inbound", 41, [43])
    st = Wal(tmp_path).replay()  # a fresh instance = the restarted process
    assert st.positions[("alice", "inbound")] == {"high_water": 41, "seen": [43]}


def test_corrupt_journal_line_is_an_explicit_refusal(tmp_path):
    wal = Wal(tmp_path)
    wal.accept("a", 1, "m", "t", b"ok")
    with open(tmp_path / "wal-1.log", "a", encoding="utf-8") as f:
        f.write("this is not json\n")
    with pytest.raises(WalCorrupt):
        wal.replay()  # FR-011: refuse, never silently lose


def test_state_for_unjournalled_message_is_corrupt(tmp_path):
    wal = Wal(tmp_path)
    with open(tmp_path / "wal-1.log", "a", encoding="utf-8") as f:
        f.write(json.dumps({"rec": "state", "sender": "ghost", "seq": 9, "state": "fetched"}) + "\n")
    with pytest.raises(WalCorrupt):
        wal.replay()


def test_missing_message_file_is_a_named_fault(tmp_path):
    wal = Wal(tmp_path, target_file_size=100)
    meta = wal.accept("a", 1, "m", "t", b"o" * 80)  # own file
    (tmp_path / meta.content_ref.split(":", 1)[1]).unlink()
    with pytest.raises(WalCorrupt):
        wal.read_content(meta.content_ref)


def test_unwritable_wal_root_is_an_explicit_fault(tmp_path):
    blocker = tmp_path / "occupied"
    blocker.write_text("a file where the WAL dir must go")
    with pytest.raises(OSError):  # named OS fault at startup, not a silent no-op store
        Wal(blocker)


def test_gap_recorded_at_fetch_is_replayed(tmp_path):
    wal = Wal(tmp_path)
    wal.record_gap("alice", 7, 9)
    st = Wal(tmp_path).replay()
    assert GapEvent("alice", 7, 9) in st.gaps
