"""Rendering — human-readable listing + ``--json`` model (D8, FR-005/FR-009).

PURE — turns the in-memory model into text; no I/O of its own. The human form
is an indented ``chapter → exercise → script — description`` listing for
scannability (FR-005); the JSON form is the structured model that guarantees
skill↔CLI equivalence (FR-009) and backs the tests. Both render the same
content from the same model, so the two entry points cannot diverge.
"""

from __future__ import annotations

import json as _json
from typing import Iterable

from .corpus import Corpus, Tutorial

_EMPTY_INDICATOR = "(no scripts)"


def render_human(chapters: Iterable[Tutorial]) -> str:
    """Indented ``chNN — title`` / ``exercise-MM`` / ``name — description`` listing.

    Empty chapters show ``(no scripts)`` (FR-008). Script names within an
    exercise are column-aligned so descriptions line up (FR-005).
    """
    lines: list[str] = []
    for chapter in chapters:
        header = f"{chapter.id} — {chapter.title}" if chapter.title else chapter.id
        lines.append(header)
        if chapter.is_empty or not chapter.exercises:
            lines.append(f"  {_EMPTY_INDICATOR}")
            continue
        for exercise in chapter.exercises:
            lines.append(f"  exercise-{exercise.number}")
            width = max((len(s.name) for s in exercise.scripts), default=0)
            for script in exercise.scripts:
                lines.append(f"    {script.name.ljust(width)}  — {script.description}")
    return "\n".join(lines)


def build_payload(corpus: Corpus, chapters: Iterable[Tutorial]) -> dict:
    """The JSON-serializable model (D8). ``chapters`` may be a filtered subset."""
    return {
        "corpus_root": corpus.root_rel,
        "chapters": [
            {
                "id": chapter.id,
                "title": chapter.title,
                "is_empty": chapter.is_empty,
                "exercises": [
                    {
                        "number": exercise.number,
                        "md_description": exercise.md_description,
                        "scripts": [
                            {
                                "name": script.name,
                                "path": script.path,
                                "description": script.description,
                                "description_source": script.description_source.value,
                            }
                            for script in exercise.scripts
                        ],
                    }
                    for exercise in chapter.exercises
                ],
            }
            for chapter in chapters
        ],
        "warnings": list(corpus.warnings),
    }


def render_json(corpus: Corpus, chapters: Iterable[Tutorial]) -> str:
    """Serialize :func:`build_payload` with stable, indented formatting."""
    return _json.dumps(build_payload(corpus, list(chapters)), indent=2)


__all__ = ["render_human", "build_payload", "render_json"]
