"""Generate the published GQP1 decode test vectors (067 FR-011, payload-contract.md §5).

Run: ``python -m tests.vectors.provision.generate_vectors`` from ``glp_quick/`` (or execute this
file directly). Regenerating overwrites the vector JSON in place.

**Every byte here is synthetic and disposable.** The "device key" in the vectors is a throwaway
keypair minted at generation time for a self-signed cert that chains to nothing — no trunk
material, and nothing that has ever protected a real endpoint, is used or reachable. That is a
hard requirement: the vectors are published for an out-of-repo consumer to implement against
(olamnit-assistant `android-quick-link-endpoints`), so they are as public as documentation.

Determinism note: the vectors are **not** byte-reproducible across runs — the envelope embeds a
random scrypt salt and GCM nonce by construction (see crypto.py), which is exactly the property
that makes two provisioning renders of the same bundle differ. Re-running produces a fresh but
equally valid vector set. Consumers must verify decode behaviour, never fixture bytes.
"""

from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.x509.oid import NameOID

from glp_quick.provision.bundle import SLICE_LEN, Bundle, chunk
from glp_quick.provision.crypto import seal

VECTOR_DIR = Path(__file__).parent
PASSPHRASE = "amber-cinder-fathom-lichen-quartz-tundra"  # fixed for the vectors; never real


def _throwaway_bundle(session_id: str = "vec00001", pad: int = 0) -> Bundle:
    """A bundle over a freshly-minted, self-signed, throwaway keypair."""
    key = ec.generate_private_key(ec.SECP256R1())
    name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "GQP1 test vector - NOT REAL")])
    now = datetime.now(timezone.utc)
    cert = (
        x509.CertificateBuilder()
        .subject_name(name)
        .issuer_name(name)
        .public_key(key.public_key())
        .serial_number(x509.random_serial_number())
        .not_valid_before(now - timedelta(minutes=1))
        .not_valid_after(now + timedelta(days=1))
        .sign(private_key=key, algorithm=hashes.SHA256())
    )
    cert_pem = cert.public_bytes(serialization.Encoding.PEM).decode("ascii")
    key_pem = key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption(),
    ).decode("ascii")
    if pad:
        # Pad via a repeated comment line so the bundle spans multiple chunks deterministically.
        cert_pem = cert_pem + ("# vector padding\n" * pad)
    return Bundle(
        endpoint_host="198.51.100.7",  # TEST-NET-2, RFC 5737 — never routable
        endpoint_port=4433,
        trunk_spki_pin="dGVzdC12ZWN0b3ItcGluLW5vdC1hLXJlYWwtdHJ1c3QtYW5jaG9y",
        device_cert_pem=cert_pem,
        device_key_pem=key_pem,
        session_id=session_id,
    )


def _write(name: str, payload: dict) -> Path:
    path = VECTOR_DIR / name
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return path


def build_all() -> list[Path]:
    written: list[Path] = []

    single = chunk(seal(_throwaway_bundle("vec00001").to_json(), PASSPHRASE), bundle_id="vecsingl")
    written.append(
        _write(
            "v1_single_chunk.json",
            {
                "description": "One-chunk bundle; assembles and decrypts cleanly.",
                "chunks": single,
                "passphrase": PASSPHRASE,
                "expect": {"result": "ok", "session_id": "vec00001"},
            },
        )
    )

    multi_bundle = _throwaway_bundle("vec00002", pad=SLICE_LEN // 4)
    multi = chunk(seal(multi_bundle.to_json(), PASSPHRASE), bundle_id="vecmulti")
    written.append(
        _write(
            "v1_multi_chunk.json",
            {
                "description": (
                    "Multi-chunk bundle. `chunks_shuffled` MUST assemble to the same result as "
                    "`chunks` — assembly is order-independent."
                ),
                "chunks": multi,
                "chunks_shuffled": list(reversed(multi)),
                "passphrase": PASSPHRASE,
                "expect": {"result": "ok", "session_id": "vec00002", "chunk_count": len(multi)},
            },
        )
    )

    # Corrupt chunk 1's slice so its CRC no longer matches.
    corrupt = list(multi)
    parts = corrupt[0].split("|")
    parts[4] = ("X" if not parts[4].startswith("X") else "Y") + parts[4][1:]
    corrupt[0] = "|".join(parts)
    written.append(
        _write(
            "v1_corrupt_chunk.json",
            {
                "description": "Chunk 1's slice mutated; its CRC-32 no longer matches.",
                "chunks": corrupt,
                "passphrase": PASSPHRASE,
                "expect": {"result": "corrupt_chunk", "detail": "1"},
            },
        )
    )

    written.append(
        _write(
            "v1_missing_chunk.json",
            {
                "description": "Chunk 2 withheld; the consumer must name exactly which is missing.",
                "chunks": [c for c in multi if "|2/" not in c],
                "passphrase": PASSPHRASE,
                "expect": {"result": "missing", "detail": "2"},
            },
        )
    )

    written.append(
        _write(
            "v1_bad_version.json",
            {
                "description": "Leading tag is GQP9; a GQP1 consumer must fail fast, not best-effort parse.",
                "chunks": [c.replace("GQP1|", "GQP9|", 1) for c in single],
                "passphrase": PASSPHRASE,
                "expect": {"result": "version_mismatch"},
            },
        )
    )

    written.append(
        _write(
            "v1_bad_passphrase.json",
            {
                "description": (
                    "Well-formed chunks, wrong passphrase. Must fail the GCM tag check and yield "
                    "NO partial plaintext."
                ),
                "chunks": single,
                "passphrase": "wrong-wrong-wrong-wrong-wrong-wrong",
                "expect": {"result": "bad_passphrase"},
            },
        )
    )

    return written


if __name__ == "__main__":  # pragma: no cover - developer entry point
    for p in build_all():
        print(f"wrote {p.name}")
