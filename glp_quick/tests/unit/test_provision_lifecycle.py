"""067 US2 lifecycle: revocation, re-provision, audit completeness (SC-004), replay refusal,
and the PROVISION_REDEEMED event-line consumption (T020/T021).

The C#-side enforcement (join-seam refusal ≤ 60 s, corrupt-file fail-closed) is covered by
``csharp/glp_link.tests/DerivedCredentialTests.cs``; this file covers the Python producer's
lifecycle stores and session state machine.
"""

from __future__ import annotations

import pytest

from glp_quick.cert import generate_shared_cert
from glp_quick.provision import audit as audit_store
from glp_quick.provision.session import ProvisioningSession


@pytest.fixture()
def cert_dir(tmp_path):
    d = tmp_path / "glpquick-cert"
    generate_shared_cert(d, days=30)
    return d


def _session(cert_dir, label="lifecycle-device"):
    return ProvisioningSession(
        cert_dir=cert_dir, device_label=label, endpoint_host="10.0.0.5", endpoint_port=4433
    )


def test_revoke_marks_credential_and_is_idempotent(cert_dir):
    s = _session(cert_dir)
    s.render()
    fp = s.derived.spki_pin

    assert not audit_store.is_revoked(cert_dir, fp)
    audit_store.record_revocation(cert_dir, fingerprint=fp, reason="device lost")
    assert audit_store.is_revoked(cert_dir, fp)

    # Idempotent by design: a re-revoke is another append-only row, never an error.
    audit_store.record_revocation(cert_dir, fingerprint=fp, reason="again")
    assert audit_store.is_revoked(cert_dir, fp)


def test_reprovision_after_revoke_yields_unrevoked_credential(cert_dir):
    old = _session(cert_dir, label="lost-laptop")
    old.render()
    audit_store.record_revocation(cert_dir, fingerprint=old.derived.spki_pin)

    fresh = _session(cert_dir, label="replacement-laptop")
    fresh.render()
    assert fresh.derived.spki_pin != old.derived.spki_pin
    assert not audit_store.is_revoked(cert_dir, fresh.derived.spki_pin)
    assert audit_store.is_revoked(cert_dir, old.derived.spki_pin)  # the old one stays dead


def test_audit_answers_sc004_alone(cert_dir):
    """SC-004: which devices were provisioned, when, by whom, and which revoked — from the
    stores alone, no other source consulted."""
    a = _session(cert_dir, label="device-a")
    a.render()
    a.redeem()
    b = _session(cert_dir, label="device-b")
    b.render()
    audit_store.record_revocation(cert_dir, fingerprint=b.derived.spki_pin, reason="test")
    audit_store.record_event(cert_dir, "revoke", fingerprint=b.derived.spki_pin)

    status = {s.fingerprint: s for s in audit_store.credential_status(cert_dir)}
    assert set(status) == {a.derived.spki_pin, b.derived.spki_pin}
    assert status[a.derived.spki_pin].device_label == "device-a"
    assert not status[a.derived.spki_pin].revoked
    assert status[b.derived.spki_pin].revoked
    assert status[b.derived.spki_pin].revoked_at is not None
    for st in status.values():
        assert st.not_before and st.not_after and st.session_id  # when + linkage

    events = {r["event"] for r in audit_store.read_audit(cert_dir)}
    assert {"issue", "render", "redeem", "revoke"} <= events
    assert all(r["actor"] for r in audit_store.read_audit(cert_dir))  # by whom


def test_second_redeem_is_replay_refused_and_audited(cert_dir):
    s = _session(cert_dir)
    s.render()
    s.redeem()
    with pytest.raises(RuntimeError, match="session_replayed"):
        s.redeem()
    refusals = [r for r in audit_store.read_audit(cert_dir) if r["event"] == "refuse"]
    assert refusals and refusals[-1]["outcome"] == "refused:session_replayed"


def test_observe_event_line_redeems_matching_session(cert_dir):
    s = _session(cert_dir)
    s.render()
    fp = s.derived.spki_pin

    assert not s.observe_event_line("CLIENT_UP quic://whatever (1/3)")       # not the event
    assert not s.observe_event_line("PROVISION_REDEEMED someOtherPin=")      # not this session
    assert s.state == "rendered"

    assert s.observe_event_line(f"PROVISION_REDEEMED {fp}")
    assert s.state == "redeemed"

    # A duplicate observation of an already-settled session is ignored, not an error.
    assert not s.observe_event_line(f"PROVISION_REDEEMED {fp}")
    assert s.state == "redeemed"


def test_observe_event_line_ignores_before_render(cert_dir):
    s = _session(cert_dir)
    assert not s.observe_event_line("PROVISION_REDEEMED anything=")  # no credential minted yet
    assert s.state == "open"
