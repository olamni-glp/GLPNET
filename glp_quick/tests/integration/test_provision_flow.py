"""067 end-to-end producer↔consumer flow (US1/US2 acceptance, quickstart.md).

Covers the loop that matters: mint → render → scan → decrypt → derived material on the joining
host, plus the lifecycle refusals (expiry, replay, revocation) that gate it.

The mesh join itself is not exercised here — that is the C# acceptance seam
(contracts/join-seam-contract.md), which is a separate deliverable.
"""

from __future__ import annotations

from datetime import timedelta

import pytest

from glp_quick.cert import generate_shared_cert, load_pin, verify_derived_against_trunk
from glp_quick.provision import audit as audit_store
from glp_quick.provision.bundle import Bundle, assemble
from glp_quick.provision.crypto import BadPassphrase, open_envelope
from glp_quick.provision.join import join_from_chunks
from glp_quick.provision.session import ProvisioningSession


@pytest.fixture()
def cert_dir(tmp_path):
    d = tmp_path / "glpquick-cert"
    generate_shared_cert(d, days=30)
    return d


def _session(cert_dir, **kw):
    return ProvisioningSession(
        cert_dir=cert_dir,
        device_label=kw.pop("label", "gavri-laptop"),
        endpoint_host=kw.pop("host", "10.0.0.5"),
        endpoint_port=kw.pop("port", 4433),
        **kw,
    )


def test_full_provision_to_join_roundtrip(cert_dir, tmp_path):
    """The headline flow: one scan set + one out-of-band passphrase yields joinable material."""
    s = _session(cert_dir)
    chunks = s.render()

    out = tmp_path / "glpquick-derived"
    result = join_from_chunks(chunks, s.passphrase, out)

    assert result.session_id == s.session_id
    assert result.trunk_spki_pin == load_pin(cert_dir)
    assert result.endpoint == "10.0.0.5:4433"

    cert_pem = (out / "device.pem").read_text(encoding="ascii")
    key_pem = (out / "device.key").read_text(encoding="ascii")
    assert "BEGIN CERTIFICATE" in cert_pem
    assert "BEGIN PRIVATE KEY" in key_pem

    # The joining host's credential must actually chain to the pinned trunk — otherwise existing
    # endpoints could not accept it without a new distribution channel (FR-012).
    assert verify_derived_against_trunk(cert_pem, cert_dir)


def test_join_with_wrong_passphrase_writes_nothing(cert_dir, tmp_path):
    s = _session(cert_dir)
    chunks = s.render()
    out = tmp_path / "glpquick-derived"
    with pytest.raises(BadPassphrase):
        join_from_chunks(chunks, "wrong-words-entirely-here-nope", out)
    assert not out.exists() or not any(out.iterdir())


def test_scan_order_does_not_matter(cert_dir, tmp_path):
    s = _session(cert_dir)
    chunks = s.render()
    result = join_from_chunks(list(reversed(chunks)), s.passphrase, tmp_path / "d")
    assert result.session_id == s.session_id


def test_photographed_codes_are_useless_without_the_passphrase(cert_dir):
    """SC-005: a bystander photograph yields ciphertext only."""
    s = _session(cert_dir)
    chunks = s.render()
    envelope = assemble(chunks)  # anyone can do this much
    with pytest.raises(BadPassphrase):
        open_envelope(envelope, "guessed-passphrase-attempt-one")


def test_expired_session_cannot_be_redeemed(cert_dir):
    s = _session(cert_dir, window_minutes=10)
    s.render()
    s.created_at = s.created_at - timedelta(minutes=11)  # simulate the window lapsing
    with pytest.raises(RuntimeError, match="session_expired"):
        s.redeem()
    assert s.state == "expired"
    assert s.passphrase is None


def test_single_redemption_refuses_replay(cert_dir):
    s = _session(cert_dir)
    s.render()
    s.redeem()
    with pytest.raises(RuntimeError, match="session_replayed"):
        s.redeem()
    events = [r["event"] for r in audit_store.read_audit(cert_dir)]
    assert "redeem" in events
    assert "refuse" in events


def test_audit_answers_who_was_provisioned_and_what_is_revoked(cert_dir):
    """SC-004: the operator question is answerable from the stores alone."""
    a = _session(cert_dir, label="tablet-a")
    a.render()
    b = _session(cert_dir, label="tablet-b")
    b.render()

    audit_store.record_revocation(cert_dir, fingerprint=a.derived.spki_pin, reason="device lost")

    status = {s.device_label: s for s in audit_store.credential_status(cert_dir)}
    assert status["tablet-a"].revoked is True
    assert status["tablet-a"].revoked_at
    assert status["tablet-b"].revoked is False  # revocation is per-credential, not fleet-wide

    assert audit_store.is_revoked(cert_dir, a.derived.spki_pin)
    assert not audit_store.is_revoked(cert_dir, b.derived.spki_pin)


def test_revoked_device_can_be_reprovisioned(cert_dir):
    """Revocation is per-credential, not a permanent device ban (US2 acceptance 3)."""
    first = _session(cert_dir, label="tablet")
    first.render()
    audit_store.record_revocation(cert_dir, fingerprint=first.derived.spki_pin)

    second = _session(cert_dir, label="tablet")
    second.render()
    assert second.derived.spki_pin != first.derived.spki_pin
    assert not audit_store.is_revoked(cert_dir, second.derived.spki_pin)


def test_every_render_is_audited(cert_dir):
    for i in range(3):
        _session(cert_dir, label=f"dev{i}").render()
    events = [r["event"] for r in audit_store.read_audit(cert_dir)]
    assert events.count("render") == 3
    assert events.count("issue") == 3


def test_bundle_carries_the_pin_the_device_needs(cert_dir):
    s = _session(cert_dir)
    chunks = s.render()
    bundle = Bundle.from_json(open_envelope(assemble(chunks), s.passphrase))
    assert bundle.trunk_spki_pin == load_pin(cert_dir)
    assert bundle.session_id == s.session_id
