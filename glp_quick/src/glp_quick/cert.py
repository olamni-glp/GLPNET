"""Shared self-signed certificate generation + SPKI SHA-256 pin (FR-003 / SC-005, research Decision 5).

The shared self-signed cert is the **only** trust anchor — no domain name, no public CA, no
hostname-bound cert. The Python tool generates it (``glp-quick cert generate``); it is distributed
out-of-band and pinned by **SPKI (SubjectPublicKeyInfo) SHA-256** — which survives re-issue with the
same key, and is preferred over a whole-cert thumbprint (Decision 5; wire-contract.md §Trust).

Recipe (Decision 5, verbatim): ``subject==issuer``, ``BasicConstraints(ca=False)``,
``KeyUsage(digital_signature, key_encipherment)``, ``EKU[serverAuth, clientAuth]``, EC P-256 or
RSA-2048; export PFX (holder) + PEM (distribution).

The validation callback (in the data-plane stacks) **never** blanket-trusts; it waives only the
no-CA-chain error + hostname mismatch — trust is the pin, not the name.
"""

from __future__ import annotations

import base64
import hashlib
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Literal, Optional

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, rsa
from cryptography.hazmat.primitives.serialization import pkcs12
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID

# Canonical artifact names (cli-contract.md §`cert generate`), + the holder PFX (Decision 5).
PEM_NAME = "glpquick.pem"          # public cert, PEM — distributed out-of-band
KEY_NAME = "glpquick.key"          # private key, PEM — server/holder side only
PFX_NAME = "glpquick.pfx"          # PKCS#12 holder bundle (cert+key) — Decision 5
FINGERPRINT_NAME = "glpquick.fingerprint"  # the SPKI SHA-256 pin (the trust anchor value)

#: Fixed common name. Trust is by SPKI pin, NOT this name (FR-003) — so it is purely cosmetic and
#: deliberately carries no host/domain meaning.
_SUBJECT_CN = "GLP-Quick Shared Cert"

KeyType = Literal["ec", "rsa"]


@dataclass(frozen=True)
class CertResult:
    """Outcome of :func:`generate_shared_cert`."""

    pem_path: Path
    key_path: Path
    pfx_path: Path
    fingerprint_path: Path
    spki_pin: str  # base64( SHA-256( DER SubjectPublicKeyInfo ) ) — the pinned trust value


def _build_name() -> x509.Name:
    return x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, _SUBJECT_CN)])


def spki_sha256_pin_from_public_key(public_key) -> str:
    """The SPKI SHA-256 pin: ``base64( SHA-256( DER SubjectPublicKeyInfo ) )`` (RFC 7469 form).

    Computed over the public key's SubjectPublicKeyInfo so it survives certificate re-issue with
    the same key (Decision 5). The C# / Gleam validation callback computes the identical value over
    the presented peer cert's SPKI and compares for equality.
    """
    spki_der = public_key.public_bytes(
        encoding=serialization.Encoding.DER,
        format=serialization.PublicFormat.SubjectPublicKeyInfo,
    )
    return base64.b64encode(hashlib.sha256(spki_der).digest()).decode("ascii")


def spki_sha256_pin(cert: x509.Certificate) -> str:
    """The SPKI SHA-256 pin of a certificate (over its public key's SubjectPublicKeyInfo)."""
    return spki_sha256_pin_from_public_key(cert.public_key())


def spki_sha256_pin_from_pem(pem_bytes: bytes) -> str:
    """Convenience: the SPKI pin of a PEM-encoded certificate (e.g. a peer's distributed cert)."""
    return spki_sha256_pin(x509.load_pem_x509_certificate(pem_bytes))


def spki_sha256_pin_from_der(der_bytes: bytes) -> str:
    """Convenience: the SPKI pin of a DER-encoded certificate (e.g. the on-wire peer cert)."""
    return spki_sha256_pin(x509.load_der_x509_certificate(der_bytes))


def generate_shared_cert(
    out_dir: Path,
    days: int = 365,
    key_type: KeyType = "ec",
    pfx_password: Optional[bytes] = None,
) -> CertResult:
    """Generate the shared self-signed cert + key into ``out_dir`` and emit the SPKI pin (FR-003).

    Writes ``glpquick.pem`` (public), ``glpquick.key`` (private), ``glpquick.pfx`` (holder bundle),
    and ``glpquick.fingerprint`` (the SPKI SHA-256 pin). Returns the paths + pin.
    """
    if days <= 0:
        raise ValueError(f"--days must be positive; got {days}")

    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    # --- key: EC P-256 (default) or RSA-2048 (Decision 5) ---
    if key_type == "ec":
        private_key = ec.generate_private_key(ec.SECP256R1())
    elif key_type == "rsa":
        private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    else:  # pragma: no cover - guarded by the Literal type
        raise ValueError(f"unsupported key_type {key_type!r}; expected 'ec' or 'rsa'")

    # --- self-signed cert: subject == issuer; end-entity (ca=False); serverAuth + clientAuth ---
    name = _build_name()
    now = datetime.now(timezone.utc)
    builder = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)  # subject == issuer (self-signed)
        .public_key(private_key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(minutes=1))  # small skew tolerance
        .not_valid_after(now + timedelta(days=days))
        .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                key_encipherment=True,
                content_commitment=False,
                data_encipherment=False,
                key_agreement=False,
                key_cert_sign=False,
                crl_sign=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .add_extension(
            x509.ExtendedKeyUsage(
                [ExtendedKeyUsageOID.SERVER_AUTH, ExtendedKeyUsageOID.CLIENT_AUTH]
            ),
            critical=False,
        )
    )
    cert = builder.sign(private_key=private_key, algorithm=hashes.SHA256())

    spki_pin = spki_sha256_pin(cert)

    # --- write artifacts ---
    pem_path = out_dir / PEM_NAME
    key_path = out_dir / KEY_NAME
    pfx_path = out_dir / PFX_NAME
    fingerprint_path = out_dir / FINGERPRINT_NAME

    pem_path.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    key_path.write_bytes(
        private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption(),
        )
    )
    pfx_encryption = (
        serialization.BestAvailableEncryption(pfx_password)
        if pfx_password
        else serialization.NoEncryption()
    )
    pfx_path.write_bytes(
        pkcs12.serialize_key_and_certificates(
            name=b"glpquick",
            key=private_key,
            cert=cert,
            cas=None,
            encryption_algorithm=pfx_encryption,
        )
    )
    fingerprint_path.write_text(spki_pin + "\n", encoding="ascii")

    return CertResult(
        pem_path=pem_path,
        key_path=key_path,
        pfx_path=pfx_path,
        fingerprint_path=fingerprint_path,
        spki_pin=spki_pin,
    )


@dataclass(frozen=True)
class DerivedCert:
    """A short-lived, per-device certificate signed by the trunk key (feature 067, FR-003).

    The private key is returned **in memory only**. It is never written to hub disk: it leaves
    this process solely inside the passphrase-encrypted QR payload (067 security posture §1/§4).
    """

    cert_pem: str
    key_pem: str
    spki_pin: str  # SPKI SHA-256 of the derived cert — its identity for audit + revocation
    not_before: datetime
    not_after: datetime
    device_label: str


def derive_device_cert(
    cert_dir: Path,
    device_label: str,
    ttl_days: int = 30,
    key_type: KeyType = "ec",
) -> DerivedCert:
    """Mint a per-device certificate **signed by the trunk key** (067 research R-002/R-003).

    Existing endpoints verify it against the already-pinned trunk SPKI, so the 036 manual-pin
    trust model is unchanged for them (FR-012) — no new distribution channel, no CA, no
    name/hostname semantics.

    The trunk private key is read from the PFX to sign, and is **never** copied into the result.
    """
    if ttl_days <= 0:
        raise ValueError(f"ttl_days must be positive; got {ttl_days}")
    if not device_label.strip():
        raise ValueError("device_label must be non-empty — it is an audit field")

    pfx_bytes = (Path(cert_dir) / PFX_NAME).read_bytes()
    trunk_key, trunk_cert, _ = pkcs12.load_key_and_certificates(pfx_bytes, password=None)
    if trunk_key is None or trunk_cert is None:
        raise ValueError(f"{PFX_NAME} did not yield both a key and a certificate")

    if key_type == "ec":
        device_key = ec.generate_private_key(ec.SECP256R1())
    elif key_type == "rsa":
        device_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    else:  # pragma: no cover - guarded by the Literal type
        raise ValueError(f"unsupported key_type {key_type!r}")

    now = datetime.now(timezone.utc)
    not_before = now - timedelta(minutes=1)  # small skew tolerance, as for the trunk cert
    not_after = now + timedelta(days=ttl_days)

    subject = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, f"glp-quick device {device_label}")])
    cert = (
        x509.CertificateBuilder()
        .subject_name(subject)
        .issuer_name(trunk_cert.subject)  # issued BY the trunk, not self-signed
        .public_key(device_key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(not_before)
        .not_valid_after(not_after)
        .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
        .add_extension(
            x509.KeyUsage(
                digital_signature=True,
                key_encipherment=True,
                content_commitment=False,
                data_encipherment=False,
                key_agreement=False,
                key_cert_sign=False,
                crl_sign=False,
                encipher_only=False,
                decipher_only=False,
            ),
            critical=True,
        )
        .add_extension(
            x509.ExtendedKeyUsage(
                [ExtendedKeyUsageOID.SERVER_AUTH, ExtendedKeyUsageOID.CLIENT_AUTH]
            ),
            critical=False,
        )
        .sign(private_key=trunk_key, algorithm=hashes.SHA256())
    )

    return DerivedCert(
        cert_pem=cert.public_bytes(serialization.Encoding.PEM).decode("ascii"),
        key_pem=device_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption(),
        ).decode("ascii"),
        spki_pin=spki_sha256_pin(cert),
        not_before=not_before,
        not_after=not_after,
        device_label=device_label,
    )


def verify_derived_against_trunk(cert_pem: str, cert_dir: Path) -> bool:
    """True iff ``cert_pem`` carries a valid signature from the trunk certificate's key.

    This is the signature check only — the acceptance seam additionally enforces the validity
    window and the revocation set (contracts/join-seam-contract.md).
    """
    from cryptography.exceptions import InvalidSignature

    derived = x509.load_pem_x509_certificate(cert_pem.encode("ascii"))
    trunk = x509.load_pem_x509_certificate(load_cert_pem(cert_dir))
    trunk_pub = trunk.public_key()
    try:
        if isinstance(trunk_pub, ec.EllipticCurvePublicKey):
            trunk_pub.verify(
                derived.signature,
                derived.tbs_certificate_bytes,
                ec.ECDSA(derived.signature_hash_algorithm),
            )
        else:
            from cryptography.hazmat.primitives.asymmetric import padding

            trunk_pub.verify(
                derived.signature,
                derived.tbs_certificate_bytes,
                padding.PKCS1v15(),
                derived.signature_hash_algorithm,
            )
    except InvalidSignature:
        return False
    return True


def load_pin(cert_dir: Path) -> str:
    """Read the pinned SPKI SHA-256 value written by :func:`generate_shared_cert`."""
    return (Path(cert_dir) / FINGERPRINT_NAME).read_text(encoding="ascii").strip()


def load_cert_pem(cert_dir: Path) -> bytes:
    """Read the distributed public cert (PEM)."""
    return (Path(cert_dir) / PEM_NAME).read_bytes()


def verify_peer_pin(presented_pin: str, expected_pin: str) -> bool:
    """Constant-time-ish equality of two SPKI pins (the trust decision; FR-003).

    The data-plane callback waives only no-CA-chain + hostname mismatch and then requires this
    to be ``True``; it is **never** a blanket accept. A mismatch is a hard ``cert_mismatch``
    failure (FR-019).
    """
    return _consttime_eq(presented_pin.strip(), expected_pin.strip())


def _consttime_eq(a: str, b: str) -> bool:
    import hmac

    return hmac.compare_digest(a, b)
