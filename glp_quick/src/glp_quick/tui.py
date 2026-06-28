"""A virtual IBM-3270-style full-screen chat UX over the genuine QUIC+WS link (feature 036).

Inspired by the IBM 3270 Information Display System: a **block-mode** terminal — you edit a whole
screen freely and *transmit* only on an AID / PF key (not char-by-char). Prototype for the roadmap
feature ``virtual-3270-term`` (full requirements: ``docs/roadmap-intake/virtual-3270-term.md``).

Keys: F1 help · F2 theme · F9 (or Ctrl-X) transmit · Enter newline · arrows move · F6 new page ·
F7/F8 prev/next page · F10 list pages · Tab focus screen/command · F3 (or Ctrl-C) quit.

Themes (F2 cycles): GREEN · AMBER · WHITE-on-black · PAPER (black-on-white) · COLOR.
Falls back to the plain line console when not on a TTY (handled by the CLI).
"""

from __future__ import annotations

import asyncio
import os
import threading
from pathlib import Path
from typing import Optional

from glp_quick.demo import _adapter
from glp_quick.repl_link import BROADCAST, GlpMessage

_ART = r"""
   ____ _     ____         ___        _      _      _____ ____  _____ ___
  / ___| |   |  _ \       / _ \ _   _(_) ___| | __ |___ /___ \|___  / _ \
 | |  _| |   | |_) |_____| | | | | | | |/ __| |/ /   |_ \ __) |  / / | | |
 | |_| | |___|  __/_____| |_| | |_| | | (__|   <   ___) / __/  / /| |_| |
  \____|_____|_|         \__\_\\__,_|_|\___|_|\_\ |____/_____|/_/  \___/
        block-mode 3270 over genuine QUIC + WebSocket   ·   F1 = help
"""


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
    from prompt_toolkit.application import Application
    from prompt_toolkit.buffer import Buffer
    from prompt_toolkit.document import Document
    from prompt_toolkit.key_binding import KeyBindings
    from prompt_toolkit.layout import Layout
    from prompt_toolkit.layout.containers import HSplit, Window
    from prompt_toolkit.layout.controls import BufferControl, FormattedTextControl
    from prompt_toolkit.layout.dimension import Dimension
    from prompt_toolkit.styles import Style

    adapter = _adapter(stack, profile)
    sid = self_id or os.environ.get("GLPQUICK_ID") or role
    default_to = BROADCAST if role == "server" else "server"
    try:
        cmd_lines = max(1, int(os.environ.get("GLPQUICK_CMDLINES", "3")))
    except ValueError:
        cmd_lines = 3

    if role == "server":
        handle = adapter.start_server(addr, port, cert, max_clients, repl)  # type: ignore[arg-type]
    else:
        handle = adapter.start_client(addr, port, cert, repl)  # type: ignore[arg-type]
        handle.send(GlpMessage(sender=sid, to="server", payload="__connected__"))

    # --- pages (block-mode screens). Page 0 = CHAT (the live link). owner: shared|me ---
    pages = [{"name": "CHAT", "owner": "shared",
              "text": _ART + f"\n*** link up as '{sid}' on {addr}:{port} ({stack}) — F1 for help ***\n"}]
    cur = [0]
    unread = [False]
    stop = threading.Event()

    screen = Buffer(multiline=True)
    command = Buffer(multiline=True)
    screen.set_document(Document(pages[0]["text"], len(pages[0]["text"])), bypass_readonly=True)

    def _save_current() -> None:
        pages[cur[0]]["text"] = screen.text

    def _load(i: int) -> None:
        cur[0] = i % len(pages)
        if cur[0] == 0:
            unread[0] = False
        screen.set_document(Document(pages[cur[0]]["text"], len(pages[cur[0]]["text"])), bypass_readonly=True)

    def _ensure_page(name: str, owner: str = "me") -> int:
        for idx, pg in enumerate(pages):
            if pg["name"] == name:
                return idx
        pages.append({"name": name, "owner": owner, "text": ""})
        return len(pages) - 1

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

    # --- themes (F2 cycles) ---
    def _mk(scr_fg, scr_bg, hdr_fg, hdr_bg, oia_fg, oia_bg, cmd_fg):
        return Style.from_dict({
            "": f"bg:{scr_bg} {scr_fg}",
            "header": f"bg:{hdr_bg} {hdr_fg} bold",
            "oia": f"bg:{oia_bg} {oia_fg}",
            "sep": oia_fg,
            "command": cmd_fg,
        })
    THEMES = [
        ("GREEN", _mk("#33ff33", "#000000", "#33ff33", "#003300", "#00cc00", "#001a00", "#cc66ff")),
        ("AMBER", _mk("#ffb000", "#000000", "#ffd060", "#332200", "#ff9000", "#1a1000", "#cc66ff")),
        ("WHITE", _mk("#d0d0d0", "#000000", "#ffffff", "#202020", "#a0a0a0", "#101010", "#cc66ff")),
        ("PAPER", _mk("#101010", "#c8c8c8", "#000000", "#9a9a9a", "#202020", "#b0b0b0", "#7a00aa")),
        ("COLOR", _mk("#c8d8ff", "#000018", "#ffffff", "#0000aa", "#00ddff", "#001030", "#ff66cc")),
    ]
    theme_idx = [0]

    def oia_text():
        pg = pages[cur[0]]
        flag = "  ●CHAT" if unread[0] else ""
        legend = "F1 help·F2 theme·F9 SEND·F7/8 page·F6 new·F10 list·TAB focus·F3 quit"
        return [("class:oia",
                 f" BLOCK MODE  P{cur[0] + 1}/{len(pages)}:{pg['name']}({pg['owner']})  "
                 f"THEME:{THEMES[theme_idx[0]][0]}{flag}   {legend} ")]

    def header_text():
        return [("class:header", f" GLP-QUICK 3270   {role.upper()} '{sid}'   link {addr}:{port} ({stack})   ")]

    screen_ctrl = BufferControl(buffer=screen, focusable=True)
    command_ctrl = BufferControl(buffer=command, focusable=True)
    body = HSplit([
        Window(FormattedTextControl(header_text), height=1, style="class:header"),
        Window(screen_ctrl, wrap_lines=True),
        Window(height=1, char="─", style="class:sep"),
        Window(FormattedTextControl(oia_text), height=1, style="class:oia"),
        Window(command_ctrl, height=Dimension(min=cmd_lines, max=cmd_lines + 3),
               wrap_lines=True, style="class:command"),
    ])
    layout = Layout(body, focused_element=command_ctrl)

    kb = KeyBindings()

    @kb.add("enter")
    def _(event):
        event.current_buffer.insert_text("\n")

    @kb.add("f9")
    @kb.add("c-x")
    def _(event):
        transmit()

    @kb.add("f1")
    @kb.add("c-g")
    def _(event):
        i = _ensure_page("HELP")
        pages[i]["text"] = (
            "─── GLP-QUICK 3270 — HELP ───\n\n"
            "  Block mode: edit the command area, press F9 to TRANSMIT the whole block.\n"
            "  Enter = newline · arrows move the cursor.\n\n"
            "  F1  this help          F2  cycle theme (GREEN/AMBER/WHITE/PAPER/COLOR)\n"
            "  F9  transmit           Enter  newline   arrows  move cursor\n"
            "  F6  new scratch page   F7/F8  prev/next page   F10  list pages\n"
            "  TAB switch focus (screen <-> command)   F3  quit\n\n"
            "  PF keys: press Fx directly (no modifier). Win+Fx is grabbed by Windows.\n"
            "  If a terminal swallows an F-key, use the Ctrl alternates:\n"
            "    Ctrl-X send · Ctrl-G help · Ctrl-T theme · Ctrl-L list ·\n"
            "    Ctrl-N/Ctrl-P next/prev page · Ctrl-O new page · Ctrl-C quit\n\n"
            "  Address a specific peer with '@<to> message'; default goes to "
            f"'{default_to}'.\n\n  (F7/F8 to return to your pages.)\n")
        _save_current(); _load(i)

    @kb.add("f2")
    @kb.add("c-t")
    def _(event):
        theme_idx[0] = (theme_idx[0] + 1) % len(THEMES)
        event.app.style = THEMES[theme_idx[0]][1]
        event.app.invalidate()

    @kb.add("f10")
    @kb.add("c-l")
    def _(event):
        i = _ensure_page("PAGES")
        listing = "─── OPEN PAGES ───\n\n" + "".join(
            f"  {n + 1:>2}. {pg['name']:<16} owner={pg['owner']}\n" for n, pg in enumerate(pages))
        pages[i]["text"] = listing + "\n  (F7/F8 to switch · F6 new page)\n"
        _save_current(); _load(i)

    @kb.add("f8")
    @kb.add("c-n")
    def _(event):
        _save_current(); _load(cur[0] + 1)

    @kb.add("f7")
    @kb.add("c-p")
    def _(event):
        _save_current(); _load(cur[0] - 1)

    @kb.add("f6")
    @kb.add("c-o")
    def _(event):
        _save_current(); pages.append({"name": f"SCRATCH{len(pages)}", "owner": "me", "text": ""}); _load(len(pages) - 1)

    @kb.add("tab")
    def _(event):
        cur_ctrl = event.app.layout.current_control
        event.app.layout.focus(screen_ctrl if cur_ctrl is command_ctrl else command_ctrl)

    @kb.add("f3")
    @kb.add("c-c")
    def _(event):
        event.app.exit()

    app = Application(layout=layout, key_bindings=kb, style=THEMES[0][1], full_screen=True, mouse_support=True)

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
