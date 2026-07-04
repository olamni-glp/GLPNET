"""Joint live-edit pinpoint changes (feature 040, US4; FR-012/013/014; data-model §PinpointChange).

A pinpoint overwrites a rectangular region of a joint page with a block of characters while the
**original** content of that region is saved, so the change is either *transient* (a framed/highlight
comment dismissible back to the saved original) or *permanent* (the overwrite persists). Overlapping
writes are last-writer-wins per region; the saved original of each overwritten region stays recoverable
(FR-012). Host-free: operates on a :class:`glp_quick.terminal.pages.Page` value object.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import List, Optional


@dataclass(frozen=True)
class Region:
    row: int
    col: int
    height: int
    width: int


@dataclass(frozen=True)
class PinResult:
    """Outcome of a pinpoint op. ``reason`` ∈ ``"" | joint_off | out_of_bounds | closed | no_transient``."""

    ok: bool
    reason: str = ""
    region: Optional[Region] = None


def _lines(page) -> List[str]:
    return page.text.split("\n")


def _page_width(lines: List[str]) -> int:
    return max((len(l) for l in lines), default=0)


def _read_region(lines: List[str], row: int, col: int, height: int, width: int) -> str:
    out = []
    for i in range(height):
        padded = lines[row + i].ljust(col + width)
        out.append(padded[col:col + width])
    return "\n".join(out)


def _write_region(page, row: int, col: int, height: int, width: int, block: str) -> None:
    lines = _lines(page)
    block_rows = block.split("\n")
    for i in range(height):
        r = row + i
        line = lines[r].ljust(col + width)
        seg = (block_rows[i] if i < len(block_rows) else "").ljust(width)[:width]
        lines[r] = line[:col] + seg + line[col + width:]
    page.text = "\n".join(lines)


def apply_pinpoint(page, row: int, col: int, height: int, width: int, block: str,
                   classification: str) -> PinResult:
    """Apply a pinpoint overwrite to ``page`` (FR-012/013/014).

    Rejected (and reported) if ``page`` is closed (``None``), joint mode is off, or the region exceeds
    the page bounds. On success the original region is saved (first write per region is the recoverable
    original; last write wins the content); ``classification`` ∈ ``{transient, permanent}``.
    """
    if page is None:
        return PinResult(False, "closed")
    if not page.joint:
        return PinResult(False, "joint_off")
    if row < 0 or col < 0 or height < 1 or width < 1:
        return PinResult(False, "out_of_bounds")
    lines = _lines(page)
    if row + height > len(lines) or col + width > _page_width(lines):
        return PinResult(False, "out_of_bounds")

    region = (row, col, height, width)
    entry = page.saved_regions.get(region)
    if entry is None:
        page.saved_regions[region] = {"original": _read_region(lines, row, col, height, width),
                                      "class": classification}
    else:
        entry["class"] = classification  # last-writer-wins classification; keep the first original
    _write_region(page, row, col, height, width, block)
    return PinResult(True, "", Region(row, col, height, width))


def undo_pin(page) -> PinResult:
    """Dismiss the most recent **transient** pinpoint, restoring its saved original (FR-014).

    Permanent pinpoints are left in place (their overwrite persists) though their original stays
    recoverable in ``saved_regions``. Returns ``no_transient`` if there is nothing to dismiss.
    """
    if page is None:
        return PinResult(False, "closed")
    for region in reversed(list(page.saved_regions.keys())):
        entry = page.saved_regions[region]
        if entry["class"] == "transient":
            row, col, height, width = region
            _write_region(page, row, col, height, width, entry["original"])
            del page.saved_regions[region]
            return PinResult(True, "", Region(row, col, height, width))
    return PinResult(False, "no_transient")


def block_dims(block: str) -> "tuple[int, int]":
    """Height (line count) and width (longest line) of a pinpoint block."""
    rows = block.split("\n")
    return len(rows), max((len(r) for r in rows), default=0)
