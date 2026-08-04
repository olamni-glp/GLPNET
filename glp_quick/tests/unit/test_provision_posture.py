"""067 mandatory security posture — the negative tests behind SC-003.

These assert the *absence* of trunk key material on every render path, and that the paths which
must refuse actually refuse instead of degrading.
"""

from __future__ import annotations

import json

import pytest

from glp_quick.cert import derive_device_cert, generate_shared_cert, load_pin, spki_sha256_pin_from_pem
from glp_quick.provision import PostureError, provision_dir
from glp_quick.provision import audit as audit_store
from glp_quick.provision.bundle import Bundle, chunk
from glp_quick.provision.crypto import generate_passphrase, seal
from glp_quick.provision.pdf_render import assert_non_secret, render_non_secret_pdf
from glp_quick.provision.session import ProvisioningSession


@pytest.fixture()
def cert_dir(tmp_path):
    d = tmp_path / "glpquick-cert"
    generate_shared_cert(d, days=30)
    return d


def _trunk_key_text(cert_dir):
    return (cert_dir / "glpquick.key").read_text(encoding="ascii")


def test_rendered_chunks_contain_no_trunk_key(cert_dir):
    """The whole point of the feature: what is scannable is never the trunk secret."""
    s = ProvisioningSession(cert_dir=cert_dir, device_label="tablet", endpoint_host="h", endpoint_port=1)
    chunks = s.render()
    blob = "\n".join(chunks)
    trunk_body = "".join(_trunk_key_text(cert_dir).splitlines()[1:-1])
    assert trunk_body not in blob
    assert "PRIVATE KEY" not in blob  # ciphertext, not PEM
    # And the derived key is present only in encrypted form, never in the clear.
    assert s.derived is not None
    assert s.derived.key_pem not in blob


def test_audit_rows_never_carry_key_material(cert_dir):
    s = ProvisioningSession(cert_dir=cert_dir, device_label="laptop", endpoint_host="h", endpoint_port=1)
    s.render()
    raw = (provision_dir(cert_dir) / "audit.jsonl").read_text(encoding="utf-8")
    assert "PRIVATE KEY" not in raw
    assert "BEGIN" not in raw
    trunk_body = "".join(_trunk_key_text(cert_dir).splitlines()[1:-1])
    assert trunk_body not in raw
    # Fingerprints and labels ARE expected.
    rows = [json.loads(ln) for ln in raw.splitlines() if ln.strip()]
    assert any(r["event"] == "issue" and r["fingerprint"] for r in rows)


def test_audit_writer_refuses_a_secret_bearing_row(cert_dir):
    with pytest.raises(PostureError):
        audit_store.record_event(
            cert_dir, "render", device_label="-----BEGIN PRIVATE KEY-----leak"
        )


def test_passphrase_is_never_persisted(cert_dir):
    s = ProvisioningSession(cert_dir=cert_dir, device_label="phone", endpoint_host="h", endpoint_port=1)
    s.render()
    assert s.passphrase
    for path in provision_dir(cert_dir).glob("*.jsonl"):
        assert s.passphrase not in path.read_text(encoding="utf-8")


def test_expiry_forgets_the_passphrase_and_payload(cert_dir):
    s = ProvisioningSession(cert_dir=cert_dir, device_label="phone", endpoint_host="h", endpoint_port=1)
    s.render()
    s.expire()
    assert s.passphrase is None
    assert s.chunks == []


def test_pdf_carries_no_secret_and_refuses_one(cert_dir, tmp_path):
    out = render_non_secret_pdf(
        tmp_path / "handoff.pdf",
        session_id="sess1234",
        device_label="tablet",
        endpoint_host="10.0.0.5",
        endpoint_port=4433,
        trunk_spki_pin=load_pin(cert_dir),
    )
    raw = out.read_bytes()
    assert b"PRIVATE KEY" not in raw
    assert b"GQP1|" not in raw

    with pytest.raises(PostureError):
        assert_non_secret("-----BEGIN PRIVATE KEY-----")
    with pytest.raises(PostureError):
        assert_non_secret("GQP1|abcd|1/1|deadbeef|payload")


def test_derived_cert_is_not_the_trunk_cert(cert_dir):
    """A derived credential must be a genuinely different key, not a copy of the trunk."""
    derived = derive_device_cert(cert_dir, device_label="tablet", ttl_days=30)
    assert derived.spki_pin != load_pin(cert_dir)
    assert derived.key_pem != _trunk_key_text(cert_dir)


def test_derived_cert_ttl_is_bounded(cert_dir):
    derived = derive_device_cert(cert_dir, device_label="tablet", ttl_days=30)
    assert (derived.not_after - derived.not_before).days <= 31


def test_no_image_writing_helper_exists():
    """FR-005 holds by construction: the render module exposes no file-writing entry point."""
    from glp_quick.provision import qr_render

    exported = [n for n in dir(qr_render) if not n.startswith("_")]
    assert not any(
        tok in n.lower() for n in exported for tok in ("save", "write", "png", "svg", "file")
    ), f"qr_render must expose no image-persisting helper; found {exported}"
