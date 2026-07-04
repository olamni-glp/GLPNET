"""REPL-in-a-page: bind a page to a live GLP REPL reached over the link (feature 040, US5; FR-016).

A ``/repl`` page carries a transcript of ``?- goal.`` prompts and the REPL's rendered results. Goals
travel as ``repl_goal`` messages over the one link; the hosting side evaluates them with a
:class:`glp_quick.repl_link.ReplBridge` (a spawned ``glp_repl`` process) and returns ``repl_result``.
:class:`ReplService` wraps the bridge with lazy start + spawn-failure capture, so a missing/failed REPL
renders an error on the page rather than crashing the terminal (FR-016). Host-free: the transcript
helpers are pure and the service accepts an injected fake bridge for unit tests.
"""

from __future__ import annotations

from typing import Optional

from glp_quick.repl_link import ReplBridge


def prompt_line(goal: str) -> str:
    return f"?- {goal.strip()}"


def _append(page, text: str) -> None:
    prefix = "" if (not page.text or page.text.endswith("\n")) else "\n"
    page.text += prefix + text.rstrip("\n") + "\n"


def append_goal(page, goal: str) -> None:
    """Append a ``?- goal.`` prompt line to a REPL page's transcript."""
    _append(page, prompt_line(goal))


def append_result(page, rendered: str) -> None:
    """Append the REPL's rendered result to a REPL page's transcript."""
    _append(page, rendered)


class ReplService:
    """Host-side REPL: lazily starts a :class:`ReplBridge` and evaluates goals.

    Inject a fake bridge (any object with ``start() -> bool``, ``evaluate(goal) -> str``, ``error``,
    and ``stop()``) for host-free tests; leave ``None`` to spawn the real ``glp_repl``.
    """

    def __init__(self, bridge: Optional[object] = None) -> None:
        self._bridge = bridge
        self._started = False
        self._start_error: Optional[str] = None

    def _ensure(self) -> None:
        if self._bridge is None:
            self._bridge = ReplBridge()
        if not self._started:
            self._started = True
            ok = self._bridge.start()
            if not ok:
                self._start_error = getattr(self._bridge, "error", None) or "repl failed to start"

    def evaluate(self, goal: str) -> str:
        """Evaluate one goal; a spawn failure yields an error rendering (never raises)."""
        self._ensure()
        if self._start_error is not None:
            return f"[repl unavailable: {self._start_error}]"
        return self._bridge.evaluate(goal)

    def stop(self) -> None:
        if self._bridge is not None:
            try:
                self._bridge.stop()
            except Exception:  # noqa: BLE001
                pass
