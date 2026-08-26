"""Append-only provisioning stores: audit, issued index, revocation (067 FR-008/FR-009).

Three JSONL files under ``glpquick-cert/provision/`` (gitignored with the trunk material):

- ``audit.jsonl``   — every render/issue/redeem/expire/refuse/revoke/pdf_render event
- ``issued.jsonl``  — index of issued derived credentials (fingerprint, label, window, session)
- ``revoked.jsonl`` — revocation records, consulted at the join seam

**These files never contain key material, passphrases, or payload bytes** — only fingerprints,
labels, timestamps, and outcomes. That is enforced by :func:`_assert_no_secrets`, which is a
refusal (not a filter): a caller trying to log a secret is a bug in the caller.

Append-only by construction: rows are only ever appended, never rewritten or deleted. Revocation
is a new row, not a mutation of the issuance row.
"""

from __future__ import annotations

import getpass
import json
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterator

from . import AUDIT_NAME, ISSUED_NAME, REVOKED_NAME, PostureError, provision_dir

#: Substrings that must never reach a persisted row. PEM headers cover cert/key material; the
#: passphrase field name catches an accidental structured leak.
_SECRET_MARKERS = ("-----BEGIN", "PRIVATE KEY", "passphrase")

EVENTS = (
    "render",
    "issue",
    "redeem",
    "expire",
    "refuse",
    "revoke",
    "pdf_render",
)


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _actor() -> str:
    """The acting identity as known to the host (067 assumption: no new identity system)."""
    try:
        return getpass.getuser()
    except Exception:  # pragma: no cover - getuser is robust on both supported platforms
        return os.environ.get("USERNAME") or os.environ.get("USER") or "unknown"


def _assert_no_secrets(row: dict[str, Any]) -> None:
    blob = json.dumps(row)
    for marker in _SECRET_MARKERS:
        if marker in blob:
            raise PostureError(
                f"refusing to persist a provisioning row containing {marker!r} — audit rows carry "
                f"fingerprints, labels and timestamps only (067 FR-002/FR-008)"
            )


def _append(path: Path, row: dict[str, Any]) -> None:
    _assert_no_secrets(row)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(row, sort_keys=True) + "\n")


def _read(path: Path) -> Iterator[dict[str, Any]]:
    if not path.exists():
        return
    with path.open("r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line:
                yield json.loads(line)


def record_event(
    cert_dir: Path,
    event: str,
    *,
    session_id: str | None = None,
    fingerprint: str | None = None,
    device_label: str | None = None,
    outcome: str = "ok",
) -> dict[str, Any]:
    """Append one audit row. Every provisioning-relevant act goes through here (FR-008)."""
    if event not in EVENTS:
        raise ValueError(f"unknown audit event {event!r}; expected one of {EVENTS}")
    row = {
        "ts": _now(),
        "event": event,
        "actor": _actor(),
        "session_id": session_id,
        "fingerprint": fingerprint,
        "device_label": device_label,
        "outcome": outcome,
    }
    _append(provision_dir(cert_dir) / AUDIT_NAME, row)
    return row


def read_audit(cert_dir: Path, since: str | None = None) -> list[dict[str, Any]]:
    rows = list(_read(provision_dir(cert_dir) / AUDIT_NAME))
    if since:
        rows = [r for r in rows if r.get("ts", "") >= since]
    return rows


def record_issued(
    cert_dir: Path,
    *,
    fingerprint: str,
    device_label: str,
    session_id: str,
    not_before: str,
    not_after: str,
) -> None:
    _append(
        provision_dir(cert_dir) / ISSUED_NAME,
        {
            "fingerprint": fingerprint,
            "device_label": device_label,
            "session_id": session_id,
            "not_before": not_before,
            "not_after": not_after,
            "issued_at": _now(),
        },
    )


def read_issued(cert_dir: Path) -> list[dict[str, Any]]:
    return list(_read(provision_dir(cert_dir) / ISSUED_NAME))


def record_revocation(cert_dir: Path, *, fingerprint: str, reason: str = "") -> dict[str, Any]:
    """Append a revocation row (FR-009). Idempotent by design: re-revoking is another row."""
    row = {
        "fingerprint": fingerprint,
        "revoked_at": _now(),
        "actor": _actor(),
        "reason": reason,
    }
    _append(provision_dir(cert_dir) / REVOKED_NAME, row)
    return row


def read_revoked(cert_dir: Path) -> set[str]:
    """The revocation set — fingerprints refused at the join seam."""
    return {r["fingerprint"] for r in _read(provision_dir(cert_dir) / REVOKED_NAME) if "fingerprint" in r}


def is_revoked(cert_dir: Path, fingerprint: str) -> bool:
    return fingerprint in read_revoked(cert_dir)


@dataclass(frozen=True)
class CredentialStatus:
    """Operator-facing view joining the issued index with the revocation set (SC-004)."""

    fingerprint: str
    device_label: str
    session_id: str
    not_before: str
    not_after: str
    revoked: bool
    revoked_at: str | None


def credential_status(cert_dir: Path) -> list[CredentialStatus]:
    """Answer "which devices were provisioned, when, and which are revoked" from the stores."""
    revocations = {
        r["fingerprint"]: r.get("revoked_at")
        for r in _read(provision_dir(cert_dir) / REVOKED_NAME)
        if "fingerprint" in r
    }
    out = []
    for row in read_issued(cert_dir):
        fp = row["fingerprint"]
        out.append(
            CredentialStatus(
                fingerprint=fp,
                device_label=row.get("device_label", ""),
                session_id=row.get("session_id", ""),
                not_before=row.get("not_before", ""),
                not_after=row.get("not_after", ""),
                revoked=fp in revocations,
                revoked_at=revocations.get(fp),
            )
        )
    return out
