"""A virtual IBM-3270-style full-screen chat UX over the genuine QUIC+WS link (feature 036).

Block-mode (edit a screen, transmit on an AID/PF key). Prototype for the roadmap feature
``virtual-3270-term`` (full requirements: ``docs/roadmap-intake/virtual-3270-term.md``).

RDP-ROBUST COMMAND MODE (works when function keys are eaten by Remote Desktop): everything is doable
with only typing + Enter. End your input with a line that is just ``//`` then Enter to TRANSMIT. Type a
slash-command then ``//``+Enter to run it: ``/help /theme [name] /pages /new [name] /next /prev /goto N
/focus /quit``. Function keys still work where the terminal passes them:

  F1/`/help`  help · F2/`/theme`  theme · F9 (or Ctrl-X, or Alt-Enter, or `//`+Enter)  transmit ·
  Enter newline · arrows move · F6/`/new` new page · F7/F8 (`/prev`,`/next`) page · F10/`/pages` list ·
  Tab focus · F3 (or `/quit`) quit. Themes (F2): GREEN · AMBER · WHITE · PAPER · COLOR.
"""

from __future__ import annotations

import asyncio
import os
import threading
from pathlib import Path
from typing import Optional

from glp_quick.demo import _adapter
from glp_quick.repl_link import BROADCAST, GlpMessage
from glp_quick.terminal import pages as pagelib
from glp_quick.terminal import protocol
from glp_quick.terminal.state import TerminalState, compose_chat

_ART = r"""
   ____ _     ____         ___        _      _      _____ ____  _____ ___
  / ___| |   |  _ \       / _ \ _   _(_) ___| | __ |___ /___ \|___  / _ \
 | |  _| |   | |_) |_____| | | | | | | |/ __| |/ /   |_ \ __) |  / / | | |
 | |_| | |___|  __/_____| |_| | |_| | | (__|   <   ___) / __/  / /| |_| |
  \____|_____|_|         \__\_\\__,_|_|\___|_|\_\ |____/_____|/_/  \___/
     block-mode 3270 over genuine QUIC + WebSocket   ·   type /help then //  (Enter)
"""

_HELP = (
    "─── GLP-QUICK 3270 — HELP ───\n\n"
    "  BLOCK MODE: compose in the command area, then TRANSMIT.\n"
    "  TRANSMIT (any of):  a line that is just '//' then Enter  ·  F9  ·  Ctrl-X  ·  Alt-Enter\n"
    "  Enter = newline.  Arrow keys move the cursor.\n\n"
    "  COMMAND MODE (RDP-safe — only needs typing + Enter): type a command, then '//' + Enter:\n"
    "    /help            this help\n"
    "    /theme [name]    cycle, or set GREEN|AMBER|WHITE|PAPER|COLOR\n"
    "    /pages           list open pages + owners\n"
    "    /new [name]      new scratch page\n"
    "    /transmit [@peer] transmit the current page as an owned block to a peer\n"
    "    /next  /prev     switch page   ·   /goto N   go to page N\n"
    "    /focus           toggle focus (screen <-> command)\n"
    "    /quit            quit\n"
    "    /send <text>     send <text> as one message\n\n"
    "  Address a specific peer:  @<to> message   (default goes to the link peer).\n"
    "  Function keys also work where the terminal passes them: F1 help · F2 theme · F6 new ·\n"
    "  F7/F8 page · F9 send · F10 list · F3 quit (Ctrl alts: Ctrl-X send).\n\n"
    "  (Use /prev or F7 to return to your pages.)\n"
)


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

    stop = threading.Event()
    app_ref = [None]  # set after the app is built (for exit/style/invalidate from helpers)

    # The whole model — pages / unread / peers / OIA link-state + the loop-serialized receive-path
    # mutation seam — lives in TerminalState (host-free, unit-tested). This view is a thin wiring over
    # it (R1); received messages mutate through state.deliver so the receive path is race-free (FR-042).
    banner = _ART + f"\n*** link up as '{sid}' on {addr}:{port} ({stack}) — type /help then // ***\n"

    def _on_recv_change() -> None:
        # Runs on the event loop thread after each delivered inbound event (R4). Reflect the change
        # only when it hit the page in view (a received page for another page must NOT steal focus,
        # FR-010); always refresh the OIA (unread indicator / link-state).
        if state.last_changed_index == state.current:
            _show(state.current_page())
        if app_ref[0] is not None:
            app_ref[0].invalidate()

    state = TerminalState(
        sid, default_to, peers=handle.peers, initial_chat_text=banner, on_change=_on_recv_change
    )

    theme_idx = [0]
    THEMES = None  # set below; theme_idx used by helpers

    screen = Buffer(multiline=True)
    command = Buffer(multiline=True)
    screen.set_document(Document(state.pages[0].text, len(state.pages[0].text)), bypass_readonly=True)

    def _show(pg) -> None:
        screen.set_document(Document(pg.text, len(pg.text)), bypass_readonly=True)

    def _switch(i: int) -> None:
        state.save_current(screen.text)
        _show(state.load(i))

    def _echo(line: str) -> None:
        """Append a local line to CHAT and reflect it if CHAT is in view (else it raises unread)."""
        state.append_chat_line(line)
        if state.current == 0:
            _show(state.pages[0])

    # --- actions (called by both PF keys and slash-commands) ---
    def do_help() -> None:
        i = state.ensure_page("HELP"); state.pages[i].text = _HELP; _switch(i)

    def do_pages() -> None:
        i = state.ensure_page("PAGES")
        # Build the listing from the CURRENT page set (before switching to the PAGES page itself).
        _switch(i)
        state.pages[i].text = pagelib.list_text(state.pages, i)
        _show(state.pages[i])

    def do_new(name: Optional[str] = None) -> None:
        state.save_current(screen.text)
        i = state.add_page(name or f"SCRATCH{len(state.pages)}")
        _show(state.load(i))

    def do_transmit(target_arg: Optional[str] = None) -> None:
        # Transmit the current page as an owned block (FR-007). Pages are DIRECTED (never broadcast) —
        # resolve @peer against the live peer set; an unknown/absent target is reported (FR-040/inv#2).
        state.save_current(screen.text)
        pg = state.current_page()
        members = state.peers()
        if target_arg and target_arg.startswith("@"):
            target = target_arg[1:].strip()
            if target != BROADCAST and target not in members:
                _echo(f"?? unknown peer '{target}' — known peers: {', '.join(members) or '(none connected)'}")
                return
        else:
            target = default_to
        if target == BROADCAST:
            _echo("?? /transmit needs a specific peer (pages are directed): /transmit @name")
            return
        handle.send(GlpMessage(sender=sid, to=target, payload=protocol.page(pg.name, sid, pg.kind, pg.text)))
        _echo(f"[{sid}>{target}] transmitted page '{pg.name}' ({len(pg.text)} chars)")

    def do_nav(delta: int) -> None:
        _switch(state.current + delta)

    def do_goto(n: int) -> None:
        _switch(n - 1)

    def do_theme(name: Optional[str] = None) -> None:
        if name:
            for k, (nm, _st) in enumerate(THEMES):
                if nm.lower() == name.lower():
                    theme_idx[0] = k
                    break
        else:
            theme_idx[0] = (theme_idx[0] + 1) % len(THEMES)
        if app_ref[0] is not None:
            app_ref[0].style = THEMES[theme_idx[0]][1]
            app_ref[0].invalidate()

    def do_quit() -> None:
        if app_ref[0] is not None:
            app_ref[0].exit()

    def do_focus() -> None:
        app = app_ref[0]
        if app is None:
            return
        app.layout.focus(screen_ctrl if app.layout.current_control is command_ctrl else command_ctrl)

    def run_command(line: str) -> None:
        parts = line.strip().split()
        cmd, args = parts[0].lower(), parts[1:]
        if cmd in ("/help", "/h"):
            do_help()
        elif cmd in ("/theme", "/t"):
            do_theme(args[0] if args else None)
        elif cmd in ("/pages", "/p"):
            do_pages()
        elif cmd in ("/new",):
            do_new(args[0] if args else None)
        elif cmd in ("/next",):
            do_nav(+1)
        elif cmd in ("/prev",):
            do_nav(-1)
        elif cmd in ("/goto",):
            try:
                do_goto(int(args[0]))
            except (IndexError, ValueError):
                _echo("?? /goto needs a page number")
        elif cmd in ("/focus",):
            do_focus()
        elif cmd in ("/transmit", "/xmit"):
            do_transmit(args[0] if args else None)
        elif cmd in ("/quit", "/q", "/exit"):
            do_quit()
        elif cmd in ("/send",):
            _send_message(" ".join(args))
        else:
            _echo(f"?? unknown command: {cmd} (try /help)")

    def _send_message(text: str) -> None:
        # @name resolve against the live peer set + chat codec, shared with link_console (FR-040/FR-026).
        out = compose_chat(sid, default_to, text, handle.peers)
        if out.message is not None:
            handle.send(out.message)
        _echo(out.echo)

    def submit() -> None:
        # strip trailing sentinel/blank lines, then run as a command or send as a message.
        lines = command.text.split("\n")
        while lines and lines[-1].strip() in ("", "//", "///"):
            lines.pop()
        text = "\n".join(lines).strip("\n")
        command.reset()
        if not text.strip():
            return
        if text.lstrip().startswith("/") and "\n" not in text.strip():
            run_command(text.strip())
        else:
            _send_message(text)

    # --- themes ---
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

    def oia_text():
        pg = state.current_page()
        unread = pagelib.unread_names(state.pages, state.current)
        flag = f"  ●NEW:{','.join(unread)}" if unread else ""
        return [("class:oia",
                 f" BLOCK MODE  P{state.current + 1}/{len(state.pages)}:{pg.name}({pg.owner})  "
                 f"THEME:{THEMES[theme_idx[0]][0]}  {state.oia_link_label()}{flag}   "
                 f"TRANSMIT: '//'+Enter or F9 · /help · /quit ")]

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
        buf = event.current_buffer
        if buf is command and buf.document.current_line.strip() in ("//", "///"):
            submit()
        else:
            buf.insert_text("\n")

    @kb.add("f9")
    @kb.add("c-x")
    @kb.add("escape", "enter")  # Alt-Enter / Esc-then-Enter
    def _(event):
        submit()

    @kb.add("f1")
    def _(event):
        do_help()

    @kb.add("f2")
    def _(event):
        do_theme()

    @kb.add("f10")
    def _(event):
        do_pages()

    @kb.add("f8")
    def _(event):
        do_nav(+1)

    @kb.add("f7")
    def _(event):
        do_nav(-1)

    @kb.add("f6")
    def _(event):
        do_new()

    @kb.add("tab")
    def _(event):
        do_focus()

    @kb.add("f3")
    def _(event):
        do_quit()

    app = Application(layout=layout, key_bindings=kb, style=THEMES[0][1], full_screen=True, mouse_support=True)
    app_ref[0] = app

    async def recv_loop():
        loop = asyncio.get_running_loop()
        while not stop.is_set():
            try:
                msg = await loop.run_in_executor(None, lambda: handle.recv(timeout=0.3))
            except Exception as exc:  # a raised recv is a fault — surface it, never vanish (FR-043)
                if not stop.is_set():
                    state.report_fault((str(exc) or type(exc).__name__)[:80])
                return
            if stop.is_set():
                return
            if msg is not None:
                state.deliver(msg)  # decode + dispatch, serialized on the loop (FR-042)
                continue
            # recv returned None: an idle timeout OR a link close/fault — the return value alone cannot
            # tell them apart (csharp.recv maps both to None), so consult health (FR-043/FR-044).
            try:
                status = adapter.health(handle)
            except Exception:
                status = None
            if status is not None and (not status.alive or status.state in ("closed", "failed")):
                if not stop.is_set():
                    detail = status.detail or ""
                    if "FAULT" in detail or status.state == "failed":
                        state.report_fault(detail or "link_lost")
                    else:
                        state.deliver(None)  # graceful close
                return
            # else: an idle tick — keep listening.

    async def main():
        state.bind_loop(asyncio.get_running_loop())  # receive mutations serialize on this loop (R4)
        bg = asyncio.ensure_future(recv_loop())
        try:
            await app.run_async()
        finally:
            stop.set()
            bg.cancel()
            adapter.stop(handle)

    asyncio.run(main())
    return 0
