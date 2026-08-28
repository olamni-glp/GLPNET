"""Consumer intake on the joining host (067 US1, contracts/cli-contract.md).

Reads chunk lines (pasted, or decoded from captured images by any external scanner), assembles
them per the GQP1 contract, prompts for the one-time passphrase, decrypts, and writes the derived
material to ``glpquick-derived/``.

**Abort-safe**: nothing is written until the envelope has decrypted and verified. A device that
gives up mid-scan leaves no plaintext behind — the buffered chunks are ciphertext, useless
without the out-of-band passphrase.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from .bundle import Bundle, assemble
from .crypto import open_envelope

DEVICE_CERT_NAME = "device.pem"
DEVICE_KEY_NAME = "device.key"
TRUNK_PIN_NAME = "trunk.pin"
ENDPOINT_NAME = "endpoint"


@dataclass(frozen=True)
class JoinResult:
    out_dir: Path
    endpoint: str
    trunk_spki_pin: str
    session_id: str


def parse_chunk_lines(text: str) -> list[str]:
    """Extract GQP1 chunk lines from arbitrary pasted text, ignoring blanks and stray prose."""
    return [ln.strip() for ln in text.splitlines() if ln.strip().startswith("GQP1|")]


def join_from_chunks(chunk_lines: Iterable[str], passphrase: str, out_dir: Path) -> JoinResult:
    """Assemble → decrypt → write ``glpquick-derived/``.

    Raises :class:`~glp_quick.provision.bundle.AssemblyError` (carrying the contract token) or
    :class:`~glp_quick.provision.crypto.BadPassphrase`. Neither writes anything.
    """
    envelope = assemble(chunk_lines)
    plaintext = open_envelope(envelope, passphrase)  # raises BadPassphrase; no partial output
    bundle = Bundle.from_json(plaintext)

    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / DEVICE_CERT_NAME).write_text(bundle.device_cert_pem, encoding="ascii")
    key_path = out_dir / DEVICE_KEY_NAME
    key_path.write_text(bundle.device_key_pem, encoding="ascii")
    _restrict(key_path)
    (out_dir / TRUNK_PIN_NAME).write_text(bundle.trunk_spki_pin + "\n", encoding="ascii")
    (out_dir / ENDPOINT_NAME).write_text(
        f"{bundle.endpoint_host}:{bundle.endpoint_port}\n", encoding="ascii"
    )

    return JoinResult(
        out_dir=out_dir,
        endpoint=f"{bundle.endpoint_host}:{bundle.endpoint_port}",
        trunk_spki_pin=bundle.trunk_spki_pin,
        session_id=bundle.session_id,
    )


def _restrict(path: Path) -> None:
    """Best-effort owner-only permissions on the written private key.

    POSIX chmod is a no-op for access control on Windows (the supported platform here), so this
    is defence-in-depth, not the protection itself — the protection is that the file lives in a
    gitignored dir and the credential is short-lived and revocable.
    """
    try:
        path.chmod(0o600)
    except OSError:
        pass
