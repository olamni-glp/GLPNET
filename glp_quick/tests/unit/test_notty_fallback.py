"""T009 [US1] — no-TTY fallback under ``--tui`` (FR-005/FR-041/SC-003).

When ``--tui`` is requested but stdin/stdout is not an interactive terminal (piped / redirected /
background), the CLI must fall back to the plain line console instead of crashing — and any exception
from ``isatty`` itself must also fall back (the T017 hardening), never propagate.

The decision is factored into ``cli.decide_tui`` so it is unit-testable without launching a link.
"""

from __future__ import annotations

import sys

from glp_quick.cli import decide_tui


class _FakeStream:
    def __init__(self, tty):
        self._tty = tty

    def isatty(self):
        if isinstance(self._tty, BaseException):
            raise self._tty
        return self._tty


def test_no_tui_flag_never_uses_tui():
    use, notice = decide_tui(False)
    assert use is False and notice is None


def test_tui_with_real_tty_uses_tui(monkeypatch):
    monkeypatch.setattr(sys, "stdin", _FakeStream(True))
    monkeypatch.setattr(sys, "stdout", _FakeStream(True))
    use, notice = decide_tui(True)
    assert use is True and notice is None


def test_tui_without_tty_falls_back_with_notice(monkeypatch):
    monkeypatch.setattr(sys, "stdin", _FakeStream(False))
    monkeypatch.setattr(sys, "stdout", _FakeStream(True))
    use, notice = decide_tui(True)
    assert use is False
    assert notice and "line console" in notice


def test_tui_isatty_exception_falls_back_not_crash(monkeypatch):
    monkeypatch.setattr(sys, "stdin", _FakeStream(OSError("no fileno")))
    monkeypatch.setattr(sys, "stdout", _FakeStream(True))
    use, notice = decide_tui(True)
    assert use is False  # hardening (FR-041): any isatty error ⇒ fallback, never propagate
    assert notice and "line console" in notice


def test_link_console_parity_shares_the_tui_send_receive_codec():
    """T059 — verify (don't re-implement) that the no-TTY fallback shares the ``--tui`` behaviours:
    ``@name`` resolve on send + the ``chat`` codec on send/receive, so the two paths cannot drift
    (FR-005/FR-040/FR-026)."""
    import inspect

    from glp_quick import link_console

    src = inspect.getsource(link_console)
    assert "compose_chat" in src          # shared @name resolve + chat codec on send (from T017/T018)
    assert "decode(" in src               # the one codec on receive (T018)
    assert "parse_addressed" not in src   # the old raw @name path is gone — no drift with --tui
