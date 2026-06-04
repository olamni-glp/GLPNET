"""Resolve a selected 022 ``Exercise`` into a runnable example — the unified
run-model + shape-classification *conformity mechanism* (research D2–D5; the
3-Hybrid scope approved 2026-06-04).

PURE / BRIDGE-FREE (research D1). Filesystem reads + the 022 corpus model only;
no bridge, no DBOS, no SQLAlchemy/psycopg, no LM, no REPL. Guarded by
``test_tutorials_no_bridge.py``.

The conformity mechanism: every example is classified into a precise
:class:`Shape`, regardless of how differently chapters are written. The common
shapes are *handled* (section single/multi-file-compose, use-case project, with
compound conjunctive goals, multi-goal sequences, variable ``:limit``, and
load-failure-as-golden); the genuinely hard shapes are *deferred with an
explicit reason* (two-session, bytecode-dump golden, Flutter-only golden) so a
differently-written example is never silently mis-run.

Selection reads the **vendored** snapshot (``tutorials/olamni/``); execution
resolves against the **sibling** repo in place via two roots (D4):
``sibling_corpus`` for section ``.glp`` files, ``sibling_glp_root`` for the
ch07 use-case project ``programs/cssg_modules`` (D5).
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import Enum
from pathlib import Path, PurePosixPath

from . import outcome as _oc
from .corpus import Exercise, Tutorial

# Default sibling execution roots (research D4; overridable via CLI).
DEFAULT_SIBLING_CORPUS = "D:/bstdev/research/glp/GLP/olamni/tutorial"
DEFAULT_SIBLING_GLP_ROOT = "D:/bstdev/research/glp/GLP"
# The vendored snapshot root, relative to repo root (022 default).
_VENDOR_RELROOT = PurePosixPath("tutorials/olamni")
# ch07 use-case canonical substrate (D5): sibling programs/cssg_modules.
_CSSG_RELPATH = "programs/cssg_modules"


class Shape(str, Enum):
    """How an example is run (the conformity taxonomy)."""

    SECTION_SINGLE = "section_single"  # one .glp, inline goals
    SECTION_MULTI_COMPOSE = "section_multi_compose"  # 2+ .glp loaded in ONE session
    SECTION_TWO_SESSION = "section_two_session"  # 2 mutually-exclusive files (DEFERRED)
    USE_CASE_PROJECT = "use_case_project"  # project dir + play goal
    NOT_IMPLEMENTED = "not_implemented"  # stub / superseded


class LoadKind(str, Enum):
    SINGLE_FILE = "single_file"
    PROJECT_DIR = "project_dir"


class GoalSource(str, Enum):
    GUIDE = "guide"  # parsed from the tutorial documentation (trace)
    USER_SUPPLIED = "user_supplied"  # --goal "<text>"


@dataclass(frozen=True)
class Goal:
    """A REPL goal to run (FR-004)."""

    text: str  # source ending in '.' (e.g. ``merge([1,2,3],[a,b],Xs).``)
    is_primary: bool = False
    needs_limit: int | None = None
    source: GoalSource = GoalSource.GUIDE


@dataclass(frozen=True)
class LoadTarget:
    """What the backend loads (FR-003)."""

    kind: LoadKind
    select_path: str  # repo-rel path in the vendored snapshot (provenance)
    exec_path: str  # absolute path under the sibling execution root (D4)


@dataclass(frozen=True)
class RunnableExample:
    """The resolved unit a run/preview/explain operates on (data-model §1)."""

    chapter_id: str
    exercise_number: str
    shape: Shape
    load_targets: tuple[LoadTarget, ...]
    goals: tuple[Goal, ...]
    golden: tuple  # outcome.Outcome | None, positionally aligned to ``goals``
    guide_path: str | None
    guide_text: str
    supported: bool = True
    unsupported_reason: str | None = None
    warnings: tuple[str, ...] = ()

    @property
    def primary_goal(self) -> Goal | None:
        for g in self.goals:
            if g.is_primary:
                return g
        return self.goals[0] if self.goals else None


# --------------------------------------------------------------------------- #
# Path helpers                                                                 #
# --------------------------------------------------------------------------- #
def _exec_path_for_script(script_path: str, sibling_corpus: Path) -> str:
    """Map a vendored script path to its sibling-corpus execution path (D4).

    ``tutorials/olamni/ch01/exercise-01/x.glp`` →
    ``<sibling_corpus>/ch01/exercise-01/x.glp`` (same relpath below the corpus).
    """
    p = PurePosixPath(script_path)
    try:
        rel = p.relative_to(_VENDOR_RELROOT)
    except ValueError:
        rel = PurePosixPath(p.name)
    return str((sibling_corpus / Path(*rel.parts)).resolve())


def _read(path: Path) -> str | None:
    try:
        return path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return None


def _exercise_files(repo_root: Path, exercise: Exercise) -> tuple[Path | None, Path | None, bool]:
    """Locate (guide, trace, has_flutter_only) under the vendored exercise dir."""
    ex_dir = repo_root / exercise.dir
    num = exercise.number
    guide = ex_dir / f"ex-{num}-tutorial.md"
    trace = ex_dir / f"ex-{num}-repl-trace.md"
    if not guide.is_file():
        cand = sorted(ex_dir.glob("*-tutorial.md")) if ex_dir.is_dir() else []
        guide = cand[0] if cand else guide
    if not trace.is_file():
        cand = sorted(ex_dir.glob("*-repl-trace.md")) if ex_dir.is_dir() else []
        trace = cand[0] if cand else trace
    has_flutter = ex_dir.is_dir() and any(ex_dir.glob("*-flutter-trace.md")) and not trace.is_file()
    return (guide if guide.is_file() else None, trace if trace.is_file() else None, has_flutter)


# --------------------------------------------------------------------------- #
# Goal extraction (D3)                                                         #
# --------------------------------------------------------------------------- #
def _extract_goals(guide_text: str) -> list[Goal]:
    """Goals from the guide's fenced blocks (D3); the play goal ``fplayMM.`` is
    primary (use-case), else the first goal. The guide is the reliable goal
    source across all chapter shapes (some traces don't echo the goal)."""
    goals: list[Goal] = []
    for text, limit in _oc.parse_guide_goals(guide_text):
        is_play = text.lower().startswith("fplay")
        goals.append(Goal(text=text, is_primary=is_play, needs_limit=limit, source=GoalSource.GUIDE))
    if goals and not any(x.is_primary for x in goals):
        goals[0] = Goal(goals[0].text, True, goals[0].needs_limit, goals[0].source)
    return goals


# --------------------------------------------------------------------------- #
# The resolver — shape classification (D2–D5)                                  #
# --------------------------------------------------------------------------- #
def resolve_example(
    tutorial: Tutorial,
    exercise: Exercise,
    *,
    repo_root: Path,
    sibling_corpus: Path,
    sibling_glp_root: Path,
) -> RunnableExample:
    """Classify ``(tutorial, exercise)`` into a :class:`RunnableExample` (D2–D5).

    Read-only. Detects shape from the 022 model (``Exercise.scripts`` empty vs
    not, D2), resolves load target(s) against the sibling roots (D4/D5), parses
    goals + golden from the vendored trace (D3/D7), and classifies the example
    as supported (handled) or deferred-with-reason (3-Hybrid).
    """
    guide, trace, flutter_only = _exercise_files(repo_root, exercise)
    trace_text = _read(trace) if trace else None
    guide_text = (_read(guide) or "") if guide else ""
    guide_path = (str(PurePosixPath(exercise.dir) / guide.name)) if guide else None
    goals = tuple(_extract_goals(guide_text)) if guide_text else ()
    # Golden outcomes are positionally aligned to the goals (D7) — robust to
    # traces that don't echo the goal text (ch03/ch04 glue the first output to
    # the GLP> prompt). Pad with None when the trace has fewer blocks.
    golden_list = _oc.parse_outcome_segments(trace_text) if trace_text else []
    golden = tuple(golden_list[i] if i < len(golden_list) else None for i in range(len(goals)))
    warnings: list[str] = []

    # --- Shape detection (D2) ---
    if exercise.scripts:
        return _resolve_section(
            tutorial, exercise, goals, golden, guide_path, guide_text,
            sibling_corpus=sibling_corpus, warnings=warnings,
        )

    # Empty scripts: use-case project (ch07) OR stub/superseded.
    if flutter_only:
        return _not_runnable(
            tutorial, exercise, Shape.NOT_IMPLEMENTED, guide_path, guide_text,
            "example has only a Flutter trace (no REPL golden) — not runnable via the REPL backend",
        )
    if guide is not None and _is_use_case(tutorial, exercise):
        exec_dir = (sibling_glp_root / _CSSG_RELPATH).resolve()
        target = LoadTarget(
            kind=LoadKind.PROJECT_DIR,
            select_path=exercise.dir,
            exec_path=str(exec_dir),
        )
        if not goals:
            warnings.append("no goal resolvable from the guide; supply one with --goal")
        return RunnableExample(
            chapter_id=tutorial.id, exercise_number=exercise.number,
            shape=Shape.USE_CASE_PROJECT, load_targets=(target,), goals=goals,
            golden=golden, guide_path=guide_path, guide_text=guide_text,
            supported=True, unsupported_reason=None, warnings=tuple(warnings),
        )
    return _not_runnable(
        tutorial, exercise, Shape.NOT_IMPLEMENTED, guide_path, guide_text,
        "chapter/example not yet implemented (no runnable scripts or use-case project)",
    )


def _resolve_section(
    tutorial, exercise, goals, golden, guide_path, guide_text, *, sibling_corpus, warnings,
) -> RunnableExample:
    targets = tuple(
        LoadTarget(
            kind=LoadKind.SINGLE_FILE,
            select_path=s.path,
            exec_path=_exec_path_for_script(s.path, sibling_corpus),
        )
        for s in exercise.scripts
    )
    if len(targets) == 1:
        shape = Shape.SECTION_SINGLE
        supported, reason = True, None
    else:
        # 2+ scripts. Default: load all in ONE session (multi-compose, handled).
        # A genuine two-session exercise (mutually-exclusive files defining the
        # same predicate, e.g. ch04/03) cannot co-load — that surfaces as a
        # clear backend load error (FR-017), which the run reports and `propose`
        # flags. We do not statically parse for collisions here.
        shape = Shape.SECTION_MULTI_COMPOSE
        supported, reason = True, None
        warnings.append(
            f"{len(targets)} scripts: loading all in one session (multi-compose). "
            "If they define overlapping predicates this is a two-session example "
            "and the load will fail — re-run per file."
        )
    if not goals:
        warnings.append("no goal resolvable from the guide; supply one with --goal")
    return RunnableExample(
        chapter_id=tutorial.id, exercise_number=exercise.number, shape=shape,
        load_targets=targets, goals=goals, golden=golden, guide_path=guide_path,
        guide_text=guide_text, supported=supported, unsupported_reason=reason,
        warnings=tuple(warnings),
    )


def _is_use_case(tutorial: Tutorial, exercise: Exercise) -> bool:
    """A scripts-empty exercise that maps to the ch07 cssg project (D5).

    ch07 exercises 01–07 are the runnable use-case set; 08–12 are SUPERSEDED.
    Generalised by chapter id == ch07 + exercise number ≤ 07.
    """
    if tutorial.id != "ch07":
        return False
    try:
        return 1 <= int(exercise.number) <= 7
    except ValueError:
        return False


def _not_runnable(tutorial, exercise, shape, guide_path, guide_text, reason) -> RunnableExample:
    return RunnableExample(
        chapter_id=tutorial.id, exercise_number=exercise.number, shape=shape,
        load_targets=(), goals=(), golden=(), guide_path=guide_path,
        guide_text=guide_text, supported=False, unsupported_reason=reason, warnings=(),
    )


__all__ = [
    "Shape", "LoadKind", "GoalSource", "Goal", "LoadTarget", "RunnableExample",
    "DEFAULT_SIBLING_CORPUS", "DEFAULT_SIBLING_GLP_ROOT", "resolve_example",
]
