"""QR-code link + cert provisioning (feature 067) — producer side.

One-scan provisioning for the glp-quick QUIC+WS mesh: the hub mints a short-lived, per-device,
trunk-signed derived certificate, encrypts it under a one-time passphrase, and renders it as
chunked QR codes on a display-only surface. A joining device scans, decrypts, and joins under the
036 manual-pin trust model (unchanged for existing endpoints).

**Security posture (spec 067, mandatory first-class scope — not waivable):**

1. The trunk cert/private key (``glpquick.pfx`` / ``glpquick.key``) is long-lived, unchangeable
   TRUNK credential material. It is **never rendered** — not in a QR, a PDF, a display page, a
   log, or an audit row.
2. What is provisioned is short-lived, per-device, revocable **derived** material.
3. Every secret-bearing payload is encrypted; the decryption secret travels out-of-band.
4. Secret-bearing images are never persisted — there is no code path that writes one.
5. Printed output carries non-secret material only.
6. Every render/issue/redeem/expiry/refusal/revocation is audited, and revocation works.
"""

from __future__ import annotations

from pathlib import Path

#: Sub-directory of the cert dir holding the append-only provisioning stores. Gitignored with the
#: trunk material; contains fingerprints/labels/timestamps only — never key material.
PROVISION_DIRNAME = "provision"

#: Where a joining host writes the derived material it acquired by scanning.
DERIVED_DIRNAME = "glpquick-derived"

AUDIT_NAME = "audit.jsonl"
REVOKED_NAME = "revoked.jsonl"
ISSUED_NAME = "issued.jsonl"


def provision_dir(cert_dir: Path) -> Path:
    """The provisioning store directory for ``cert_dir`` (created on demand by the writers)."""
    return Path(cert_dir) / PROVISION_DIRNAME


class PostureError(RuntimeError):
    """Raised when a caller asks for something the mandatory security posture forbids.

    This is a refusal, never a fallback: the caller is wrong, and the operation does not proceed
    in a degraded form (spec FR-002/FR-006).
    """
