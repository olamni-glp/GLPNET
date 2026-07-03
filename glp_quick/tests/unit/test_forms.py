"""T029 [US4] — mask / form pages (forms.py; FR-015)."""

from __future__ import annotations

from glp_quick.terminal import forms
from glp_quick.terminal import protocol
from glp_quick.terminal.forms import Field, Label, Mask


def _mask():
    return Mask(
        labels=[Label(0, 0, "Name:"), Label(1, 0, "Age:")],
        fields=[Field(0, 6, 12), Field(1, 6, 3)],
    )


def test_render_shows_labels_and_empty_field_placeholders():
    txt = forms.render(_mask())
    assert "Name:" in txt and "Age:" in txt
    assert "_" * 12 in txt and "_" * 3 in txt   # empty fillable regions


def test_fill_returns_values_with_labels_intact():
    m = _mask()
    filled = forms.fill(m, [(0, "Alice"), (1, "30")])
    txt = forms.render(filled)
    assert "Name:" in txt and "Age:" in txt      # fixed labels intact (FR-015)
    assert "Alice" in txt and "30" in txt         # entered values present
    assert forms.labels_intact(m, filled)         # labels unchanged by fill
    # only fillable regions carry values — the original mask is unmodified
    assert m.values == []


def test_fill_ignores_out_of_range_field_index():
    m = _mask()
    filled = forms.fill(m, [(9, "ignored")])
    assert "ignored" not in forms.render(filled)


def test_wire_round_trip_form_def_and_fill():
    m = _mask()
    labels, fields = forms.to_wire(m)
    # encode/decode form_def through the codec
    wire = protocol.form_def("FORM", labels, fields)
    tm = protocol.decode(wire)
    assert tm.kind == "form_def"
    rebuilt = forms.from_wire(tm.fields[1], tm.fields[2])
    assert forms.labels_intact(m, rebuilt) and rebuilt.fields == m.fields

    # fill on the other side, return via form_fill
    fills = [(0, "Bob"), (1, "42")]
    fill_wire = protocol.form_fill("FORM", fills)
    ftm = protocol.decode(fill_wire)
    assert ftm.kind == "form_fill"
    got = forms.fills_from_wire(ftm.fields[1])
    assert got == fills
    completed = forms.fill(rebuilt, got)
    txt = forms.render(completed)
    assert "Name:" in txt and "Bob" in txt and "42" in txt
