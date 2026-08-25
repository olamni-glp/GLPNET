"""``glp-quick provision`` sub-app (067, contracts/cli-contract.md).

Exit codes (contract surface):

===  ==========================================================================
  0  success (session redeemed; join written; revoke/audit ok)
  3  session expired
  4  session aborted
  5  bad_passphrase
  6  assembly error (message carries the payload-contract token)
  7  version_mismatch
 64  cert-dir or posture refusal
===  ==========================================================================
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Optional

import typer

from .. import cert as cert_mod
from . import DERIVED_DIRNAME, PostureError
from . import audit as audit_store
from .bundle import AssemblyError, TooManyChunks
from .crypto import BadPassphrase, MalformedEnvelope
from .join import join_from_chunks, parse_chunk_lines
from .pdf_render import render_non_secret_pdf
from .qr_render import render_page
from .session import DEFAULT_TTL_DAYS, DEFAULT_WINDOW_MINUTES, ProvisioningSession

provision_app = typer.Typer(
    no_args_is_help=True,
    help="QR-code link + cert provisioning (feature 067) — derived credentials only; the trunk "
    "key is never rendered.",
)


def _cert_dir_or_exit(cert: Path) -> Path:
    cert = Path(cert)
    if not cert.exists():
        typer.echo(f"error: --cert directory not found: {cert}", err=True)
        raise typer.Exit(64)
    try:
        cert_mod.load_pin(cert)
    except FileNotFoundError:
        typer.echo(
            f"error: --cert dir {cert} has no {cert_mod.FINGERPRINT_NAME}; "
            f"run `glp-quick cert generate --out {cert}` first",
            err=True,
        )
        raise typer.Exit(64)
    return cert


@provision_app.command("session")
def provision_session(
    label: str = typer.Option(..., "--label", help="Human device label (recorded in the audit)."),
    addr: str = typer.Option(..., "--addr", help="Link endpoint address the device should join."),
    port: int = typer.Option(..., "--port", help="Link endpoint UDP port."),
    cert: Path = typer.Option(Path("glpquick-cert"), "--cert", help="Shared-cert dir (trunk)."),
    window_min: int = typer.Option(DEFAULT_WINDOW_MINUTES, "--window-min"),
    ttl_days: int = typer.Option(DEFAULT_TTL_DAYS, "--ttl-days"),
) -> None:
    """Mint a derived credential and display it as encrypted QR codes (display-only)."""
    cert = _cert_dir_or_exit(cert)
    session = ProvisioningSession(
        cert_dir=cert,
        device_label=label,
        endpoint_host=addr,
        endpoint_port=port,
        window_minutes=window_min,
        ttl_days=ttl_days,
    )
    try:
        chunks = session.render()
    except TooManyChunks as exc:
        typer.echo(f"error: {exc}", err=True)
        raise typer.Exit(64)
    except PostureError as exc:
        typer.echo(f"error: provision_posture: {exc}", err=True)
        raise typer.Exit(64)

    assert session.passphrase is not None
    typer.echo(
        render_page(
            chunks,
            session_id=session.session_id,
            device_label=label,
            passphrase=session.passphrase,
            expires_at=session.expires_at.isoformat(),
        )
    )
    typer.echo(
        f"[provision] session {session.session_id} rendered; credential "
        f"{session.derived.spki_pin if session.derived else '?'} valid until "
        f"{session.derived.not_after.isoformat() if session.derived else '?'}"
    )
    typer.echo(
        "[provision] the codes above are display-only and were not written to disk; "
        "re-run this command to render a fresh session."
    )


@provision_app.command("pdf")
def provision_pdf(
    session_id: str = typer.Option(..., "--session"),
    out: Path = typer.Option(..., "--out"),
    addr: str = typer.Option(..., "--addr"),
    port: int = typer.Option(..., "--port"),
    label: str = typer.Option("", "--label"),
    cert: Path = typer.Option(Path("glpquick-cert"), "--cert"),
) -> None:
    """Generate the NON-SECRET printable hand-off page (endpoint + pin + instructions)."""
    cert = _cert_dir_or_exit(cert)
    try:
        written = render_non_secret_pdf(
            out,
            session_id=session_id,
            device_label=label,
            endpoint_host=addr,
            endpoint_port=port,
            trunk_spki_pin=cert_mod.load_pin(cert),
        )
    except PostureError as exc:
        audit_store.record_event(
            cert, "pdf_render", session_id=session_id, outcome="refused:provision_posture"
        )
        typer.echo(f"error: provision_posture: {exc}", err=True)
        raise typer.Exit(64)
    audit_store.record_event(cert, "pdf_render", session_id=session_id, device_label=label)
    typer.echo(f"[provision] non-secret hand-off written: {written}")


@provision_app.command("revoke")
def provision_revoke(
    fingerprint: str = typer.Option(..., "--fingerprint"),
    reason: str = typer.Option("", "--reason"),
    cert: Path = typer.Option(Path("glpquick-cert"), "--cert"),
) -> None:
    """Revoke an issued derived credential (refused at the join seam)."""
    cert = _cert_dir_or_exit(cert)
    already = audit_store.is_revoked(cert, fingerprint)
    audit_store.record_revocation(cert, fingerprint=fingerprint, reason=reason)
    audit_store.record_event(cert, "revoke", fingerprint=fingerprint)
    if already:
        typer.echo(f"[provision] {fingerprint} was already revoked; recorded again (idempotent)")
    else:
        typer.echo(f"[provision] revoked {fingerprint}")


@provision_app.command("audit")
def provision_audit(
    cert: Path = typer.Option(Path("glpquick-cert"), "--cert"),
    since: Optional[str] = typer.Option(None, "--since", help="ISO timestamp lower bound."),
    as_json: bool = typer.Option(False, "--json"),
) -> None:
    """Show the provisioning audit trail and per-credential status (read-only)."""
    cert = _cert_dir_or_exit(cert)
    rows = audit_store.read_audit(cert, since=since)
    status = audit_store.credential_status(cert)
    if as_json:
        import json

        typer.echo(json.dumps({"events": rows, "credentials": [s.__dict__ for s in status]}, indent=2))
        return
    typer.echo(f"[provision] {len(rows)} audit event(s)")
    for r in rows:
        typer.echo(f"  {r['ts']}  {r['event']:<10} {r.get('outcome','')}  {r.get('device_label') or ''}")
    typer.echo(f"[provision] {len(status)} issued credential(s)")
    for s in status:
        state = f"REVOKED {s.revoked_at}" if s.revoked else f"valid until {s.not_after}"
        typer.echo(f"  {s.fingerprint}  {s.device_label:<20} {state}")


@provision_app.command("join")
def provision_join(
    input_path: Path = typer.Option(..., "--input", help="File of scanned chunk lines, or - for stdin."),
    out_dir: Path = typer.Option(Path(DERIVED_DIRNAME), "--out-dir"),
    passphrase: Optional[str] = typer.Option(
        None, "--passphrase", help="Omit to be prompted (preferred — keeps it out of shell history)."
    ),
) -> None:
    """Assemble scanned chunks, decrypt with the one-time passphrase, write derived material."""
    text = sys.stdin.read() if str(input_path) == "-" else Path(input_path).read_text(encoding="utf-8")
    lines = parse_chunk_lines(text)
    if not lines:
        typer.echo("error: no GQP1 chunk lines found in the input", err=True)
        raise typer.Exit(6)
    if passphrase is None:
        passphrase = typer.prompt("one-time passphrase", hide_input=True)

    try:
        result = join_from_chunks(lines, passphrase, out_dir)
    except AssemblyError as exc:
        code = 7 if exc.token == "version_mismatch" else 6
        typer.echo(f"error: {exc.token}: {exc.detail}", err=True)
        raise typer.Exit(code)
    except MalformedEnvelope as exc:
        typer.echo(f"error: malformed_envelope: {exc}", err=True)
        raise typer.Exit(6)
    except BadPassphrase:
        typer.echo(
            "error: bad_passphrase — nothing was written; re-check the passphrase you were given "
            "out-of-band and try again",
            err=True,
        )
        raise typer.Exit(5)

    typer.echo(f"[provision] joined session {result.session_id}")
    typer.echo(f"[provision] derived material written to {result.out_dir}")
    typer.echo(f"[provision] endpoint {result.endpoint}, trunk pin {result.trunk_spki_pin}")
    typer.echo(f"[provision] start the client with: glp-quick --client --derived-dir {result.out_dir}")
