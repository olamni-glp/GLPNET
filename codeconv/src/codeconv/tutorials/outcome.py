"""Outcome-only capture + golden parsing for the run layer (research D7).

PURE / BRIDGE-FREE (research D1). No bridge, no DBOS, no SQLAlchemy/psycopg, no
LM. Stdlib only (``re``). Guarded by ``test_tutorials_no_bridge.py``.

Both REPL backends (C# default, Dart) print the identical *outcome grammar*
(contract ``repl_backend.md``): per goal, zero or more binding lines
``Name = value`` (value may be the literal ``<unbound>``) then exactly one
status line ``→ succeeds | → suspended | → failed``. The golden file
``ex-MM-repl-trace.md`` interleaves ``GLP> <goal>`` prompts with the same
binding+status lines inside fenced code. ONE parser serves stdout, the golden,
and (for the resolver, D3) the goal list — because the golden trace's
``GLP>`` blocks are the one reliable goal source across both chapter shapes
(the ch01 *guide* omits the prompt on goal lines; the *trace* never does).

Outcome-only (FR-008): we capture binding lines + the single status line and
nothing else (no reduction trace, no bytecode, no suspension events). Special
golden kinds (load-failure diagnostics, ``tagged(...)`` side effects, bytecode
dumps) are detected and carried for the 3-Hybrid conformity classifier.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from enum import Enum


class Status(str, Enum):
    """The single terminal status line of a goal (outcome-only, D7)."""

    SUCCEEDS = "succeeds"
    SUSPENDED = "suspended"
    FAILED = "failed"


class GoldenKind(str, Enum):
    """What kind of expected output a golden block carries (3-Hybrid taxonomy).

    ``BINDINGS_STATUS`` is the common case the run layer handles fully. The
    others are recognised so the classifier can *handle* (load-failure,
    side-effect) or *defer-with-flag* (bytecode dump) rather than silently
    mis-compare differently-written examples.
    """

    BINDINGS_STATUS = "bindings_status"
    LOAD_FAILURE = "load_failure"  # `Error loading ...` diagnostic is the expected result
    SIDE_EFFECT = "side_effect"  # `tagged(...)` / `_output` lines (ch07 plays)
    BYTECODE_DUMP = "bytecode_dump"  # `=== BYTECODE FOR .../N ===` block (deferred shape)


# Status line, e.g. ``→ succeeds`` (the arrow may be absent in some renderings).
_STATUS_RE = re.compile(r"^\s*(?:→\s*)?(succeeds|suspended|failed)\s*$")
# Binding line, e.g. ``Xs = [1, a, 2]`` or ``R = <unbound>``. Name is a GLP
# variable (Uppercase/underscore start). Avoids matching ``a = b`` prose.
_BINDING_RE = re.compile(r"^\s*([A-Z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*$")
# A ``GLP> <text>`` prompt line (the goal/load/meta source in a trace).
_PROMPT_RE = re.compile(r"^\s*GLP>\s*(.*?)\s*$")
# Fresh internal variable token, e.g. ``X60`` — varies per session (D7).
_FRESHVAR_RE = re.compile(r"\bX\d+\b")
# Side-effect line emitted by `_output/1` (ch07): ``tagged(alice, cmd(...))``.
_SIDE_EFFECT_RE = re.compile(r"^\s*tagged\(")
# Bytecode disassembly header.
_BYTECODE_RE = re.compile(r"^\s*=== BYTECODE FOR ")
# Load diagnostics.
_LOAD_OK_RE = re.compile(r"^\s*✓ Loaded")
_LOAD_FAIL_RE = re.compile(r"Error loading|Type checking failed|SRSW")


@dataclass(frozen=True)
class Binding:
    """A single ``Name = value`` binding (value may be ``<unbound>``)."""

    name: str
    value: str


@dataclass(frozen=True)
class Outcome:
    """Outcome-only result for one goal (shared by golden + actual, D7)."""

    bindings: tuple[Binding, ...]
    status: Status | None  # None when the block carries no status line (e.g. load-failure)
    kind: GoldenKind = GoldenKind.BINDINGS_STATUS
    side_effects: tuple[str, ...] = ()  # captured `tagged(...)` lines (kind=SIDE_EFFECT)
    raw: str = ""  # the captured text block (provenance)


@dataclass(frozen=True)
class GoalBlock:
    """A ``GLP> <goal>`` block from a trace: the goal text + its outcome."""

    goal: str  # text after ``GLP>`` (a goal, ``.glp`` path, dir, or ``:meta``)
    outcome: Outcome


def normalize_freshvars(value: str) -> str:
    """Canonicalize fresh-var tokens (``X60`` → ``X#``) so two outcomes that
    differ only in per-session variable numbering compare equal (D7)."""
    return _FRESHVAR_RE.sub("X#", value)


def is_load_line(text: str) -> bool:
    """A ``GLP>`` argument that loads a file/project (not a goal, not meta)."""
    t = text.strip()
    if not t or t.startswith(":"):
        return False
    if t.endswith(".glp"):
        return True
    # Bare filesystem path (project dir): has a separator, no call parens, no
    # trailing '.', no spaces — distinguishes a dir load from a goal term.
    looks_path = ("/" in t or "\\" in t) and "(" not in t and not t.endswith(".") and " " not in t
    return looks_path


def is_meta_line(text: str) -> bool:
    """A REPL meta command, e.g. ``:limit 100``, ``:quit``, ``:trace``."""
    return text.strip().startswith(":")


def parse_limit(text: str) -> int | None:
    """Extract N from ``:limit N`` (else None)."""
    m = re.match(r"^\s*:limit\s+(\d+)\s*$", text.strip())
    return int(m.group(1)) if m else None


def _classify_block(lines: list[str]) -> GoldenKind:
    joined = "\n".join(lines)
    if _BYTECODE_RE.search(joined):
        return GoldenKind.BYTECODE_DUMP
    if any(_LOAD_FAIL_RE.search(ln) for ln in lines) and not any(_LOAD_OK_RE.search(ln) for ln in lines):
        return GoldenKind.LOAD_FAILURE
    if any(_SIDE_EFFECT_RE.search(ln) for ln in lines):
        return GoldenKind.SIDE_EFFECT
    return GoldenKind.BINDINGS_STATUS


def parse_outcome_block(lines: list[str]) -> Outcome:
    """Parse the output lines following a goal into an :class:`Outcome` (D7).

    Captures binding lines + the single status line (outcome-only). Recognises
    side-effect (``tagged(...)``) and load-failure/bytecode blocks for the
    classifier. Ignores banners, prompts, ``✓ Loaded`` and trace noise.
    """
    kind = _classify_block(lines)
    bindings: list[Binding] = []
    side_effects: list[str] = []
    status: Status | None = None
    for ln in lines:
        if kind == GoldenKind.SIDE_EFFECT and _SIDE_EFFECT_RE.search(ln):
            side_effects.append(ln.strip())
        sm = _STATUS_RE.match(ln)
        if sm:
            status = Status(sm.group(1))
            continue
        bm = _BINDING_RE.match(ln)
        if bm and not ln.lstrip().startswith("GLP>"):
            bindings.append(Binding(bm.group(1), bm.group(2)))
    return Outcome(
        bindings=tuple(bindings),
        status=status,
        kind=kind,
        side_effects=tuple(side_effects),
        raw="\n".join(lines).strip(),
    )


def parse_transcript(text: str) -> list[GoalBlock]:
    """Split a trace/stdout transcript on ``GLP> <x>`` prompts into blocks.

    Works on the golden ``ex-MM-repl-trace.md`` (fenced) AND raw backend stdout
    (which echoes ``GLP> `` prompts). Each block = the prompt's argument + the
    output lines up to the next prompt. Code-fence lines (```` ``` ````) are
    dropped. The first prompt is typically the load line; ``:meta`` lines carry
    no outcome but are kept (the resolver reads ``:limit`` from them).
    """
    # Strip the GLP> prompt echoes that may be glued to following output by the
    # backend (e.g. ``GLP> ✓ Loaded``): split into logical lines first.
    raw_lines = text.splitlines()
    blocks: list[GoalBlock] = []
    current_goal: str | None = None
    current_lines: list[str] = []

    def flush() -> None:
        if current_goal is not None:
            blocks.append(GoalBlock(goal=current_goal, outcome=parse_outcome_block(current_lines)))

    for ln in raw_lines:
        if ln.strip().startswith("```"):
            continue
        # A line may be ``GLP> <arg>`` possibly with trailing glued output.
        pm = _PROMPT_RE.match(ln)
        if pm is not None and (ln.lstrip().startswith("GLP>")):
            flush()
            current_goal = pm.group(1)
            current_lines = []
            continue
        if current_goal is not None:
            current_lines.append(ln)
    flush()
    return blocks


def _looks_output(text: str) -> bool:
    """True if a line is REPL *output* (binding / status / side-effect / error /
    bytecode), as opposed to a goal echo. Used to handle traces & stdout where
    the first output line is glued to the ``GLP>`` prompt (ch03/ch04, and the
    C# REPL's piped stdout, do not echo the goal)."""
    return bool(
        _BINDING_RE.match(text) or _STATUS_RE.match(text) or _SIDE_EFFECT_RE.search(text)
        or _LOAD_FAIL_RE.search(text) or _BYTECODE_RE.search(text)
    )


def parse_outcome_segments(text: str) -> list[Outcome]:
    """Ordered list of per-goal :class:`Outcome` from a trace OR backend stdout.

    Segments on ``GLP>`` prompts. A segment is skipped when its header is a load
    (``✓ Loaded`` / a ``.glp`` path / a project dir) or ``:meta``. Otherwise it
    is an OUTPUT segment: if the header itself is output-looking (glued-output
    traces / C# stdout), it is the block's first line; if it is a goal echo
    (ch01/ch07), it is dropped and only the following lines form the block. This
    is the one parser that serves every trace style AND both backends (D6/D7).
    """
    segments: list[tuple[str, list[str]]] = []
    cur: tuple[str, list[str]] | None = None
    for ln in text.splitlines():
        if ln.strip().startswith("```"):
            continue
        pm = _PROMPT_RE.match(ln)
        if pm is not None and ln.lstrip().startswith("GLP>"):
            if cur is not None:
                segments.append(cur)
            cur = (pm.group(1), [])
        elif cur is not None:
            cur[1].append(ln)
    if cur is not None:
        segments.append(cur)

    goldens: list[Outcome] = []
    for header, content in segments:
        h = header.strip()
        if h.startswith("✓ Loaded") or (h and (is_load_line(h) or is_meta_line(h))):
            continue
        block = [header, *content] if _looks_output(h) else content
        # Skip wholly-empty segments (e.g. a blank prompt with no output).
        if not any(s.strip() for s in block):
            continue
        goldens.append(parse_outcome_block(block))
    return goldens


def parse_guide_goals(guide_text: str) -> list[tuple[str, int | None]]:
    """Ordered ``(goal_text, needs_limit)`` from a guide's fenced code blocks (D3).

    The guide (``ex-MM-tutorial.md``) is the reliable GOAL source across all
    chapter shapes (some traces don't echo the goal). A goal is a fenced line
    (optionally ``GLP>``-prefixed) that ends with ``.``, is not a load/``:meta``/
    output/shell line. A preceding ``:limit N`` attaches to the next goal.
    """
    goals: list[tuple[str, int | None]] = []
    in_fence = False
    pending_limit: int | None = None
    for ln in guide_text.splitlines():
        s = ln.strip()
        if s.startswith("```"):
            in_fence = not in_fence
            continue
        if not in_fence or not s:
            continue
        pm = _PROMPT_RE.match(ln)
        t = pm.group(1).strip() if (pm and ln.lstrip().startswith("GLP>")) else s
        if not t or t.startswith("✓") or t.startswith("$") or t.startswith("#"):
            continue
        if is_meta_line(t):
            lim = parse_limit(t)
            if lim is not None:
                pending_limit = lim
            continue
        if is_load_line(t) or _looks_output(t):
            continue
        if t.endswith("."):
            goals.append((t, pending_limit))
            pending_limit = None
    return goals


def parse_golden(trace_text: str) -> dict[str, Outcome]:
    """Goal-text → expected :class:`Outcome` from ``ex-MM-repl-trace.md`` (D7).

    Keyed by normalized goal text (stripped, trailing ``.`` removed) so the
    resolver/explain can match an executed goal to its golden. Load lines and
    ``:meta`` blocks are excluded (no outcome to compare).
    """
    out: dict[str, Outcome] = {}
    for blk in parse_transcript(trace_text):
        g = blk.goal.strip()
        if not g or is_meta_line(g) or is_load_line(g):
            continue
        out[_goal_key(g)] = blk.outcome
    return out


def _goal_key(goal: str) -> str:
    g = goal.strip()
    if g.endswith("."):
        g = g[:-1].strip()
    return g


def outcomes_equal(actual: Outcome, golden: Outcome) -> bool:
    """Outcome-only equality with fresh-var normalization (D7).

    Compares status + bindings (name + normalized value); for SIDE_EFFECT
    goldens also compares the normalized ``tagged(...)`` line sequence. Ground
    bindings and status compare verbatim (modulo fresh-var canonicalization).
    """
    if actual.status != golden.status:
        return False
    a_b = [(b.name, normalize_freshvars(b.value)) for b in actual.bindings]
    g_b = [(b.name, normalize_freshvars(b.value)) for b in golden.bindings]
    if a_b != g_b:
        return False
    if golden.kind == GoldenKind.SIDE_EFFECT:
        a_s = [normalize_freshvars(s) for s in actual.side_effects]
        g_s = [normalize_freshvars(s) for s in golden.side_effects]
        if a_s != g_s:
            return False
    return True


__all__ = [
    "Status",
    "GoldenKind",
    "Binding",
    "Outcome",
    "GoalBlock",
    "normalize_freshvars",
    "is_load_line",
    "is_meta_line",
    "parse_limit",
    "parse_outcome_block",
    "parse_transcript",
    "parse_outcome_segments",
    "parse_guide_goals",
    "parse_golden",
    "outcomes_equal",
]
