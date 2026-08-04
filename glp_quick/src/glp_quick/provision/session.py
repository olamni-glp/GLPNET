"""Provisioning session lifecycle (067 FR-005/FR-010, data-model.md).

``open → rendered → redeemed | expired | aborted``

A session is **single-redemption**: once redeemed (or expired) its payload is unusable. The
derived credential it issued keeps its own TTL — expiring the session does not expire the
credential, and redeeming it does not extend one.

The one-time passphrase lives on the in-memory session object only. It is displayed once beside
the QR codes and is never written to disk or to an audit row.
"""

from __future__ import annotations

import secrets
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from pathlib import Path

from ..cert import DerivedCert, derive_device_cert, load_pin
from . import audit as audit_store
from .bundle import Bundle, chunk, new_bundle_id
from .crypto import generate_passphrase, seal

DEFAULT_WINDOW_MINUTES = 10
DEFAULT_TTL_DAYS = 30

_SESSION_ID_LEN = 8
_B32 = "abcdefghijklmnopqrstuvwxyz234567"


def new_session_id() -> str:
    return "".join(secrets.choice(_B32) for _ in range(_SESSION_ID_LEN))


@dataclass
class ProvisioningSession:
    """One engineer-initiated act of provisioning one device."""

    cert_dir: Path
    device_label: str
    endpoint_host: str
    endpoint_port: int
    window_minutes: int = DEFAULT_WINDOW_MINUTES
    ttl_days: int = DEFAULT_TTL_DAYS

    session_id: str = field(default_factory=new_session_id)
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    state: str = "open"

    passphrase: str | None = field(default=None, repr=False)
    derived: DerivedCert | None = field(default=None, repr=False)
    chunks: list[str] = field(default_factory=list, repr=False)

    @property
    def expires_at(self) -> datetime:
        return self.created_at + timedelta(minutes=self.window_minutes)

    def is_expired(self, now: datetime | None = None) -> bool:
        return (now or datetime.now(timezone.utc)) >= self.expires_at

    def render(self) -> list[str]:
        """Mint the derived credential, build + encrypt the bundle, produce the chunk lines.

        Audits `issue` then `render`. The returned lines are display-only input — nothing here
        writes an image or the payload to disk.
        """
        if self.state != "open":
            raise RuntimeError(f"session {self.session_id} is {self.state}, cannot render")

        self.derived = derive_device_cert(
            self.cert_dir, device_label=self.device_label, ttl_days=self.ttl_days
        )
        audit_store.record_issued(
            self.cert_dir,
            fingerprint=self.derived.spki_pin,
            device_label=self.device_label,
            session_id=self.session_id,
            not_before=self.derived.not_before.isoformat(),
            not_after=self.derived.not_after.isoformat(),
        )
        audit_store.record_event(
            self.cert_dir,
            "issue",
            session_id=self.session_id,
            fingerprint=self.derived.spki_pin,
            device_label=self.device_label,
        )

        bundle = Bundle(
            endpoint_host=self.endpoint_host,
            endpoint_port=self.endpoint_port,
            trunk_spki_pin=load_pin(self.cert_dir),
            device_cert_pem=self.derived.cert_pem,
            device_key_pem=self.derived.key_pem,
            session_id=self.session_id,
        )
        self.passphrase = generate_passphrase()
        envelope = seal(bundle.to_json(), self.passphrase)
        self.chunks = chunk(envelope, bundle_id=new_bundle_id())
        self.state = "rendered"

        audit_store.record_event(
            self.cert_dir,
            "render",
            session_id=self.session_id,
            fingerprint=self.derived.spki_pin,
            device_label=self.device_label,
        )
        return self.chunks

    def redeem(self) -> None:
        """Mark the session redeemed — single-redemption (FR-010)."""
        if self.state == "redeemed":
            audit_store.record_event(
                self.cert_dir,
                "refuse",
                session_id=self.session_id,
                fingerprint=self.derived.spki_pin if self.derived else None,
                outcome="refused:session_replayed",
            )
            raise RuntimeError("session_replayed")
        if self.state != "rendered":
            raise RuntimeError(f"session {self.session_id} is {self.state}, cannot redeem")
        if self.is_expired():
            self.expire()
            raise RuntimeError("session_expired")
        self.state = "redeemed"
        audit_store.record_event(
            self.cert_dir,
            "redeem",
            session_id=self.session_id,
            fingerprint=self.derived.spki_pin if self.derived else None,
            device_label=self.device_label,
        )

    def expire(self) -> None:
        if self.state in ("redeemed", "expired", "aborted"):
            return
        self.state = "expired"
        self._forget_secrets()
        audit_store.record_event(
            self.cert_dir,
            "expire",
            session_id=self.session_id,
            fingerprint=self.derived.spki_pin if self.derived else None,
            device_label=self.device_label,
            outcome="expired",
        )

    def abort(self) -> None:
        """Operator cancel, or crash-recovery marking an interrupted session unusable."""
        if self.state in ("redeemed", "expired", "aborted"):
            return
        self.state = "aborted"
        self._forget_secrets()
        audit_store.record_event(
            self.cert_dir,
            "expire",
            session_id=self.session_id,
            fingerprint=self.derived.spki_pin if self.derived else None,
            device_label=self.device_label,
            outcome="aborted",
        )

    def _forget_secrets(self) -> None:
        """Drop the passphrase and rendered payload once the session can no longer be redeemed."""
        self.passphrase = None
        self.chunks = []
