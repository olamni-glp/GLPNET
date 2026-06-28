"""Interactive / file-driven `--server` / `--client` link console (FR-007/FR-008a).

Launches the chosen stack's endpoint over a genuine QUIC+WS link and gives the operator a duplex
console. Messages are sent from **either** stdin (interactive terminal) **or** an outbox file
(``GLPQUICK_OUTBOX`` — append a line to send; works across turns / non-interactive shells). Received
messages are printed (and appended to ``GLPQUICK_INBOX`` if set). The link stays alive until the
peer/link closes or Ctrl-C — it does **not** tear down on stdin EOF, so a one-shot/piped invocation
still receives the peer's replies.

On connect the endpoint **auto-announces** (an empty-payload envelope) so the mesh registers its id
immediately and the peer can address it before the operator types anything.

Input line grammar:
  ``<to> <payload>``   → send payload to endpoint `<to>` (or `broadcast`)
  ``<payload>``        → send to the default peer (client→`server`, server→`broadcast`)
"""

from __future__ import annotations

import os
import sys
import threading
import time
from pathlib import Path
from typing import Optional

from glp_quick.demo import _adapter
from glp_quick.repl_link import BROADCAST, GlpMessage


def run(
    role: str,
    stack: str,
    profile: str,
    addr: str,
    port: int,
    cert: Path,
    repl: str,
    max_clients: int = 3,
    self_id: Optional[str] = None,
) -> int:
    """Run a link console in the given role until the link closes / Ctrl-C. Returns an exit code."""
    adapter = _adapter(stack, profile)
    sid = self_id or os.environ.get("GLPQUICK_ID") or role  # "server" | "client" | custom
    outbox = os.environ.get("GLPQUICK_OUTBOX")
    inbox = os.environ.get("GLPQUICK_INBOX")

    if role == "server":
        handle = adapter.start_server(addr, port, cert, max_clients, repl)  # type: ignore[arg-type]
        default_to = BROADCAST
    else:
        handle = adapter.start_client(addr, port, cert, repl)  # type: ignore[arg-type]
        default_to = "server"

    print(f"[glp-quick] {role} '{sid}' linked on {addr}:{port} (stack={stack}). "
          f"send: stdin or append to GLPQUICK_OUTBOX={outbox or '(unset)'}; "
          f"type '<to> <payload>' or '<payload>' (default to={default_to}); Ctrl-C to quit.",
          file=sys.stderr, flush=True)

    # Auto-announce so the peer's mesh registers this id immediately (clients announce to the server).
    if role == "client":
        handle.send(GlpMessage(sender=sid, to="server", payload="__connected__"))

    stop = threading.Event()

    def _send(line: str) -> None:
        line = line.rstrip("\r\n")
        if not line.strip():
            return
        # Plain text goes to the default peer; "@<to> payload" addresses a specific endpoint/broadcast.
        if line.startswith("@"):
            head, _, rest = line[1:].partition(" ")
            to, payload = head, rest
        else:
            to, payload = default_to, line
        handle.send(GlpMessage(sender=sid, to=to, payload=payload))

    def printer() -> None:
        while not stop.is_set():
            try:
                msg = handle.recv(timeout=0.4)
            except Exception:
                return
            if msg is not None and msg.payload != "__connected__":
                line = f"{msg.sender} -> {msg.to}: {msg.payload}"
                print(line, flush=True)
                if inbox:
                    with open(inbox, "a", encoding="utf-8") as f:
                        f.write(line + "\n")

    def stdin_reader() -> None:
        try:
            for raw in sys.stdin:
                _send(raw)
        except Exception:
            pass  # EOF / closed stdin — the link stays alive via the printer + outbox

    def outbox_poller() -> None:
        if not outbox:
            return
        Path(outbox).touch()
        sent = 0
        while not stop.is_set():
            try:
                lines = Path(outbox).read_text(encoding="utf-8").splitlines()
            except OSError:
                lines = []
            if len(lines) > sent:
                for line in lines[sent:]:
                    _send(line)
                sent = len(lines)
            time.sleep(0.3)

    threading.Thread(target=printer, daemon=True).start()
    threading.Thread(target=stdin_reader, daemon=True).start()
    threading.Thread(target=outbox_poller, daemon=True).start()

    try:
        while not stop.is_set():
            try:
                if not adapter.health(handle).alive:
                    break
            except Exception:
                pass
            time.sleep(0.4)
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        adapter.stop(handle)
    return 0
