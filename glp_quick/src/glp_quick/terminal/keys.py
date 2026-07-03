"""PF-key bindings + the dynamic PF-legend (US3 default legend; US7 adds user-bindable free keys).

Host-free: the binding model and the legend text are plain data, so they are unit-testable (FR-045).
Every binding carries a **typed-command equivalent** (the RDP-safe invariant, FR-002/FR-020) which the
legend surfaces, so the terminal stays fully operable when function keys are eaten by Remote Desktop.
``tui`` renders :func:`legend_line` as small reverse-video blocks just above the command line (FR-025).
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional, Sequence, Tuple


@dataclass(frozen=True)
class KeyBinding:
    """A PF key bound to an action, with the typed command that does the same thing (RDP-safe)."""

    key: str            # e.g. "F1", "F9", "PF13"
    action: str         # human label, e.g. "help", "transmit"
    typed_equiv: str    # the slash-command (or "//+Enter"), e.g. "/help"
    legend_label: str   # short label for the legend block, e.g. "F1 help"
    ctrl_alt: str = ""  # a Ctrl fallback for a host-reserved key (FR-020), e.g. "C-e"


#: Every physical PF key: F1–F12 plus the 3270 PF13–PF24 (= Shift+F1..F12). FR-020.
ALL_PF_KEYS: List[str] = [f"F{i}" for i in range(1, 13)] + [f"PF{i}" for i in range(13, 25)]

#: Keys the host terminal commonly reserves (e.g. Windows Terminal F11 = fullscreen) → need a Ctrl
#: alternate so the binding stays reachable (FR-020).
RESERVED_KEYS = {"F11"}


def pf_for_shift(fn: int) -> str:
    """The 3270 PF key for ``Shift+F<fn>`` (Shift+F1 → PF13 … Shift+F12 → PF24)."""
    if not 1 <= fn <= 12:
        raise ValueError("shift F-key must be F1..F12")
    return f"PF{12 + fn}"


def ctrl_alternate(key: str) -> str:
    """A Ctrl fallback token for a reserved PF key (e.g. ``F11`` → ``C-e``). Empty for unreserved keys."""
    if key not in RESERVED_KEYS:
        return ""
    # F11 → Ctrl-E (a mnemonic, host-independent) — the legend shows it so the key stays reachable.
    return "C-e"


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


class BindingRegistry:
    """The live PF-key bindings: the built-in defaults plus the user's ``/bind`` bindings (US7).

    Only **free** (unassigned) PF keys are user-bindable, and **every** binding carries a typed-command
    equivalent so the action stays reachable when the function key does not arrive (FR-019/002). The
    legend reflects the current bindings live.
    """

    def __init__(self) -> None:
        self._defaults = list(DEFAULT_BINDINGS)
        self._assigned = {b.key for b in self._defaults}
        self._user: "dict[str, KeyBinding]" = {}

    def is_pf_key(self, key: str) -> bool:
        return key in ALL_PF_KEYS

    def is_free(self, key: str) -> bool:
        """A PF key is bindable iff it is a real PF key not already taken by a default or a user binding."""
        return key in ALL_PF_KEYS and key not in self._assigned and key not in self._user

    def bind(self, key: str, command: str, action: Optional[str] = None) -> Tuple[bool, str]:
        """Bind a free PF ``key`` to a typed ``command`` (its own typed equivalent). Returns ``(ok, msg)``.

        Rejected (reported) if ``key`` is not a PF key or is already assigned. A reserved key is bound
        with a Ctrl alternate so it stays reachable (FR-020).
        """
        key = key.upper()
        command = command.strip()
        if not command:
            return False, "a binding needs a typed command (its RDP-safe equivalent)"
        if not self.is_pf_key(key):
            return False, f"'{key}' is not a PF key (F1..F12 or PF13..PF24)"
        if not self.is_free(key):
            return False, f"'{key}' is already assigned — only free PF keys are bindable"
        act = action or command
        binding = KeyBinding(key, act, command, f"{key} {act}", ctrl_alt=ctrl_alternate(key))
        self._user[key] = binding
        return True, f"bound {key} → {command}" + (f" (or {binding.ctrl_alt})" if binding.ctrl_alt else "")

    def unbind(self, key: str) -> bool:
        return self._user.pop(key.upper(), None) is not None

    def user_binding(self, key: str) -> Optional[KeyBinding]:
        return self._user.get(key.upper())

    def all_bindings(self) -> List[KeyBinding]:
        return self._defaults + list(self._user.values())

    def legend(self) -> str:
        return legend_line(self.all_bindings())
