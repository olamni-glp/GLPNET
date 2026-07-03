"""Mask / form pages (feature 040, US4; FR-015; data-model §Mask).

A mask is a page with **fixed labels** at fixed positions plus **fillable regions** (fields). One side
defines it; the other fills the fields and returns the completed form with the fixed labels intact. The
model is host-free (unit-tested); it bridges to the wire via ``label(R,C,"L")`` / ``field(R,C,W)`` /
``fill(Idx,"V")`` terms (see ``contracts/terminal-protocol.md`` and :mod:`glp_quick.terminal.protocol`).
"""

from __future__ import annotations

from dataclasses import dataclass, field as dc_field
from typing import List, Tuple


@dataclass(frozen=True)
class Label:
    row: int
    col: int
    text: str


@dataclass(frozen=True)
class Field:
    row: int
    col: int
    width: int


@dataclass
class Mask:
    labels: List[Label]
    fields: List[Field]
    values: List[str] = dc_field(default_factory=list)


def _dims(mask: Mask) -> Tuple[int, int]:
    rows = cols = 0
    for l in mask.labels:
        rows = max(rows, l.row + 1)
        cols = max(cols, l.col + len(l.text))
    for f in mask.fields:
        rows = max(rows, f.row + 1)
        cols = max(cols, f.col + f.width)
    return rows, cols


def render(mask: Mask) -> str:
    """Render the mask to page text: labels at their fixed positions; each field shows its value
    (or ``_`` placeholders when empty). Labels are always emitted verbatim, so they stay intact (FR-015).
    """
    rows, cols = _dims(mask)
    grid = [[" "] * cols for _ in range(rows)]

    def place(row: int, col: int, s: str) -> None:
        for j, ch in enumerate(s):
            if 0 <= row < rows and 0 <= col + j < cols:
                grid[row][col + j] = ch

    for l in mask.labels:
        place(l.row, l.col, l.text)
    vals = list(mask.values) + [""] * (len(mask.fields) - len(mask.values))
    for i, f in enumerate(mask.fields):
        cell = (vals[i].ljust(f.width) if vals[i] else "_" * f.width)[:f.width]
        place(f.row, f.col, cell)
    return "\n".join("".join(r).rstrip() for r in grid)


def fill(mask: Mask, fills: List[Tuple[int, str]]) -> Mask:
    """Return a copy of ``mask`` with the given ``(field_index, value)`` fills applied (labels untouched)."""
    vals = list(mask.values) + [""] * (len(mask.fields) - len(mask.values))
    for idx, v in fills:
        if 0 <= idx < len(mask.fields):
            vals[idx] = v
    return Mask(labels=list(mask.labels), fields=list(mask.fields), values=vals)


def labels_intact(original: Mask, other: Mask) -> bool:
    """Whether ``other`` keeps ``original``'s fixed labels unchanged (position + text)."""
    return list(original.labels) == list(other.labels)


# --- wire bridge (protocol form_def / form_fill term shapes) -----------------------------
def to_wire(mask: Mask) -> "Tuple[List[Tuple[int, int, str]], List[Tuple[int, int, int]]]":
    """``(label_tuples, field_tuples)`` for :func:`glp_quick.terminal.protocol.form_def`."""
    labels = [(l.row, l.col, l.text) for l in mask.labels]
    fields = [(f.row, f.col, f.width) for f in mask.fields]
    return labels, fields


def from_wire(label_terms, field_terms) -> Mask:
    """Build a :class:`Mask` from decoded ``label(R,C,"L")`` / ``field(R,C,W)`` terms."""
    labels = [Label(t.args[0], t.args[1], t.args[2]) for t in label_terms]
    fields = [Field(t.args[0], t.args[1], t.args[2]) for t in field_terms]
    return Mask(labels, fields)


def fills_from_wire(fill_terms) -> List[Tuple[int, str]]:
    """Build ``(index, value)`` fills from decoded ``fill(Idx,"V")`` terms."""
    return [(t.args[0], t.args[1]) for t in fill_terms]
