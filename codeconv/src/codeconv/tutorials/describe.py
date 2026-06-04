"""Description + title text extraction (D6, D7, FR-003/FR-004, US3).

PURE — string processing only; no filesystem, no bridge, no LM (mechanical
extraction, D7). Two text sources feed the listing:

  * **Chapter titles** (D6): the top-level ``tutorial.md`` "Chapter status"
    table (authoritative ``chNN → name`` map), then a chapter guide's H1 tail.
  * **Per-script descriptions** (D7): the exercise ``ex-MM-tutorial.md`` H1
    descriptive tail (text after the ``—``), else the ``.glp`` leading comment,
    else the ``(no description)`` sentinel.

All extracted text is normalized to a single trimmed line (collapse
whitespace, cap length for terminal scannability).
"""

from __future__ import annotations

import re

# Sentinel shown when no description can be derived from any source (US3 #2).
NO_DESCRIPTION = "(no description)"

# Cap one-line descriptions so the terminal listing stays scannable (D7).
_MAX_LEN = 100

# Dash separators between an H1 title's "what" and its "descriptive tail",
# tried in order. Em dash is what the real corpus uses (``# Exercise 1 — …``).
_DASHES = ("—", "–", " - ")

# A ``.glp`` leading comment that is only the file's own name (a banner) carries
# no description — e.g. ``% ch-03-ex-01-glp-fair-stream-merger.glp``.
_FILENAME_BANNER = re.compile(r"^[\w.\-]+\.glp$")

# A markdown table data row's first cell that is a bare chapter number.
_NUM_CELL = re.compile(r"^\d+$")


def normalize_one_line(text: str) -> str:
    """Collapse all whitespace to single spaces, trim, and cap length."""
    one = re.sub(r"\s+", " ", text).strip()
    if len(one) > _MAX_LEN:
        one = one[: _MAX_LEN - 1].rstrip() + "…"
    return one


def _first_h1(md_text: str) -> str | None:
    """Return the content of the first level-1 ATX heading (``# …``), or None."""
    for line in md_text.splitlines():
        stripped = line.strip()
        if stripped.startswith("# ") and not stripped.startswith("## "):
            return stripped[2:].strip()
    return None


def _dash_tail(heading: str) -> str | None:
    """The text after the first dash separator in a heading, or None."""
    for dash in _DASHES:
        idx = heading.find(dash)
        if idx != -1:
            tail = heading[idx + len(dash):].strip()
            if tail:
                return tail
    return None


# --------------------------------------------------------------------------- #
# D6 — chapter titles                                                          #
# --------------------------------------------------------------------------- #
def parse_overview_titles(tutorial_md_text: str) -> dict[str, str]:
    """Parse the top-level ``tutorial.md`` "Chapter status" table → ``{chNN: title}``.

    The table shape is ``| # | Chapter | Tutorial entry | Status |`` with data
    rows whose first cell is the bare chapter number and second cell the human
    title. Header/separator/non-numeric rows are ignored. The numeric id is
    zero-padded to the ``chNN`` form (D6 precedence source #1).
    """
    titles: dict[str, str] = {}
    for line in tutorial_md_text.splitlines():
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if len(cells) < 2:
            continue
        num, title = cells[0], cells[1]
        if not _NUM_CELL.match(num) or not title:
            continue
        titles[f"ch{int(num):02d}"] = normalize_one_line(title)
    return titles


def chapter_title_from_guide(guide_md_text: str) -> str | None:
    """D6 precedence #2: the descriptive tail of a ``chNN_tutorial.md`` H1.

    ``# Chapter 3 — GLP Core`` → ``GLP Core``. Falls back to the whole H1 text
    when there is no dash tail; returns None when there is no H1 at all.
    """
    h1 = _first_h1(guide_md_text)
    if h1 is None:
        return None
    tail = _dash_tail(h1)
    return normalize_one_line(tail if tail else h1)


# --------------------------------------------------------------------------- #
# D7 — per-script descriptions                                                 #
# --------------------------------------------------------------------------- #
def extract_exercise_md_description(md_text: str) -> str | None:
    """D7 step 1: a one-line description from an ``ex-MM-tutorial.md``.

    Prefer the H1 descriptive tail (text after ``—``); else the first
    non-empty, non-heading, non-fenced paragraph line. Returns None when the
    document yields nothing usable.
    """
    h1 = _first_h1(md_text)
    if h1 is not None:
        tail = _dash_tail(h1)
        if tail:
            return normalize_one_line(tail)

    in_fence = False
    for line in md_text.splitlines():
        s = line.strip()
        if s.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence or not s or s.startswith("#") or s.startswith(">"):
            continue
        return normalize_one_line(s)
    return None


def extract_glp_header(glp_text: str) -> str | None:
    """D7 step 2: the first informative leading ``%``/``%%`` comment of a ``.glp``.

    Skips a pure filename banner (``% <name>.glp``) and blank comment lines;
    stops at the first non-comment, non-blank line. Returns None when no
    informative leading comment exists.
    """
    for raw in glp_text.splitlines():
        line = raw.strip()
        if not line:
            continue
        if not line.startswith("%"):
            break  # reached code — header region is over
        content = line.lstrip("%").strip()
        if not content:
            continue  # blank comment line (``%`` / ``%%``)
        if _FILENAME_BANNER.match(content):
            continue  # filename banner — not a description
        return normalize_one_line(content)
    return None


def resolve_script_description(
    exercise_md: str | None,
    glp_header: str | None,
    *,
    multi_script: bool,
) -> tuple[str, str]:
    """Resolve one script's ``(description, source)`` per D7 / FR-004.

    Precedence is the exercise ``.md`` first (FR-004) **except** for multi-script
    exercises, where the shared ``.md`` line cannot describe an individual script
    so its own ``.glp`` header is the disambiguator (data-model: "per-script
    ``glp_header`` disambiguates individual scripts"). Returns the
    ``(no description)`` sentinel + ``"none"`` when no source yields text.
    """
    if multi_script:
        ordered = ((glp_header, "glp_header"), (exercise_md, "exercise_md"))
    else:
        ordered = ((exercise_md, "exercise_md"), (glp_header, "glp_header"))
    for text, source in ordered:
        if text:
            return text, source
    return NO_DESCRIPTION, "none"


__all__ = [
    "NO_DESCRIPTION",
    "normalize_one_line",
    "parse_overview_titles",
    "chapter_title_from_guide",
    "extract_exercise_md_description",
    "extract_glp_header",
    "resolve_script_description",
]
