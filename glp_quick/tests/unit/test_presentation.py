"""T024 [US3] — presentation + PF-legend (presentation.py, keys.py).

FR-021 (5 distinct themes; purple/magenta command accent), FR-022 (OIA fields), FR-023 (N-line vs
two-strip layout config), FR-025 (legend blocks carry their typed-command equivalent).
"""

from __future__ import annotations

from glp_quick.terminal import keys as K
from glp_quick.terminal import presentation as pres
from glp_quick.terminal.state import TerminalState


def _rgb(h: str):
    h = h.lstrip("#")
    return int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)


# --- themes (FR-021) ---------------------------------------------------------------------
def test_five_distinct_named_themes_at_minimum():
    names = [t.name for t in pres.THEMES]
    assert {"GREEN", "AMBER", "WHITE", "PAPER", "COLOR"} <= set(names)
    assert len(names) == len(set(names)) >= 5


def test_every_theme_command_accent_is_purple_magenta():
    for t in pres.THEMES:
        r, g, b = _rgb(t.cmd_accent)
        # purple/magenta ⇒ red and blue both dominate green
        assert r > g and b > g, f"{t.name} accent {t.cmd_accent} is not purple/magenta"


def test_style_dict_has_the_expected_classes_and_reverse_legend():
    d = pres.to_style_dict(pres.THEMES[0])
    assert set(d) >= {"", "header", "oia", "sep", "command", "legend"}
    assert "reverse" in d["legend"]  # FR-025 reverse-video legend blocks
    assert pres.THEMES[0].cmd_accent in d["command"]


def test_find_theme_by_name_case_insensitive():
    assert pres.find_theme("amber") == 1
    assert pres.find_theme("nope") == -1


# --- OIA (FR-022) ------------------------------------------------------------------------
def test_render_oia_reports_mode_page_owner_and_link():
    st = TerminalState("me", "server")
    st.add_page("PLAN")
    st.load(1)
    line = pres.render_oia(st, "GREEN")
    assert "BLOCK MODE" in line
    assert "P2/2:PLAN(me)" in line          # current page X/N : name(owner)
    assert "THEME:GREEN" in line
    assert "LINK:up" in line


def test_render_oia_shows_new_page_indicator_when_unread_offscreen():
    st = TerminalState("me", "server")
    from glp_quick.terminal.pages import receive_page
    receive_page(st.pages, "bob", "NP", "plain", "x")  # unread, not current
    assert "●NEW:NP" in pres.render_oia(st, "GREEN")


# --- layout (FR-023) ---------------------------------------------------------------------
def test_layout_config_from_env_defaults_and_overrides():
    assert pres.LayoutConfig.from_env({}).mode == "lines"
    assert pres.LayoutConfig.from_env({}).n_command_lines == 3
    assert pres.LayoutConfig.from_env({"GLPQUICK_CMDLINES": "5"}).n_command_lines == 5
    assert pres.LayoutConfig.from_env({"GLPQUICK_LAYOUT": "two-strip"}).mode == "two-strip"
    assert pres.LayoutConfig.from_env({"GLPQUICK_CMDLINES": "bad"}).n_command_lines == 3


def test_layout_apply_command_switches_modes():
    cfg = pres.LayoutConfig()
    assert "two-strip" in cfg.apply_command("two-strip") and cfg.mode == "two-strip"
    assert cfg.apply_command("lines 6") and cfg.mode == "lines" and cfg.n_command_lines == 6
    assert cfg.apply_command("2") and cfg.n_command_lines == 2
    assert cfg.apply_command("garbage").startswith("??")


# --- PF-legend (FR-025) ------------------------------------------------------------------
def test_legend_blocks_each_carry_a_typed_equivalent():
    blocks = K.legend_blocks()
    assert len(blocks) == len(K.DEFAULT_BINDINGS)
    for b, binding in zip(blocks, K.DEFAULT_BINDINGS):
        assert binding.action.split()[0] in b or binding.legend_label in b
        assert binding.typed_equiv in b  # RDP-safe: the typed equivalent is always shown


def test_legend_line_joins_blocks_and_shows_transmit_equivalent():
    line = K.legend_line()
    assert "/help" in line and "//+Enter" in line and "/quit" in line
