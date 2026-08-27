"""067 crypto envelope: roundtrip, wrong-passphrase, no-partial-plaintext (FR-004)."""

from __future__ import annotations

import pytest

from glp_quick.provision.crypto import (
    AAD,
    NONCE_LEN,
    SALT_LEN,
    BadPassphrase,
    MalformedEnvelope,
    generate_passphrase,
    open_envelope,
    seal,
)


def test_roundtrip_recovers_exact_plaintext():
    pw = generate_passphrase()
    plaintext = b'{"v":1,"secret":"material"}'
    assert open_envelope(seal(plaintext, pw), pw) == plaintext


def test_wrong_passphrase_raises_and_yields_no_plaintext():
    envelope = seal(b"sensitive", generate_passphrase())
    with pytest.raises(BadPassphrase):
        open_envelope(envelope, "not-the-right-passphrase-at-all")


def test_envelope_framing_is_salt_nonce_then_ciphertext():
    envelope = seal(b"x", "pw")
    # Framing is a wire contract (payload-contract.md §2) — consumers slice by these offsets.
    assert len(envelope) > SALT_LEN + NONCE_LEN + 16
    assert len(set(envelope[:SALT_LEN])) > 1  # salt is random, not a constant block


def test_two_seals_of_same_plaintext_differ():
    """Fresh salt + nonce per seal — a repeated provisioning must not produce identical bytes."""
    assert seal(b"same", "pw") != seal(b"same", "pw")


def test_truncated_envelope_is_malformed_not_bad_passphrase():
    """Structural failure must be distinguishable from an authentication failure."""
    with pytest.raises(MalformedEnvelope):
        open_envelope(b"short", "pw")


def test_tampered_ciphertext_fails_authentication():
    envelope = bytearray(seal(b"payload", "pw"))
    envelope[-1] ^= 0xFF  # flip a tag bit
    with pytest.raises(BadPassphrase):
        open_envelope(bytes(envelope), "pw")


def test_aad_binds_the_format_version():
    assert AAD == b"GQP1"


def test_generated_passphrase_shape():
    pw = generate_passphrase()
    assert len(pw.split("-")) == 6
    assert pw == pw.lower()
    # Distinct across calls with overwhelming probability — this is the entropy smoke test.
    assert len({generate_passphrase() for _ in range(20)}) > 15
