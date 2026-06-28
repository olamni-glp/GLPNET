"""Interactive `--server` / `--client` link console (FR-007/FR-008a).

Launches the chosen stack's endpoint over a genuine QUIC+WS link and gives the operator a simple
duplex console: lines typed on stdin are sent as GLP-message envelopes; messages received over the
link are printed. This is what makes a real **cross-host** run possible (server on host A, client on
host B/gavri), as opposed to the same-process `demo` harness.

Input line grammar (prototype):
  ``<to> <payload>``   → send payload to endpoint `<to>` (or `broadcast`)
  ``<payload>``        → send to the default peer (client→`server`, server→`broadcast`)
"""

from __future__ import annotations

import sys
import threading
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
    """Run an interactive link console in the given role until EOF/Ctrl-C. Returns an exit code."""
    adapter = _adapter(stack, profile)
    sid = self_id or role  # "server" | "client"

    if role == "server":
        handle = adapter.start_server(addr, port, cert, max_clients, repl)  # type: ignore[arg-type]
        default_to = BROADCAST
    else:
        handle = adapter.start_client(addr, port, cert, repl)  # type: ignore[arg-type]
        default_to = "server"

    print(f"[glp-quick] {role} linked on {addr}:{port} (stack={stack}); "
          f"type '<to> <payload>' or '<payload>' (default to={default_to}); Ctrl-C / EOF to quit.",
          file=sys.stderr, flush=True)

    stop = threading.Event()

    def printer() -> None:
        while not stop.is_set():
            try:
                msg = handle.recv(timeout=0.4)
            except Exception:
                return
            if msg is not None:
                print(f"{msg.sender} -> {msg.to}: {msg.payload}", flush=True)

    pump = threading.Thread(target=printer, daemon=True)
    pump.start()

    try:
        for raw in sys.stdin:
            line = raw.strip()
            if not line:
                continue
            parts = line.split(" ", 1)
            to, payload = (parts[0], parts[1]) if len(parts) == 2 else (default_to, parts[0])
            handle.send(GlpMessage(sender=sid, to=to, payload=payload))
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        adapter.stop(handle)
    return 0
