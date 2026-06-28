"""T015 — cert generation + SPKI SHA-256 pin trust accept/reject (research Decision 5, FR-003/SC-005).

The shared self-signed cert is the only trust anchor; trust is the SPKI pin, not a name/CA. These
tests assert the Decision 5 recipe and the accept/reject decision the data-plane callback enforces.
"""

from __future__ import annotations

import base64

import pytest
from cryptography import x509
from cryptography.x509.oid import ExtendedKeyUsageOID

from glp_quick import cert as cert_mod


def _load_cert(cert_dir):
    return x509.load_pem_x509_certificate(cert_mod.load_cert_pem(cert_dir))


def test_generate_writes_all_artifacts(tmp_path):
    r = cert_mod.generate_shared_cert(tmp_path, days=30)
    assert r.pem_path.exists() and r.key_path.exists()
    assert r.pfx_path.exists() and r.fingerprint_path.exists()
    names = {p.name for p in tmp_path.iterdir()}
    assert {"glpquick.pem", "glpquick.key", "glpquick.pfx", "glpquick.fingerprint"} <= names


def test_pin_is_base64_sha256(tmp_path):
    r = cert_mod.generate_shared_cert(tmp_path)
    raw = base64.b64decode(r.spki_pin)
    assert len(raw) == 32  # SHA-256 digest


def test_pin_roundtrips_pem_file_and_pfx(tmp_path):
    r = cert_mod.generate_shared_cert(tmp_path)
    assert cert_mod.spki_sha256_pin_from_pem(cert_mod.load_cert_pem(tmp_path)) == r.spki_pin
    assert cert_mod.load_pin(tmp_path) == r.spki_pin
    from cryptography.hazmat.primitives.serialization import pkcs12

    _key, crt, _ = pkcs12.load_key_and_certificates(r.pfx_path.read_bytes(), None)
    assert cert_mod.spki_sha256_pin(crt) == r.spki_pin


def test_verify_accepts_match_rejects_mismatch(tmp_path):
    r = cert_mod.generate_shared_cert(tmp_path)
    assert cert_mod.verify_peer_pin(r.spki_pin, cert_mod.load_pin(tmp_path)) is True
    assert cert_mod.verify_peer_pin("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", r.spki_pin) is False


def test_distinct_certs_have_distinct_pins_and_are_rejected(tmp_path):
    a = cert_mod.generate_shared_cert(tmp_path / "a")
    b = cert_mod.generate_shared_cert(tmp_path / "b")
    assert a.spki_pin != b.spki_pin
    # cert B presented against pin A → rejected (the cert_mismatch path, FR-019)
    assert cert_mod.verify_peer_pin(b.spki_pin, a.spki_pin) is False


@pytest.mark.parametrize("key_type", ["ec", "rsa"])
def test_decision5_cert_profile(tmp_path, key_type):
    cert_mod.generate_shared_cert(tmp_path, key_type=key_type)
    crt = _load_cert(tmp_path)
    # subject == issuer (self-signed)
    assert crt.subject == crt.issuer
    # BasicConstraints(ca=False)
    bc = crt.extensions.get_extension_for_class(x509.BasicConstraints).value
    assert bc.ca is False
    # KeyUsage(digital_signature, key_encipherment)
    ku = crt.extensions.get_extension_for_class(x509.KeyUsage).value
    assert ku.digital_signature is True and ku.key_encipherment is True
    # EKU[serverAuth, clientAuth]
    eku = crt.extensions.get_extension_for_class(x509.ExtendedKeyUsage).value
    assert ExtendedKeyUsageOID.SERVER_AUTH in eku and ExtendedKeyUsageOID.CLIENT_AUTH in eku


def test_days_controls_validity(tmp_path):
    cert_mod.generate_shared_cert(tmp_path, days=10)
    crt = _load_cert(tmp_path)
    span = crt.not_valid_after_utc - crt.not_valid_before_utc
    assert 10 <= span.days <= 11  # 10 days + the 1-minute skew tolerance


def test_zero_days_rejected(tmp_path):
    with pytest.raises(ValueError):
        cert_mod.generate_shared_cert(tmp_path, days=0)
