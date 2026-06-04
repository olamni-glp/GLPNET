"""Corpus discovery — walk the vendored tree into an in-memory model.

PURE — filesystem reads only; no bridge, no DBOS, no LM, no REPL (research D1;
guarded by ``test_tutorials_no_bridge.py``). Read-only (FR-010): nothing is
executed or written. The model is a projection of the vendored snapshot at
``tutorials/olamni/`` (FR-007), re-walked each invocation (<3 s, SC-005).

Shape (data-model.md): ``Corpus → Tutorial(chapter chNN) → Exercise(exercise-MM)
→ Script(.glp)``. The ``.glp`` script is the listable/runnable unit; the
exercise is the description anchor (D4).
"""

from __future__ import annotations

import os
import re
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path

from . import describe

# Recognized layout (research ground truth): ``chNN`` chapters, ``exercise-MM``
# exercises (FR-011 — anything else under the corpus is non-standard).
_CHAPTER_DIR = re.compile(r"^ch(\d+)$", re.IGNORECASE)
_EXERCISE_DIR = re.compile(r"^exercise-(\d+)$", re.IGNORECASE)

_OVERVIEW_MD = "tutorial.md"


class CorpusUnreachableError(Exception):
    """The corpus root does not exist or cannot be read (FR-006, exit 5).

    Carries the path that was tried so the CLI can name it in the error.
    """

    def __init__(self, path: Path) -> None:
        self.path = Path(path)
        super().__init__(f"tutorial corpus not found or unreadable: {self.path}")


class DescriptionSource(str, Enum):
    """Where a script's one-line description came from (D7; for ``--json``/tests)."""

    EXERCISE_MD = "exercise_md"
    GLP_HEADER = "glp_header"
    NONE = "none"


@dataclass(frozen=True)
class Script:
    """A single ``.glp`` file — the listable/runnable unit (D4)."""

    name: str
    path: str  # repo-relative POSIX path under the corpus
    description: str  # always one trimmed line; ``(no description)`` if none
    description_source: DescriptionSource


@dataclass(frozen=True)
class Exercise:
    """An ``exercise-MM`` directory — the description anchor (D4)."""

    number: str  # ``MM`` from the dir name (zero-padded as found)
    dir: str  # repo-relative POSIX path of the exercise directory
    scripts: tuple[Script, ...]
    md_description: str | None  # shared one-line summary from ``ex-MM-tutorial.md``


@dataclass(frozen=True)
class Tutorial:
    """A chapter-level grouping ``chNN`` (spec "Tutorial")."""

    id: str  # ``chNN`` normalized lowercase, zero-padded
    title: str | None  # human title (D6); None → render id only
    exercises: tuple[Exercise, ...]
    is_empty: bool  # True when no recognizable ``exercise-MM`` with scripts (FR-008)


@dataclass
class Corpus:
    """The root of the vendored snapshot the lister reads (FR-007)."""

    root: Path
    root_rel: str  # repo-relative POSIX of ``root`` (for ``--json`` / display)
    chapters: list[Tutorial] = field(default_factory=list)
    overview_titles: dict[str, str] = field(default_factory=dict)
    warnings: list[str] = field(default_factory=list)


# --------------------------------------------------------------------------- #
# Path helpers                                                                 #
# --------------------------------------------------------------------------- #
def _rel_posix(target: Path, base: Path | None) -> str:
    """``target`` as a POSIX path relative to ``base`` (repo root) when possible.

    Falls back to the absolute POSIX path when ``base`` is None or on a
    different drive (Windows ``relpath`` raises ``ValueError`` across drives).
    """
    target = Path(target)
    if base is not None:
        try:
            return Path(os.path.relpath(target, base)).as_posix()
        except ValueError:
            pass
    return target.as_posix()


def _chapter_id(dir_name: str) -> str:
    """``ch3`` / ``CH03`` → ``ch03`` (normalized lowercase, zero-padded)."""
    m = _CHAPTER_DIR.match(dir_name)
    assert m is not None  # caller guards
    return f"ch{int(m.group(1)):02d}"


def _read_text(path: Path) -> str | None:
    """Read a UTF-8 text file, tolerating unreadable/binary content."""
    try:
        return path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None


# --------------------------------------------------------------------------- #
# Root resolution + readability guard (T005, FR-006)                           #
# --------------------------------------------------------------------------- #
def default_corpus_root(repo_root: Path) -> Path:
    """The default vendored corpus location: ``<repo-root>/tutorials/olamni`` (D2)."""
    return Path(repo_root) / "tutorials" / "olamni"


def resolve_corpus_root(repo_root: Path, override: Path | None) -> Path:
    """Resolve and validate the corpus root, else raise ``CorpusUnreachableError``.

    ``override`` is the ``--corpus`` flag (tests / non-default trees); otherwise
    the repo-relative default (D2). The root must exist and be a readable
    directory — no partial/misleading listing on failure (FR-006).
    """
    root = Path(override).resolve() if override is not None else default_corpus_root(repo_root)
    if not root.is_dir():
        raise CorpusUnreachableError(root)
    try:
        os.scandir(root).close()
    except OSError as exc:  # pragma: no cover - permission edge
        raise CorpusUnreachableError(root) from exc
    return root


# --------------------------------------------------------------------------- #
# The walk (T006/T012/T013/T026)                                               #
# --------------------------------------------------------------------------- #
def load_corpus(root: Path, repo_root: Path | None = None) -> Corpus:
    """Walk the corpus ``root`` into a :class:`Corpus` (read-only).

    ``repo_root`` makes emitted paths repo-relative POSIX (data-model). The walk
    recognizes ``chNN`` chapters and ``exercise-MM`` exercises, collects ``.glp``
    scripts, derives descriptions (D7), and records non-standard directories as
    warnings (FR-011) without ever aborting the listing.
    """
    root = Path(root)
    corpus = Corpus(root=root, root_rel=_rel_posix(root, repo_root))

    overview = root / _OVERVIEW_MD
    text = _read_text(overview)
    if text is not None:
        corpus.overview_titles = describe.parse_overview_titles(text)

    chapter_dirs: list[tuple[str, Path]] = []
    for entry in sorted(os.scandir(root), key=lambda e: e.name.lower()):
        if not entry.is_dir():
            continue  # top-level files (tutorial.md, charter.md, …) are not chapters
        if _CHAPTER_DIR.match(entry.name):
            chapter_dirs.append((_chapter_id(entry.name), Path(entry.path)))
        else:
            corpus.warnings.append(
                f"skipped non-standard dir: {_rel_posix(Path(entry.path), root)}"
            )

    for chapter_id, chapter_path in sorted(chapter_dirs, key=lambda t: t[0]):
        corpus.chapters.append(
            _build_chapter(chapter_id, chapter_path, root, repo_root, corpus.overview_titles, corpus.warnings)
        )

    return corpus


def _build_chapter(
    chapter_id: str,
    chapter_path: Path,
    root: Path,
    repo_root: Path | None,
    overview_titles: dict[str, str],
    warnings: list[str],
) -> Tutorial:
    exercise_dirs: list[tuple[str, Path]] = []
    for entry in sorted(os.scandir(chapter_path), key=lambda e: e.name.lower()):
        if not entry.is_dir():
            continue  # chapter-level .md files are signposts, not exercises
        if _EXERCISE_DIR.match(entry.name):
            number = _EXERCISE_DIR.match(entry.name).group(1)  # type: ignore[union-attr]
            exercise_dirs.append((number, Path(entry.path)))
        else:
            warnings.append(
                f"skipped non-standard dir: {_rel_posix(Path(entry.path), root)}"
            )

    exercises: list[Exercise] = []
    for number, ex_path in sorted(exercise_dirs, key=lambda t: t[0]):
        exercise = _build_exercise(number, ex_path, root, repo_root, warnings)
        if exercise is not None:
            exercises.append(exercise)

    return Tutorial(
        id=chapter_id,
        title=_resolve_title(chapter_id, chapter_path, overview_titles),
        exercises=tuple(exercises),
        is_empty=not exercises,
    )


def _build_exercise(
    number: str,
    ex_path: Path,
    root: Path,
    repo_root: Path | None,
    warnings: list[str],
) -> Exercise | None:
    glp_files = sorted(
        (p for p in ex_path.iterdir() if p.is_file() and p.suffix == ".glp"),
        key=lambda p: p.name.lower(),
    )
    if not glp_files:
        # An exercise-MM dir with no scripts is a non-standard shape — warn and
        # do NOT render it as an empty exercise (data-model; FR-011).
        warnings.append(
            f"skipped exercise with no scripts: {_rel_posix(ex_path, root)}"
        )
        return None

    md_description = _exercise_md_description(ex_path, number)
    multi = len(glp_files) > 1

    scripts: list[Script] = []
    for glp in glp_files:
        glp_text = _read_text(glp) or ""
        glp_header = describe.extract_glp_header(glp_text)
        text, source = describe.resolve_script_description(
            md_description, glp_header, multi_script=multi
        )
        scripts.append(
            Script(
                name=glp.name,
                path=_rel_posix(glp, repo_root),
                description=text,
                description_source=DescriptionSource(source),
            )
        )

    return Exercise(
        number=number,
        dir=_rel_posix(ex_path, repo_root),
        scripts=tuple(scripts),
        md_description=md_description,
    )


def _exercise_md_description(ex_path: Path, number: str) -> str | None:
    """The shared one-line description from this exercise's ``ex-MM-tutorial.md``.

    Prefer the conventional ``ex-MM-tutorial.md`` name; tolerate any single
    ``*-tutorial.md`` in the exercise dir as a fallback (robust to slight naming
    drift), but never read a sibling exercise's guide.
    """
    candidate = ex_path / f"ex-{number}-tutorial.md"
    if not candidate.is_file():
        guides = sorted(p for p in ex_path.glob("*-tutorial.md") if p.is_file())
        candidate = guides[0] if guides else candidate
    text = _read_text(candidate) if candidate.is_file() else None
    if text is None:
        return None
    return describe.extract_exercise_md_description(text)


def _resolve_title(
    chapter_id: str, chapter_path: Path, overview_titles: dict[str, str]
) -> str | None:
    """Chapter human title by D6 precedence: overview table → guide H1 → None."""
    if chapter_id in overview_titles:
        return overview_titles[chapter_id]
    guide = chapter_path / f"{chapter_id}_tutorial.md"
    if guide.is_file():
        text = _read_text(guide)
        if text is not None:
            tail = describe.chapter_title_from_guide(text)
            if tail:
                return tail
    return None


__all__ = [
    "CorpusUnreachableError",
    "DescriptionSource",
    "Script",
    "Exercise",
    "Tutorial",
    "Corpus",
    "default_corpus_root",
    "resolve_corpus_root",
    "load_corpus",
]
