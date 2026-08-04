"""067 GQP1 chunking + assembly: order independence and named failures (FR-007)."""

from __future__ import annotations

import pytest

from glp_quick.provision.bundle import (
    MAX_CHUNKS,
    SLICE_LEN,
    AssemblyError,
    Bundle,
    TooManyChunks,
    assemble,
    chunk,
)


def _envelope(n: int = 100) -> bytes:
    return bytes(range(256)) * (n // 256 + 1)


def test_single_chunk_roundtrip():
    env = b"small-envelope"
    assert assemble(chunk(env)) == env


def test_multi_chunk_roundtrip():
    env = _envelope(SLICE_LEN * 3)
    lines = chunk(env)
    assert len(lines) > 1
    assert assemble(lines) == env


def test_assembly_is_order_independent():
    env = _envelope(SLICE_LEN * 3)
    lines = chunk(env)
    assert assemble(list(reversed(lines))) == env
    assert assemble([lines[1], lines[-1], lines[0]] + lines[2:-1]) == env


def test_duplicate_identical_chunk_is_idempotent():
    """Scanning the same code twice is normal operator behaviour, not an error."""
    env = _envelope(SLICE_LEN * 2)
    lines = chunk(env)
    assert assemble(lines + [lines[0]]) == env


def test_missing_chunk_names_the_missing_index():
    lines = chunk(_envelope(SLICE_LEN * 3))
    with pytest.raises(AssemblyError) as exc:
        assemble([ln for ln in lines if "|2/" not in ln])
    assert exc.value.token == "missing"
    assert "2" in exc.value.detail


def test_corrupt_chunk_names_the_index():
    lines = chunk(_envelope(SLICE_LEN * 2))
    body = lines[0].split("|")
    body[4] = body[4][:-1] + ("X" if not body[4].endswith("X") else "Y")
    with pytest.raises(AssemblyError) as exc:
        assemble(["|".join(body)] + lines[1:])
    assert exc.value.token == "corrupt_chunk"


def test_unknown_version_tag_fails_fast():
    lines = chunk(b"payload")
    with pytest.raises(AssemblyError) as exc:
        assemble([ln.replace("GQP1|", "GQP9|", 1) for ln in lines])
    assert exc.value.token == "version_mismatch"


def test_conflicting_bundle_ids_are_rejected():
    a = chunk(_envelope(SLICE_LEN * 2), bundle_id="aaaaaaaa")
    b = chunk(_envelope(SLICE_LEN * 2), bundle_id="bbbbbbbb")
    with pytest.raises(AssemblyError) as exc:
        assemble([a[0], b[1]])
    assert exc.value.token == "conflicting_chunk"


def test_no_chunks_is_missing_not_a_crash():
    with pytest.raises(AssemblyError) as exc:
        assemble([])
    assert exc.value.token == "missing"


def test_oversized_bundle_refuses_rather_than_emitting_a_wall_of_codes():
    with pytest.raises(TooManyChunks):
        chunk(_envelope(SLICE_LEN * (MAX_CHUNKS + 2)))


def test_bundle_json_roundtrip():
    b = Bundle(
        endpoint_host="10.0.0.5",
        endpoint_port=4433,
        trunk_spki_pin="pin==",
        device_cert_pem="-----BEGIN CERTIFICATE-----\nx\n-----END CERTIFICATE-----\n",
        device_key_pem="-----BEGIN PRIVATE KEY-----\ny\n-----END PRIVATE KEY-----\n",
        session_id="abcd1234",
    )
    assert Bundle.from_json(b.to_json()) == b


def test_bundle_rejects_foreign_version():
    with pytest.raises(AssemblyError) as exc:
        Bundle.from_json(b'{"v":99,"endpoint":{"host":"h","port":1}}')
    assert exc.value.token == "version_mismatch"
