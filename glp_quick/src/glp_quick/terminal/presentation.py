"""3270 presentation: colour themes, OIA status line, splash, and compose-layout config (US3).

Host-free and prompt_toolkit-free — themes are plain colour data, the OIA is a pure string builder, and
the layout is a plain config object — so all of it is unit-testable (FR-045). ``tui`` turns
:func:`to_style_dict` into a prompt_toolkit ``Style`` and drives the layout from :class:`LayoutConfig`.

- FR-021: at least GREEN / AMBER / WHITE / PAPER / COLOR themes; command lines carry a purple/magenta accent.
- FR-022: OIA shows mode · current page ``X/N : name(owner)`` · link state · (legend is a separate strip).
- FR-023: compose area is N command lines (default ~3) **or** a two-strip response/command layout.
- FR-024: an ASCII screen-art splash on startup.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Mapping

from glp_quick.terminal import pages as pagelib

#: ASCII screen-art splash shown on startup (FR-024).
SPLASH = r"""
   ____ _     ____         ___        _      _      _____ ____  _____ ___
  / ___| |   |  _ \       / _ \ _   _(_) ___| | __ |___ /___ \|___  / _ \
 | |  _| |   | |_) |_____| | | | | | | |/ __| |/ /   |_ \ __) |  / / | | |
 | |_| | |___|  __/_____| |_| | |_| | | (__|   <   ___) / __/  / /| |_| |
  \____|_____|_|         \__\_\\__,_|_|\___|_|\_\ |____/_____|/_/  \___/
     block-mode 3270 over genuine QUIC + WebSocket   ·   type /help then //  (Enter)
"""


@dataclass(frozen=True)
class Theme:
    """One colour theme. ``cmd_accent`` is the purple/magenta command-line accent required by FR-021."""

    name: str
    screen_fg: str
    screen_bg: str
    header_fg: str
    header_bg: str
    oia_fg: str
    oia_bg: str
    cmd_accent: str


#: The five selectable themes (FR-021). Every ``cmd_accent`` is a purple/magenta hue.
THEMES: List[Theme] = [
    Theme("GREEN", "#33ff33", "#000000", "#33ff33", "#003300", "#00cc00", "#001a00", "#cc66ff"),
    Theme("AMBER", "#ffb000", "#000000", "#ffd060", "#332200", "#ff9000", "#1a1000", "#cc66ff"),
    Theme("WHITE", "#d0d0d0", "#000000", "#ffffff", "#202020", "#a0a0a0", "#101010", "#cc66ff"),
    Theme("PAPER", "#101010", "#c8c8c8", "#000000", "#9a9a9a", "#202020", "#b0b0b0", "#7a00aa"),
    Theme("COLOR", "#c8d8ff", "#000018", "#ffffff", "#0000aa", "#00ddff", "#001030", "#ff66cc"),
]


def to_style_dict(t: Theme) -> Dict[str, str]:
    """Prompt_toolkit style dict for a theme. The command line + PF-legend carry the accent; the legend
    is reverse-video (FR-025)."""
    return {
        "": f"bg:{t.screen_bg} {t.screen_fg}",
        "header": f"bg:{t.header_bg} {t.header_fg} bold",
        "oia": f"bg:{t.oia_bg} {t.oia_fg}",
        "sep": t.oia_fg,
        "command": t.cmd_accent,
        "legend": f"{t.cmd_accent} reverse",
    }


def find_theme(name: str) -> int:
    """Index of the theme whose name matches ``name`` (case-insensitive), or ``-1``."""
    for i, t in enumerate(THEMES):
        if t.name.lower() == name.lower():
            return i
    return -1


def render_oia(state: Any, theme_name: str) -> str:
    """The OIA status line: mode · current page ``X/N : name(owner)`` · theme · link state · new-page
    indicator (FR-022). The PF-legend is a separate reverse-video strip (FR-025), not part of this line.
    """
    pg = state.current_page()
    unread = pagelib.unread_names(state.pages, state.current)
    flag = f"  ●NEW:{','.join(unread)}" if unread else ""
    return (
        f" BLOCK MODE  P{state.current + 1}/{len(state.pages)}:{pg.name}({pg.owner})  "
        f"THEME:{theme_name}  {state.oia_link_label()}{flag}   "
        f"TRANSMIT: '//'+Enter or F9 · /help · /quit "
    )


@dataclass
class LayoutConfig:
    """Compose-area configuration (FR-023): ``lines`` (N command lines) or ``two-strip``."""

    mode: str = "lines"          # "lines" | "two-strip"
    n_command_lines: int = 3

    @classmethod
    def from_env(cls, env: Mapping[str, str]) -> "LayoutConfig":
        raw = str(env.get("GLPQUICK_LAYOUT", "")).strip().lower()
        mode = "two-strip" if raw in ("two-strip", "two_strip", "twostrip", "two") else "lines"
        try:
            n = max(1, int(env.get("GLPQUICK_CMDLINES", "3")))
        except (ValueError, TypeError):
            n = 3
        return cls(mode=mode, n_command_lines=n)

    def apply_command(self, arg: str) -> str:
        """Parse a ``/layout …`` argument, mutate self, and return a user-facing status message."""
        a = (arg or "").strip().lower()
        if a in ("two-strip", "two_strip", "twostrip", "two"):
            self.mode = "two-strip"
            return "layout: two-strip (response strip above command strip)"
        parts = a.split()
        if parts and parts[0] == "lines":
            if len(parts) >= 2 and parts[1].isdigit():
                self.n_command_lines = max(1, int(parts[1]))
            self.mode = "lines"
            return f"layout: {self.n_command_lines} command lines"
        if a.isdigit():
            self.mode = "lines"
            self.n_command_lines = max(1, int(a))
            return f"layout: {self.n_command_lines} command lines"
        return "?? /layout [lines N | two-strip]"
