"""A virtual IBM-3270-style full-screen chat UX over the genuine QUIC+WS link (feature 036).

Inspired by the IBM 3270 Information Display System: a **block-mode** terminal where you edit a whole
screen freely and *transmit* only when you press an AID / PF key (not char-by-char). Layout:

    ┌─ header ─────────────────────────────────────────────────────────────┐
    │ GLP-QUICK 3270   PAGE 1/N: CHAT   link=...                            │
    ├─ screen (big, scrollable, editable; PAGES switch with PF7/PF8) ───────┤
    │ << peer: ...                                                          │
    │ [me] ...                                                              │
    │ ... (you can move the cursor freely and type on any page) ...         │
    ├─ OIA (operator information area) ─────────────────────────────────────┤
    │ BLOCK MODE  PF7/8 page  PF6 new  PF9 SEND  TAB focus  PF3 quit         │
    ├─ command (3-line compose; Enter=newline, PF9=transmit) ───────────────┤
    │ > type here, multi-line, send only when you press PF9                 │
    └───────────────────────────────────────────────────────────────────────┘

The CHAT page (page 1) is the live link: incoming messages and your transmits land there. Extra pages
are local scratch screens you can write on. Falls back to the plain line console when not on a TTY.
"""

from __future__ import annotations

import asyncio
import os
import threading
from pathlib import Path
from typing import Optional

from glp_quick.demo import _adapter
from glp_quick.repl_link import BROADCAST, GlpMessage


def run_tui(
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
    # Lazy imports so the package works without prompt_toolkit on non-TUI paths.
    from prompt_toolkit.application import Application
    from prompt_toolkit.buffer import Buffer
    from prompt_toolkit.document import Document
    from prompt_toolkit.filters import has_focus
    from prompt_toolkit.key_binding import KeyBindings
    from prompt_toolkit.layout import Layout
    from prompt_toolkit.layout.containers import HSplit, Window
    from prompt_toolkit.layout.controls import BufferControl, FormattedTextControl
    from prompt_toolkit.layout.dimension import Dimension
    from prompt_toolkit.styles import Style

    adapter = _adapter(stack, profile)
    sid = self_id or os.environ.get("GLPQUICK_ID") or role
    default_to = BROADCAST if role == "server" else "server"

    if role == "server":
        handle = adapter.start_server(addr, port, cert, max_clients, repl)  # type: ignore[arg-type]
    else:
        handle = adapter.start_client(addr, port, cert, repl)  # type: ignore[arg-type]
        handle.send(GlpMessage(sender=sid, to="server", payload="__connected__"))

    # --- model: pages (block-mode screens). Page 0 = CHAT (the live link). ---
    pages = [{"name": "CHAT", "text": f"*** GLP-QUICK 3270 — link up as '{sid}' on {addr}:{port} ***\n"}]
    cur = [0]
    unread = [False]
    stop = threading.Event()

    screen = Buffer(multiline=True)   # the big editable/scrollable area (mirrors the current page)
    command = Buffer(multiline=True)  # the 3-line compose/command area
    screen.set_document(Document(pages[0]["text"], len(pages[0]["text"])), bypass_readonly=True)

    def _save_current() -> None:
        pages[cur[0]]["text"] = screen.text

    def _load(i: int) -> None:
        cur[0] = i % len(pages)
        if cur[0] == 0:
            unread[0] = False
        screen.set_document(Document(pages[cur[0]]["text"], len(pages[cur[0]]["text"])), bypass_readonly=True)

    def append_chat(line: str) -> None:
        pages[0]["text"] += line + "\n"
        if cur[0] == 0:
            screen.set_document(Document(pages[0]["text"], len(pages[0]["text"])), bypass_readonly=True)
        else:
            unread[0] = True

    def transmit() -> None:
        text = command.text.rstrip("\n")
        if not text.strip():
            return
        handle.send(GlpMessage(sender=sid, to=default_to, payload=text))
        append_chat(f"[{sid}] " + text.replace("\n", "\n      "))
        command.reset()

    # --- OIA (status line) ---
    def oia_text():
        pg = f"PAGE {cur[0] + 1}/{len(pages)}:{pages[cur[0]]['name']}"
        flag = "  ●CHAT-MSG" if unread[0] else ""
        return [("class:oia", f" BLOCK MODE   {pg}{flag}   PF7/8 page · PF6 new · PF9 SEND · TAB focus · PF3 quit ")]

    def header_text():
        return [("class:header", f" GLP-QUICK 3270   {role.upper()} '{sid}'   link {addr}:{port} ({stack})   ")]

    # --- layout ---
    screen_ctrl = BufferControl(buffer=screen, focusable=True)
    command_ctrl = BufferControl(buffer=command, focusable=True)
    body = HSplit([
        Window(FormattedTextControl(header_text), height=1, style="class:header"),
        Window(screen_ctrl, wrap_lines=True),  # the giant 3270 screen
        Window(height=1, char="─", style="class:sep"),
        Window(FormattedTextControl(oia_text), height=1, style="class:oia"),
        Window(command_ctrl, height=Dimension(min=3, max=6), wrap_lines=True, style="class:command"),
    ])
    layout = Layout(body, focused_element=command_ctrl)

    # --- key bindings (PF keys) ---
    kb = KeyBindings()

    @kb.add("enter")
    def _(event):
        event.current_buffer.insert_text("\n")  # block-mode: Enter is a newline, not transmit

    @kb.add("f9")
    def _(event):
        transmit()

    @kb.add("c-x")  # Ctrl-X also transmits (in case PF9 is intercepted by the terminal)
    def _(event):
        transmit()

    @kb.add("f8")
    def _(event):
        _save_current(); _load(cur[0] + 1)

    @kb.add("f7")
    def _(event):
        _save_current(); _load(cur[0] - 1)

    @kb.add("f6")
    def _(event):
        _save_current(); pages.append({"name": f"SCRATCH{len(pages)}", "text": ""}); _load(len(pages) - 1)

    @kb.add("tab")
    def _(event):
        cur_win = event.app.layout.current_control
        event.app.layout.focus(screen_ctrl if cur_win is command_ctrl else command_ctrl)

    @kb.add("f3")
    @kb.add("c-c")
    def _(event):
        event.app.exit()

    style = Style.from_dict({
        "header": "bg:#003300 #33ff33 bold",
        "oia": "bg:#001a00 #00cc00",
        "sep": "#006600",
        "command": "#aaffaa",
        "": "bg:#000000 #33ff33",  # green-on-black 3270 phosphor
    })

    app = Application(layout=layout, key_bindings=kb, style=style, full_screen=True, mouse_support=True)

    async def recv_loop():
        loop = asyncio.get_event_loop()
        while not stop.is_set():
            try:
                msg = await loop.run_in_executor(None, lambda: handle.recv(timeout=0.3))
            except Exception:
                return
            if msg is not None and msg.payload != "__connected__":
                append_chat(f"<< {msg.sender}: " + msg.payload.replace("\n", "\n   "))
                app.invalidate()

    async def main():
        bg = asyncio.ensure_future(recv_loop())
        try:
            await app.run_async()
        finally:
            stop.set()
            bg.cancel()
            adapter.stop(handle)

    asyncio.run(main())
    return 0
