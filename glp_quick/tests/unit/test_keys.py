"""T054 [US7] — user-bindable PF keys + live legend (keys.BindingRegistry; FR-019/FR-020)."""

from __future__ import annotations

from glp_quick.terminal import keys as K
from glp_quick.terminal.keys import BindingRegistry


def test_free_key_detection():
    r = BindingRegistry()
    assert r.is_free("F4") and r.is_free("F5") and r.is_free("PF13")
    assert not r.is_free("F1")   # a default-assigned key
    assert not r.is_free("Z")    # not a PF key at all


def test_bind_free_key_reflects_in_legend_with_typed_equivalent():
    r = BindingRegistry()
    ok, _ = r.bind("F4", "/pages")
    assert ok
    leg = r.legend()
    assert "F4" in leg and "/pages" in leg      # legend shows the binding + its typed equivalent (FR-019)
    assert not r.is_free("F4")                  # now taken


def test_cannot_bind_an_already_assigned_key():
    r = BindingRegistry()
    ok, msg = r.bind("F1", "/whatever")
    assert not ok and "already assigned" in msg


def test_every_binding_carries_a_typed_equivalent():
    r = BindingRegistry()
    r.bind("F4", "/pages")
    r.bind("PF13", "/theme")
    for b in r.all_bindings():
        assert b.typed_equiv   # RDP-safe invariant (FR-002/FR-019)


def test_reserved_key_exposes_a_ctrl_alternate():
    r = BindingRegistry()
    ok, msg = r.bind("F11", "/quit")
    assert ok
    b = r.user_binding("F11")
    assert b.ctrl_alt == "C-e" and "C-e" in msg   # FR-020 Ctrl fallback


def test_shift_maps_to_pf13_through_pf24():
    assert K.pf_for_shift(1) == "PF13"
    assert K.pf_for_shift(12) == "PF24"


def test_bind_requires_a_command():
    r = BindingRegistry()
    ok, _ = r.bind("F4", "   ")
    assert not ok


def test_unbind_frees_the_key():
    r = BindingRegistry()
    r.bind("F5", "/pages")
    assert not r.is_free("F5")
    assert r.unbind("F5") and r.is_free("F5")
