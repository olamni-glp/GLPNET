"""ms-message CLI — originator / recipient / status / dlq surface.

Command contract: specs/063-wave-5-consolidated-captured-triad/contracts/
mesh-messaging-protocol.md ("CLI surface"). Scaffold (T001): the commands
are declared with their contracted options; behaviour lands per task —
originator T019, recipient T020, status T022, dlq T018. Until then each
command exits with a named not-implemented error (never a silent no-op).
"""

from __future__ import annotations

import typer

app = typer.Typer(
    name="ms-message",
    help="Durable first-hop mesh messaging: signal-then-fetch with WAL durability (feature 063 US2).",
    no_args_is_help=True,
)

dlq_app = typer.Typer(help="Inspect and re-drive dead letters.", no_args_is_help=True)
app.add_typer(dlq_app, name="dlq")


def _not_implemented(command: str, task: str) -> None:
    typer.echo(f"ms-message {command}: not implemented yet (lands in {task})", err=True)
    raise typer.Exit(code=2)


@app.command()
def originator(
    station: str = typer.Option(..., "--station", help="This node's ground-station id."),
    listen: str = typer.Option(None, "--listen", help="Endpoint to listen on (host:port)."),
    mailbox: str = typer.Option(None, "--mailbox", help="Mailbox/topic to accept content into."),
    to: str = typer.Option(None, "--to", help="First-hop target station id."),
    count: int = typer.Option(None, "--count", help="Drill mode: journal N generated messages."),
) -> None:
    """Accept content into a mailbox for a target; journal; signal reachable targets."""
    _not_implemented("originator", "T019")


@app.command()
def recipient(
    station: str = typer.Option(..., "--station", help="This node's ground-station id."),
    from_: str = typer.Option(..., "--from", help="Holder endpoint to receive signals from."),
) -> None:
    """Receive signals, fetch at own pace, print/store delivered messages, advance position."""
    _not_implemented("recipient", "T020")


@app.command()
def status() -> None:
    """Journal/position/gap/DLQ summary for the node."""
    _not_implemented("status", "T022")


@dlq_app.command("list")
def dlq_list() -> None:
    """List dead letters with their park reasons."""
    _not_implemented("dlq list", "T018")


@dlq_app.command("redrive")
def dlq_redrive() -> None:
    """Re-drive parked dead letters."""
    _not_implemented("dlq redrive", "T018")


if __name__ == "__main__":
    app()
