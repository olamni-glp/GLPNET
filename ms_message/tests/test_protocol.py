"""T006 — protocol shapes round-trip + named-fault decoding.

Contract: specs/063-wave-5-consolidated-captured-triad/contracts/
mesh-messaging-protocol.md ("Messages (hop-to-hop)").
"""

from __future__ import annotations

import json

import pytest

from ms_message.protocol import (
    PROTOCOL_VERSION,
    UNKNOWN,
    BatchMessage,
    Fetch,
    FetchBatch,
    FriendLookup,
    FriendReply,
    GapMarker,
    ProtocolError,
    Signal,
    decode,
    encode,
)


ROUND_TRIP_PAYLOADS = [
    Signal(holder_station="alice", mailbox_id="news", high_water_seq=17),
    Fetch(requester_station="bob", mailbox_id="news", from_seq=3, max_count=100),
    FetchBatch(
        mailbox_id="news",
        entries=(
            BatchMessage(sender_station="alice", sender_seq=3, content=b"hello"),
            BatchMessage(sender_station="alice", sender_seq=4, content=b""),
            GapMarker(expected_seq=5, got_seq=7),
            BatchMessage(sender_station="alice", sender_seq=7, content=b"\x00\xffbin"),
        ),
        high_water_seq=7,
    ),
    FetchBatch(mailbox_id="empty", entries=(), high_water_seq=0),
    FriendLookup(asker="alice", target_station="carol"),
    FriendReply(target_station="carol", address="192.168.0.42:4501"),
    FriendReply(target_station="mallory", address=UNKNOWN),
]


@pytest.mark.parametrize("payload", ROUND_TRIP_PAYLOADS, ids=lambda p: type(p).__name__)
def test_round_trip(payload):
    assert decode(encode(payload)) == payload


def test_wire_form_is_ground_json_with_version_and_kind():
    obj = json.loads(encode(Signal("alice", "news", 1)).decode("utf-8"))
    assert obj["v"] == PROTOCOL_VERSION
    assert obj["kind"] == "signal"
    assert obj["holder_station"] == "alice"


def test_signal_carries_no_content_field():
    obj = json.loads(encode(Signal("alice", "news", 9)).decode("utf-8"))
    assert not any("content" in k for k in obj)


def test_friend_reply_unknown_sentinel():
    assert FriendReply("x", UNKNOWN).is_unknown
    assert not FriendReply("x", "10.0.0.1").is_unknown


def test_batch_gap_is_explicit_not_silent():
    batch = decode(
        encode(
            FetchBatch(
                mailbox_id="m",
                entries=(
                    BatchMessage("a", 1, b"x"),
                    GapMarker(expected_seq=2, got_seq=4),
                    BatchMessage("a", 4, b"y"),
                ),
                high_water_seq=4,
            )
        )
    )
    assert isinstance(batch, FetchBatch)
    gaps = [e for e in batch.entries if isinstance(e, GapMarker)]
    assert gaps == [GapMarker(expected_seq=2, got_seq=4)]


@pytest.mark.parametrize(
    "raw",
    [
        b"not json at all",
        b"[1,2,3]",
        b'{"kind":"signal"}',  # missing version
        b'{"v":99,"kind":"signal","holder_station":"a","mailbox_id":"m","high_water_seq":1}',
        b'{"v":1,"kind":"no_such_kind"}',
        b'{"v":1,"kind":"signal","holder_station":"a","mailbox_id":"m"}',  # missing field
        b'{"v":1,"kind":"signal","holder_station":"a","mailbox_id":"m","high_water_seq":"one"}',
        b'{"v":1,"kind":"signal","holder_station":"a","mailbox_id":"m","high_water_seq":true}',
        b'{"v":1,"kind":"fetch_batch","mailbox_id":"m","entries":[{"kind":"bogus"}],"high_water_seq":0}',
        b'{"v":1,"kind":"fetch_batch","mailbox_id":"m","entries":[{"kind":"msg","sender_station":"a","sender_seq":1,"content_b64":"@@@"}],"high_water_seq":1}',
        b'{"v":1,"kind":"fetch_batch","mailbox_id":"m","entries":["not-an-object"],"high_water_seq":0}',
    ],
    ids=[
        "not-json",
        "not-object",
        "missing-version",
        "wrong-version",
        "unknown-kind",
        "missing-field",
        "wrong-type",
        "bool-as-int",
        "bogus-entry-kind",
        "bad-base64",
        "entry-not-object",
    ],
)
def test_malformed_payload_raises_named_fault(raw):
    with pytest.raises(ProtocolError):
        decode(raw)
