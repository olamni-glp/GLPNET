"""``glp-quick`` CLI — one tool hosts both roles + cert + demo (FR-007), identical across stacks (FR-010).

Authoritative surface: ``specs/036-http3-quic-ws-link/contracts/cli-contract.md``::

    glp-quick cert generate --out <dir> [--days 365]
    glp-quick --server --addr <ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--max-clients 3] [--repl csharp|dart]
    glp-quick --client --addr <server-ip|name> --port <udp> --cert <dir> [--stack csharp|gleam] [--repl csharp|dart]
    glp-quick demo   --addr <server-ip> --port <udp> --cert <dir> [--stack csharp|gleam] [--clients 3]

This is the **top-down skeleton + mocks** (FR-017): ``cert generate`` is fully wired (a standalone,
tested utility — T009); ``--server`` / ``--client`` / ``demo`` parse + validate their inputs and
describe the plan, but open **no** QUIC endpoint — the data-plane stacks are gated (T013/T013a) and
their behaviour lands in the user-story phases (US1 MVP onward). Skeleton runs are clearly labelled.
"""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Optional, Tuple

import typer

from glp_quick import cert as cert_mod
from glp_quick.stacks.base import GleamProfile, ReplKind, StackName

app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Genuine HTTP/3 (QUIC) + WebSocket channel-link to run GLP between GLP REPL endpoints (feature 036).",
)
cert_app = typer.Typer(no_args_is_help=True, help="Shared self-signed certificate operations (FR-003).")
app.add_typer(cert_app, name="cert")

_SKELETON = "[glp-quick:skeleton]"


def _validate_stack(stack: str) -> StackName:
    if stack not in ("csharp", "gleam"):
        raise typer.BadParameter("--stack must be 'csharp' or 'gleam'")
    return stack  # type: ignore[return-value]


def _validate_repl(repl: str) -> "tuple[ReplKind, Optional[str]]":
    """``csharp``/``dart`` select a REPL kind (the pre-063 surface); a filesystem path to a GLP
    REPL (.exe or .dll) selects the 063 US1 live bridge — the host spawns that REPL as a child
    and bridges its stdio over the link envelopes (contract C1). Returns ``(kind, path|None)``."""
    if repl in ("csharp", "dart"):
        return repl, None  # type: ignore[return-value]
    if repl.lower().endswith((".dll", ".exe")) or "/" in repl or "\\" in repl:
        p = Path(repl)
        # E2 finding cp-02: the path branch must admit only an existing regular .dll/.exe file —
        # a directory or extensionless slash-path is a refusal, never a spawn attempt.
        if not p.is_file() or p.suffix.lower() not in (".dll", ".exe"):
            raise typer.BadParameter(f"--repl path must be an existing .dll/.exe file: {repl}")
        return "csharp", str(p.resolve())
    raise typer.BadParameter("--repl must be 'csharp' | 'dart' | a path to a GLP REPL (.exe/.dll)")


def _validate_profile(stack: str, profile: Optional[str]) -> Optional[GleamProfile]:
    if profile is None:
        # Default gleam to the built, documented Profile A (side-process QUIC); Profile C
        # (in-process quicer) is opt-in via --profile c and may be build-blocked on a host.
        return "a" if stack == "gleam" else None
    if stack != "gleam":
        raise typer.BadParameter("--profile applies only to --stack gleam")
    if profile not in ("a", "c"):
        raise typer.BadParameter("--profile must be 'a' or 'c'")
    return profile  # type: ignore[return-value]


def decide_tui(want_tui: bool) -> Tuple[bool, Optional[str]]:
    """Decide whether to launch the full-screen ``--tui`` (FR-005/FR-041/SC-003).

    The full-screen block-mode UI needs a real interactive terminal. When ``--tui`` is requested but
    stdin/stdout is not a TTY (piped / redirected / background) — or ``isatty`` itself raises (a
    detached or exotic stream) — fall back to the plain line console instead of crashing. Returns
    ``(use_tui, notice_or_None)``; the caller prints the notice on the fallback.
    """
    if not want_tui:
        return False, None
    try:
        tty = bool(sys.stdin.isatty() and sys.stdout.isatty())
    except Exception:
        tty = False  # any isatty error ⇒ fall back, never propagate (FR-041 hardening)
    if tty:
        return True, None
    return False, "--tui: no interactive terminal detected; using the plain line console."


def _require_cert_dir(cert: Path) -> str:
    """Validate the shared-cert dir and return its pinned SPKI value, or fail clearly (FR-019)."""
    cert = Path(cert)
    if not cert.exists():
        raise typer.BadParameter(f"--cert directory not found: {cert}")
    try:
        return cert_mod.load_pin(cert)
    except FileNotFoundError:
        raise typer.BadParameter(
            f"--cert dir {cert} has no {cert_mod.FINGERPRINT_NAME}; run `glp-quick cert generate --out {cert}` first"
        )


# --------------------------------------------------------------------------------------
# cert generate  (fully wired — standalone tested utility, T009)
# --------------------------------------------------------------------------------------
@cert_app.command("generate")
def cert_generate(
    out: Path = typer.Option(..., "--out", help="Directory to write the shared cert/key/pin into."),
    days: int = typer.Option(365, "--days", help="Validity period in days.", min=1),
    key_type: str = typer.Option("ec", "--key-type", help="'ec' (P-256, default) or 'rsa' (2048)."),
) -> None:
    """Generate the shared self-signed cert + key; print the SPKI SHA-256 pin (FR-003)."""
    if key_type not in ("ec", "rsa"):
        raise typer.BadParameter("--key-type must be 'ec' or 'rsa'")
    result = cert_mod.generate_shared_cert(out, days=days, key_type=key_type)  # type: ignore[arg-type]
    typer.echo(f"shared cert written to {out}/")
    typer.echo(f"  public  : {result.pem_path.name}   (distribute out-of-band)")
    typer.echo(f"  private : {result.key_path.name}   (holder/server-side only)")
    typer.echo(f"  holder  : {result.pfx_path.name}   (PKCS#12 bundle)")
    typer.echo(f"  pin file: {result.fingerprint_path.name}")
    typer.echo("")
    typer.echo(f"SPKI SHA-256 pin (the ONLY trust anchor — pin this on every host):")
    typer.echo(f"  {result.spki_pin}")


# --------------------------------------------------------------------------------------
# demo  (skeleton — needs the data-plane stack; SC-001..SC-006 harness lands in US1/US2)
# --------------------------------------------------------------------------------------
@app.command("demo")
def demo(
    addr: str = typer.Option(..., "--addr", help="Server LAN IP (or machine name)."),
    port: int = typer.Option(..., "--port", help="UDP port.", min=1, max=65535),
    cert: Path = typer.Option(..., "--cert", help="Shared-cert directory."),
    stack: str = typer.Option("csharp", "--stack", help="csharp | gleam."),
    profile: Optional[str] = typer.Option(None, "--profile", help="gleam only: a | c."),
    clients: int = typer.Option(3, "--clients", help="Concurrent clients (≥3).", min=1),
) -> None:
    """LAN-IP conformance demo (SC-001..SC-006) over the genuine QUIC+WS link."""
    stack_ = _validate_stack(stack)
    profile_ = _validate_profile(stack_, profile) or "a"
    _require_cert_dir(cert)
    from glp_quick.demo import run_demo
    from glp_quick.stacks.csharp import LinkError

    try:
        report = run_demo(addr, port, cert, stack=stack_, clients=clients, profile=profile_)
    except (LinkError, NotImplementedError) as e:
        typer.echo(f"demo failed: {e}", err=True)
        raise typer.Exit(code=1)
    typer.echo(report.render())
    if not report.ok:
        raise typer.Exit(code=1)


# --------------------------------------------------------------------------------------
# top-level callback: --server / --client mode flags (no subcommand)
# --------------------------------------------------------------------------------------
@app.callback(invoke_without_command=True)
def main(
    ctx: typer.Context,
    server: bool = typer.Option(False, "--server", help="Run the server role."),
    client: bool = typer.Option(False, "--client", help="Run the client role."),
    addr: Optional[str] = typer.Option(None, "--addr", help="Server bind/target LAN IP or machine name."),
    port: Optional[int] = typer.Option(None, "--port", help="UDP port.", min=1, max=65535),
    cert: Optional[Path] = typer.Option(None, "--cert", help="Shared-cert directory."),
    stack: str = typer.Option("csharp", "--stack", help="csharp | gleam."),
    profile: Optional[str] = typer.Option(None, "--profile", help="gleam only: a | c."),
    max_clients: int = typer.Option(3, "--max-clients", help="Server: max concurrent clients (≥3).", min=1),
    repl: str = typer.Option("csharp", "--repl", help="GLP REPL to bridge: csharp | dart | a path to a GLP REPL exe/dll (live bridge, 063 US1)."),
    retry: bool = typer.Option(False, "--retry", help="Client: retry while server-not-ready."),
    tui: bool = typer.Option(False, "--tui", help="Virtual IBM-3270-style full-screen chat UX (block-mode, pages, PF keys)."),
) -> None:
    """Dispatch the role flags when no subcommand (``cert`` / ``demo``) is given."""
    if ctx.invoked_subcommand is not None:
        return  # a subcommand will handle the invocation
    if server and client:
        raise typer.BadParameter("choose exactly one of --server / --client")
    if not server and not client:
        typer.echo(ctx.get_help())
        raise typer.Exit(code=0)

    stack_ = _validate_stack(stack)
    profile_ = _validate_profile(stack_, profile) or "a"
    repl_, repl_path = _validate_repl(repl)
    if addr is None or port is None or cert is None:
        raise typer.BadParameter("--addr, --port and --cert are required for --server/--client")
    _require_cert_dir(cert)

    role = "server" if server else "client"
    from glp_quick.stacks.csharp import LinkError

    # FR-005 / SC-003: the full-screen --tui needs a real TTY; when stdin/stdout is not a terminal
    # (piped / redirected / isatty raising), fall back to the plain line console instead of crashing.
    use_tui, notice = decide_tui(tui)
    if notice:
        typer.echo(notice, err=True)

    try:
        if use_tui:
            if repl_path is not None:
                raise typer.BadParameter("--repl <path> (the live bridge) runs on the plain console, not --tui")
            from glp_quick.tui import run_tui
            code = run_tui(role, stack_, profile_, addr, port, cert, repl_, max_clients=max_clients)
        else:
            from glp_quick.link_console import run as run_link
            code = run_link(role, stack_, profile_, addr, port, cert, repl_, max_clients=max_clients,
                            repl_path=repl_path)
        raise typer.Exit(code=code)
    except LinkError as e:
        typer.echo(f"{role} failed: {e}", err=True)
        raise typer.Exit(code=1)
    except NotImplementedError as e:
        # E2 finding cp-07: the gleam adapters' loud --repl refusal must surface as a clean
        # CLI error, never an unhandled traceback.
        typer.echo(f"{role} refused: {e}", err=True)
        raise typer.Exit(code=1)


if __name__ == "__main__":  # pragma: no cover
    app()
