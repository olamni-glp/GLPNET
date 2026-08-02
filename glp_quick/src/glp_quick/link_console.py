"""Interactive / file-driven `--server` / `--client` link console (FR-007/FR-008a).

Launches the chosen stack's endpoint over a genuine QUIC+WS link and gives the operator a duplex
console. In an interactive terminal it uses **prompt_toolkit** so your input line stays pinned at the
bottom and incoming messages render cleanly **above** it (no garbling). In a non-interactive shell
(piped / background / file-driven) it falls back to a plain stdin reader + an outbox-file poller, so
the same entry point drives the multi-process demo and tests.

Send: type a line (interactive) or append to ``GLPQUICK_OUTBOX``. Receive: printed (and appended to
``GLPQUICK_INBOX`` if set). ``GLPQUICK_QUIET=1`` suppresses stdout printing (send-only; pair with a
``Get-Content -Wait`` tail of the inbox in another window).

Input grammar: plain text → the default peer (client→``server``, server→``broadcast``);
``@<to> payload`` → a specific endpoint / ``broadcast``.
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
from glp_quick.terminal.protocol import decode
from glp_quick.terminal.state import compose_chat


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
    repl_path: Optional[str] = None,
) -> int:
    """Run a link console in the given role until the link closes / Ctrl-C. Returns an exit code."""
    adapter = _adapter(stack, profile)
    sid = self_id or os.environ.get("GLPQUICK_ID") or role
    outbox = os.environ.get("GLPQUICK_OUTBOX")
    inbox = os.environ.get("GLPQUICK_INBOX")
    quiet = bool(os.environ.get("GLPQUICK_QUIET"))

    if role == "server":
        handle = adapter.start_server(addr, port, cert, max_clients, repl,  # type: ignore[arg-type]
                                      repl_path=repl_path, self_id=sid)
        default_to = BROADCAST
    else:
        handle = adapter.start_client(addr, port, cert, repl,  # type: ignore[arg-type]
                                      repl_path=repl_path, self_id=sid)
        default_to = "server"

    print(f"[glp-quick] {role} '{sid}' linked on {addr}:{port} (stack={stack}). "
          f"Type a message (or '@<to> msg'); Ctrl-C/Ctrl-D to quit. "
          f"OUTBOX={outbox or '(unset)'} INBOX={inbox or '(unset)'}",
          file=sys.stderr, flush=True)

    if role == "client":  # auto-announce so the peer's mesh registers this id immediately
        handle.send(GlpMessage(sender=sid, to="server", payload="__connected__"))

    stop = threading.Event()

    def _send(line: str) -> None:
        line = line.rstrip("\r\n")
        if not line.strip():
            return
        # Shared send-path: @name resolve against the live peer set + chat codec (FR-040/FR-026),
        # identical to the --tui path so the two cannot drift (parity checked by T059).
        out = compose_chat(sid, default_to, line, handle.peers)
        if out.message is None:
            emit(out.echo)  # unknown-peer / empty-body report — never a silent default-fallback
            return
        handle.send(out.message)

    # Decide on the interactive (prompt_toolkit) path vs the plain path.
    interactive = False
    try:
        interactive = sys.stdin.isatty() and sys.stdout.isatty()
    except Exception:
        interactive = False
    patch_stdout = None
    PromptSession = None
    if interactive:
        try:
            from prompt_toolkit import PromptSession as _PS
            from prompt_toolkit.patch_stdout import patch_stdout as _patch
            PromptSession, patch_stdout = _PS, _patch
        except ImportError:
            interactive = False

    def emit(text: str) -> None:
        if not quiet:
            print(text, flush=True)  # under patch_stdout this renders above the prompt
        if inbox:
            with open(inbox, "a", encoding="utf-8") as f:
                f.write(text + "\n")

    def printer() -> None:
        while not stop.is_set():
            try:
                msg = handle.recv(timeout=0.4)
            except Exception:
                return
            if msg is None or msg.payload == "__connected__":
                continue
            tm = decode(msg.payload)  # one codec on receive too (FR-026)
            if tm.kind == "chat":
                emit(f"<< {msg.sender}: {tm.fields[0] if tm.fields else ''}")
            else:
                emit(f"<< {msg.sender}: [{tm.kind}] {msg.payload}")  # surface non-chat, never swallow (R6)

    def outbox_poller() -> None:
        if not outbox:
            return
        Path(outbox).touch()
        sent = 0
        while not stop.is_set():
            try:
                lines = Path(outbox).read_text(encoding="utf-8", errors="replace").splitlines()
            except OSError:
                lines = []
            if len(lines) > sent:
                for line in lines[sent:]:
                    _send(line)
                sent = len(lines)
            time.sleep(0.3)

    threading.Thread(target=printer, daemon=True).start()
    threading.Thread(target=outbox_poller, daemon=True).start()

    try:
        if interactive:
            session = PromptSession()
            with patch_stdout():
                while not stop.is_set():
                    try:
                        _send(session.prompt("> "))
                    except (EOFError, KeyboardInterrupt):
                        break
        else:
            # Plain path: a stdin reader (EOF just ends it) + a keep-alive health loop.
            def stdin_reader() -> None:
                try:
                    for raw in sys.stdin:
                        _send(raw)
                except Exception:
                    pass
            threading.Thread(target=stdin_reader, daemon=True).start()
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
