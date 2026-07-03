"""PF-key bindings + the dynamic PF-legend (US3 default legend; US7 adds user-bindable free keys).

Host-free: the binding model and the legend text are plain data, so they are unit-testable (FR-045).
Every binding carries a **typed-command equivalent** (the RDP-safe invariant, FR-002/FR-020) which the
legend surfaces, so the terminal stays fully operable when function keys are eaten by Remote Desktop.
``tui`` renders :func:`legend_line` as small reverse-video blocks just above the command line (FR-025).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Sequence


@dataclass(frozen=True)
class KeyBinding:
    """A PF key bound to an action, with the typed command that does the same thing (RDP-safe)."""

    key: str            # e.g. "F1", "F9"
    action: str         # human label, e.g. "help", "transmit"
    typed_equiv: str    # the slash-command (or "//+Enter"), e.g. "/help"
    legend_label: str   # short label for the legend block, e.g. "F1 help"


#: The default (built-in) PF-key bindings shown in the legend (FR-020). Their typed equivalents mirror
#: the ``tui`` key handlers so the legend is always truthful.
DEFAULT_BINDINGS: List[KeyBinding] = [
    KeyBinding("F1", "help", "/help", "F1 help"),
    KeyBinding("F2", "theme", "/theme", "F2 theme"),
    KeyBinding("F6", "new page", "/new", "F6 new"),
    KeyBinding("F7", "prev page", "/prev", "F7 prev"),
    KeyBinding("F8", "next page", "/next", "F8 next"),
    KeyBinding("F9", "transmit", "//+Enter", "F9 xmit"),
    KeyBinding("F10", "list pages", "/pages", "F10 pages"),
    KeyBinding("F3", "quit", "/quit", "F3 quit"),
]


def legend_blocks(bindings: Sequence[KeyBinding] = DEFAULT_BINDINGS) -> List[str]:
    """One legend block per binding — each shows the key+action and its typed-command equivalent."""
    return [f" {b.legend_label}={b.typed_equiv} " for b in bindings]


def legend_line(bindings: Sequence[KeyBinding] = DEFAULT_BINDINGS) -> str:
    """The full legend strip (blocks joined by rules), rendered reverse-video by the view (FR-025)."""
    return "│".join(legend_blocks(bindings))
