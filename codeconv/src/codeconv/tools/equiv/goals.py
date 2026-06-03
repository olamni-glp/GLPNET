"""Goal-bearing equivalence corpus from the sibling GLP tutorial set.

PURE — filesystem reads only; no bridge, no LM, no REPL spawn
(``test_no_lm_on_production_path`` guards the import surface).

``corpus.py`` enumerates + tags the GLP sources but carries NO goal; a
behavioural trace needs a goal. This module supplies the
``(source, goal, expected_outcome)`` triples the differential oracle runs,
parsed from the human-approved tutorial ``ex-NN-repl-trace.md`` captures.

Design: ``specs/020-trace-equivalence-fidelity/design-comprehensive-equiv-driver.md``
(ratified 2026-06-03). The tutorials run IN PLACE under a configured sibling
tutorial root (FR-006 single-source — never copied). See
``[[reference_glp_tutorial_corpus]]``.

🔴 The ``repl-trace.md`` golden is OUTCOME-ONLY (``Var=…`` + ``→ status``, no
``:trace`` instruction spine). So ``expected_status``/``expected_bindings`` here
are a human-validated ASSERTION the driver's own Dart golden re-capture must
reproduce (decision 2) — NOT the instruction-level golden the relation compares.

Goal-format variation handled (measured across ch01–06):
  * ch01/02/05/06 — goal IN-FENCE: ``GLP> append([1,2,3],[a,b,c],Zs).`` then the
    bindings then ``→ succeeds``.
  * ch03/04 — goal IN-PROSE *after* the block: a fence ``GLP> R = 1`` / ``→ succeeds``
    then ``Goal: `and(1, 1, R).` — …`` on the next prose line.
Source↔goal is exact via the ``✓ Loaded: <path>`` context (the active file).
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional

import yaml

STRICT = "strict"
TRACE = "trace"

GOALS_PARTS = (".codeconv", "equiv-manifest", "goals.yml")

# Decision 4: approved single-file strict chapters first. ch07 (project-load,
# multi-goal plays) + ch04 ex06–10 ("pending review") are deferred follow-ons.
APPROVED_CHAPTERS = ("ch01", "ch02", "ch03", "ch04", "ch05", "ch06")
PENDING_EXERCISES = frozenset(
    ("ch04", f"exercise-{n:02d}") for n in range(6, 11)
)

# The path segment that re-roots an absolute-or-relative loaded path onto the
# configured tutorial root (a loaded path is shown as either
# ``D:/…/olamni/tutorial/ch02/…glp`` or ``olamni/tutorial/ch02/…glp``).
_ROOT_SEGMENT = "olamni/tutorial/"


@dataclass(frozen=True)
class GoalEntry:
    """One ``(source, goal, expected_outcome)`` triple for the oracle.

    ``source`` is POSIX, relative to the tutorial root (run in place). ``goal``
    is the runnable goal incl. trailing ``.``. ``expected_*`` is the human-
    approved outcome cross-check (decision 2)."""

    source: str
    goal: str
    expected_status: str
    expected_bindings: tuple[tuple[str, str], ...]
    origin: str
    tier: str = STRICT
    compare_mode: str = TRACE


# --------------------------------------------------------------------------- #
# Line tokenizer for a repl-trace.md.                                          #
# --------------------------------------------------------------------------- #
_LOADED = re.compile(r"(?:GLP>\s*)?✓\s*Loaded:\s*(?P<path>\S+)")
_GOAL_FENCE = re.compile(r"^GLP>\s*(?P<goal>[a-z_][A-Za-z0-9_]*\(.*\))\s*\.?\s*$")
_GOAL_PROSE = re.compile(r"^\s*Goal:\s*`(?P<goal>[^`]+?)`")
_BIND = re.compile(r"^(?:GLP>\s*)?(?P<var>[A-Z][A-Za-z0-9_]*)\s*=\s*(?P<val>.+?)\s*$")
_STATUS = re.compile(r"^→\s*(?P<status>succeeds|suspended|failed)\b")


def _norm_goal(goal: str) -> str:
    g = goal.strip()
    if not g.endswith("."):
        g += "."
    return g


def _reroot(path: str) -> Optional[str]:
    """``…/olamni/tutorial/ch02/x.glp`` → ``ch02/x.glp`` (relative to the root)."""
    p = path.replace("\\", "/")
    idx = p.find(_ROOT_SEGMENT)
    if idx >= 0:
        return p[idx + len(_ROOT_SEGMENT):]
    return None


def parse_trace_goals(trace_text: str, *, origin: str) -> list[GoalEntry]:
    """Extract ``GoalEntry`` rows from one ``repl-trace.md`` (both goal formats).

    Tokenize, then pair each result block (binds + ``→ status``) with its goal:
    the nearest in-fence goal BEFORE the block (P1), else the nearest ``Goal:``
    prose AFTER it (P2). A block with no associable goal or no active loaded
    source is skipped (load confirmations, error demos) — never fabricated."""
    events: list[tuple[str, object]] = []
    for raw in trace_text.splitlines():
        line = raw.rstrip("\n")
        m = _LOADED.search(line)
        if m:
            events.append(("load", m.group("path")))
            continue
        m = _GOAL_PROSE.match(line)
        if m:
            events.append(("goalprose", _norm_goal(m.group("goal"))))
            continue
        m = _GOAL_FENCE.match(line)
        if m:
            events.append(("goalfence", _norm_goal(m.group("goal"))))
            continue
        m = _STATUS.match(line)
        if m:
            events.append(("status", m.group("status")))
            continue
        m = _BIND.match(line)
        if m:
            events.append(("bind", (m.group("var"), m.group("val").strip())))
            continue

    # Build result blocks (binds since the last status/goalfence, terminated by a
    # status), tracking the active loaded source. Record each block's event span
    # so the prose-after-goal (P2) can be associated.
    blocks: list[dict] = []
    current_source: Optional[str] = None
    pending_binds: list[tuple[str, str]] = []
    pending_fence: Optional[str] = None
    for i, (kind, val) in enumerate(events):
        if kind == "load":
            rel = _reroot(str(val))
            if rel:
                current_source = rel
            pending_binds, pending_fence = [], None
        elif kind == "goalfence":
            pending_fence = str(val)
            pending_binds = []
        elif kind == "bind":
            pending_binds.append(val)  # type: ignore[arg-type]
        elif kind == "status":
            blocks.append(
                {
                    "source": current_source,
                    "fence_goal": pending_fence,
                    "bindings": tuple(pending_binds),
                    "status": val,
                    "end_idx": i,
                }
            )
            pending_binds, pending_fence = [], None

    # P2: associate a trailing ``Goal:`` prose with the block it follows (before
    # the next block / next goal).
    out: list[GoalEntry] = []
    for b_i, b in enumerate(blocks):
        goal = b["fence_goal"]
        if goal is None:
            nxt = blocks[b_i + 1]["end_idx"] if b_i + 1 < len(blocks) else len(events)
            for j in range(b["end_idx"] + 1, nxt):
                if events[j][0] == "goalprose":
                    goal = str(events[j][1])
                    break
        if goal is None or b["source"] is None:
            continue  # load confirmation / error demo / unassociable — skip
        out.append(
            GoalEntry(
                source=b["source"],
                goal=goal,
                expected_status=str(b["status"]),
                expected_bindings=b["bindings"],
                origin=origin,
            )
        )
    return out


# --------------------------------------------------------------------------- #
# Walk the tutorial tree → the one-shot seed (reviewed → goals.yml, g1=c).     #
# --------------------------------------------------------------------------- #
def discover_tutorials(tutorial_root: Path | str) -> list[GoalEntry]:
    """Parse every approved ch01–06 exercise's ``repl-trace.md`` into GoalEntry
    rows. Deterministic, sorted. Pending (ch04 ex06–10) excluded. The OUTPUT is
    reviewed + written to ``goals.yml`` — this is the bootstrap, not the runtime
    read path (mirrors ``corpus.discover_from_suites``)."""
    root = Path(tutorial_root)
    rows: list[GoalEntry] = []
    for chapter in APPROVED_CHAPTERS:
        ch_dir = root / chapter
        if not ch_dir.is_dir():
            continue
        for ex_dir in sorted(p for p in ch_dir.iterdir() if p.is_dir()):
            if (chapter, ex_dir.name) in PENDING_EXERCISES:
                continue
            traces = sorted(ex_dir.glob("ex-*-repl-trace.md"))
            for trace in traces:
                origin = f"tutorial:{chapter}/{ex_dir.name}"
                rows.extend(
                    parse_trace_goals(trace.read_text(encoding="utf-8"), origin=origin)
                )
    return sorted(rows, key=lambda e: (e.source, e.goal))


# --------------------------------------------------------------------------- #
# Reviewed artifact (g1=c): write goals.yml, read it back at runtime.          #
# --------------------------------------------------------------------------- #
def to_yaml(entries: Iterable[GoalEntry]) -> str:
    """Render the reviewed ``goals.yml`` (provenance header + safe-dumped rows).

    ``yaml.safe_dump`` quotes the binding values (``[1, 2, 3]`` etc.) correctly
    — hand-rolling that quoting would be fragile."""
    rows = [
        {
            "source": e.source,
            "goal": e.goal,
            "expected_status": e.expected_status,
            "expected_bindings": [list(b) for b in e.expected_bindings],
            "origin": e.origin,
            "tier": e.tier,
            "compare_mode": e.compare_mode,
        }
        for e in entries
    ]
    header = (
        "# Goal-bearing equivalence corpus — feature 020 (codeconv-equiv).\n"
        "#\n"
        "# Reviewed, checked-in (g1=c). Seeded once by goals.discover_tutorials\n"
        "# parsing the sibling GLP tutorial repl-trace.md captures\n"
        "# (D:/bstdev/research/glp/GLP/olamni/tutorial, ch01-06 approved).\n"
        "# Each (source, goal) is run IN PLACE under the tutorial root (no copy;\n"
        "# FR-006). expected_* is the human-approved OUTCOME the driver's own Dart\n"
        "# golden re-capture must reproduce (decision 2) — NOT the instruction\n"
        "# golden. Design: specs/020-.../design-comprehensive-equiv-driver.md.\n"
        "#\n"
        "# Regenerate (then review the diff) with:\n"
        "#   python -c \"from codeconv.tools.equiv import goals; \\\n"
        "#     goals.write_artifacts('.', 'D:/bstdev/research/glp/GLP/olamni/tutorial')\"\n"
    )
    return header + yaml.safe_dump(
        {"version": 1, "goals": rows},
        sort_keys=False,
        allow_unicode=True,
        width=10000,
    )


def load(repo_root: Path | str) -> tuple[GoalEntry, ...]:
    """Parse ``.codeconv/equiv-manifest/goals.yml`` into ``GoalEntry`` rows.

    Raises on a malformed/missing artifact (mis-authoring, not a tolerated
    state — the seed must have run and been reviewed)."""
    path = Path(repo_root).joinpath(*GOALS_PARTS)
    raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError(f"goals {path}: top level must be a mapping")
    out: list[GoalEntry] = []
    for r in raw.get("goals") or []:
        out.append(
            GoalEntry(
                source=str(r["source"]),
                goal=str(r["goal"]),
                expected_status=str(r["expected_status"]),
                expected_bindings=tuple(
                    (str(b[0]), str(b[1])) for b in (r.get("expected_bindings") or [])
                ),
                origin=str(r["origin"]),
                tier=str(r.get("tier", STRICT)),
                compare_mode=str(r.get("compare_mode", TRACE)),
            )
        )
    return tuple(out)


def write_artifacts(repo_root: Path | str, tutorial_root: Path | str) -> int:
    """Seed → write ``goals.yml``. Returns the goal count (for review). One-shot,
    re-runnable; the diff is what gets reviewed."""
    entries = discover_tutorials(tutorial_root)
    path = Path(repo_root).joinpath(*GOALS_PARTS)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(to_yaml(entries), encoding="utf-8")
    return len(entries)


__all__ = [
    "STRICT",
    "TRACE",
    "GOALS_PARTS",
    "APPROVED_CHAPTERS",
    "PENDING_EXERCISES",
    "GoalEntry",
    "parse_trace_goals",
    "discover_tutorials",
    "to_yaml",
    "load",
    "write_artifacts",
]
