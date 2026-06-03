"""Live differential capture — spawn the Dart golden + C# candidate REPLs per
``(source, goal)``, collect the recorded trace texts, cross-check the outcome,
and produce the equivalence verdict.

🔴 This is the NONDETERMINISTIC spawn layer. It lives in the CLI/agent layer and
is NEVER invoked from a DBOS step (R12) — the durable ``equiv`` step is a pure
verdict-ingest of the recorded artifacts (T024). The subprocess spawn is
INJECTABLE (``CaptureSpawn``) so the orchestration (outcome parse, decision-2
cross-check, verdict) is unit-testable on recorded fixtures with no live REPL.

Design: ``specs/020-trace-equivalence-fidelity/design-comprehensive-equiv-driver.md``.
Proven invocations (T022 + 2026-06-03 smoke):
  Dart golden : stdin ``:trace\\n:debug\\nload <abs>\\n<goal>\\n:quit\\n``, cwd=repo root
  C# candidate: stdin ``load <abs>\\n<goal>\\n:quit\\n``, env ``GLP_EQUIV_TRACE=<f>``
The Dart ``:trace``/``:debug`` stdout is the golden trace; the C# ``GLP_EQUIV_TRACE``
file is the candidate trace (canonical EV/OUT wire). ``normalize.parse_dart`` /
``parse_csharp`` (via ``verdict.compare_recorded``) consume them.
"""

from __future__ import annotations

import os
import re
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Optional

from codeconv.tools.equiv import verdict as _verdict
from codeconv.tools.equiv.goals import GoalEntry
from codeconv.tools.equiv.relation import Verdict


# --------------------------------------------------------------------------- #
# Config + the injectable spawn seam.                                         #
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class ReplConfig:
    """Where the two REPL executables + the in-place sources live."""

    repo_root: Path
    tutorial_root: Path
    dart_repl: Path
    csharp_repl: Path
    timeout_s: int = 120


def default_config(repo_root: Path | str, tutorial_root: Path | str) -> ReplConfig:
    """The standard exe locations (Dart pre-built; C# Debug build)."""
    rr = Path(repo_root)
    return ReplConfig(
        repo_root=rr,
        tutorial_root=Path(tutorial_root),
        dart_repl=rr / "glp_runtime" / "glp_repl.exe",
        csharp_repl=rr / "out" / "csharp" / "glp_repl" / "bin" / "Debug" / "net10.0" / "glp_repl.exe",
    )


@dataclass(frozen=True)
class RawCapture:
    """Raw text both REPLs produced for one ``(source, goal)`` run."""

    dart_stdout: str          # Dart :trace+:debug stdout == the golden trace text
    candidate_wire: str       # C# GLP_EQUIV_TRACE file content == the candidate trace
    candidate_stdout: str     # C# stdout (for the candidate outcome parse)
    candidate_ran: bool       # the C# REPL ran AND emitted a wire trace
    reason: Optional[str] = None


# (config, source_abs, goal) -> RawCapture. Injected in tests.
CaptureSpawn = Callable[[ReplConfig, str, str], RawCapture]


def _default_spawn(config: ReplConfig, source_abs: str, goal: str) -> RawCapture:
    """Spawn both REPLs. cwd = repo root (so each REPL finds its prelude)."""
    dart_in = f":trace\n:debug\nload {source_abs}\n{goal}\n:quit\n"
    try:
        dproc = subprocess.run(
            [str(config.dart_repl)], input=dart_in, cwd=str(config.repo_root),
            capture_output=True, text=True, timeout=config.timeout_s,
        )
        dart_stdout = dproc.stdout
    except subprocess.TimeoutExpired:
        return RawCapture("", "", "", False, reason="Dart golden REPL timed out")

    tmpdir = Path(tempfile.mkdtemp(prefix="equiv-ct-"))
    ct = tmpdir / "candidate.wire"
    env = {**os.environ, "GLP_EQUIV_TRACE": str(ct)}
    cs_in = f"load {source_abs}\n{goal}\n:quit\n"
    try:
        cproc = subprocess.run(
            [str(config.csharp_repl)], input=cs_in, cwd=str(config.repo_root),
            capture_output=True, text=True, timeout=config.timeout_s, env=env,
        )
        candidate_stdout = cproc.stdout
        wire = ct.read_text(encoding="utf-8") if ct.exists() else ""
        ran = bool(wire.strip())
        reason = None if ran else "C# candidate REPL produced no GLP_EQUIV_TRACE output"
    except subprocess.TimeoutExpired:
        candidate_stdout, wire, ran = "", "", False
        reason = "C# candidate REPL timed out"
    finally:
        try:
            if ct.exists():
                ct.unlink()
            tmpdir.rmdir()
        except OSError:
            pass
    return RawCapture(dart_stdout, wire, candidate_stdout, ran, reason)


# --------------------------------------------------------------------------- #
# REPL outcome parse (Var = value / → status) — the decision-2 cross-check.    #
# --------------------------------------------------------------------------- #
_OUT_BIND = re.compile(r"^(?:GLP>\s*)?(?P<var>[A-Z][A-Za-z0-9_]*)\s*=\s*(?P<val>.+?)\s*$")
_OUT_STATUS = re.compile(r"^(?:GLP>\s*)?→\s*(?P<status>succeeds|suspended|failed)\b")


def parse_repl_outcome(stdout: str) -> tuple[Optional[str], tuple[tuple[str, str], ...]]:
    """Final ``(status, bindings)`` from a REPL stdout — the bindings printed
    immediately before the terminating ``→ status`` line. One goal per run, so
    the last such block is the outcome. ``[DEBUG]`` / reduction lines (lowercase
    head) never match ``_OUT_BIND``."""
    status: Optional[str] = None
    binds: list[tuple[str, str]] = []
    pending: list[tuple[str, str]] = []
    for line in stdout.splitlines():
        ms = _OUT_STATUS.match(line)
        if ms:
            status, binds = ms.group("status"), list(pending)
            pending = []
            continue
        mb = _OUT_BIND.match(line)
        if mb:
            pending.append((mb.group("var"), mb.group("val").strip()))
    return status, tuple(binds)


# --------------------------------------------------------------------------- #
# Capture + compare one (source, goal).                                       #
# --------------------------------------------------------------------------- #
@dataclass(frozen=True)
class CaptureResult:
    golden_trace: str
    candidate_trace: str
    golden_status: Optional[str]
    golden_bindings: tuple[tuple[str, str], ...]
    candidate_status: Optional[str]
    candidate_ran: bool
    reason: Optional[str] = None

    @property
    def have_both(self) -> bool:
        return bool(self.golden_trace.strip()) and self.candidate_ran


def _resolve_source(config: ReplConfig, source: str) -> str:
    """The load path both REPLs accept, run in place (FR-006).

    The capture runs each REPL with cwd = ``repo_root``, and the REPL file loader
    resolves a path verbatim ONLY when it starts with ``/`` / ``./`` / ``../``;
    anything else is rooted at a ``glp/`` workspace dir (glp_repl.dart:193-198),
    so a Windows-absolute ``D:/…`` is mis-resolved. We therefore emit a
    repo-root-relative POSIX path with an explicit ``./`` / ``../`` prefix — the
    sibling tutorial corpus becomes ``../GLP/olamni/tutorial/…`` (no copy)."""
    p = Path(source)
    abs_p = p if p.is_absolute() else (config.tutorial_root / source)
    rel = os.path.relpath(abs_p, config.repo_root).replace("\\", "/")
    if not (rel.startswith("../") or rel.startswith("./") or rel.startswith("/")):
        rel = "./" + rel
    return rel


def capture_pair(
    config: ReplConfig, source: str, goal: str, *, spawn: CaptureSpawn = _default_spawn
) -> CaptureResult:
    """Spawn both REPLs for one ``(source, goal)`` and collect the trace texts +
    parsed outcomes (no verdict yet)."""
    raw = spawn(config, _resolve_source(config, source), goal)
    g_status, g_binds = parse_repl_outcome(raw.dart_stdout)
    c_status, _ = parse_repl_outcome(raw.candidate_stdout)
    return CaptureResult(
        golden_trace=raw.dart_stdout,
        candidate_trace=raw.candidate_wire,
        golden_status=g_status,
        golden_bindings=g_binds,
        candidate_status=c_status,
        candidate_ran=raw.candidate_ran,
        reason=raw.reason,
    )


def _norm_val(v: str) -> str:
    return re.sub(r"\s+", "", v)


def _outcome_matches_expected(cap: CaptureResult, goal: GoalEntry) -> Optional[bool]:
    """Decision-2 cross-check: did the Dart golden re-capture REPRODUCE the
    tutorial's human-approved outcome? ``None`` if the golden outcome could not
    be parsed (capture failure, distinct from a mismatch)."""
    if cap.golden_status is None:
        return None
    if cap.golden_status != goal.expected_status:
        return False
    got = {var: _norm_val(val) for var, val in cap.golden_bindings}
    want = {var: _norm_val(val) for var, val in goal.expected_bindings}
    return got == want


@dataclass(frozen=True)
class GoalVerdict:
    """The full per-goal result: equivalence verdict + the decision-2 outcome
    cross-check + the raw capture (for recording / escalation)."""

    goal: GoalEntry
    verdict: Optional[Verdict]
    capture: CaptureResult
    outcome_matches_expected: Optional[bool]

    @property
    def equivalent(self) -> bool:
        return self.verdict is not None and self.verdict.equivalent

    @property
    def needs_agent_work(self) -> bool:
        return self.verdict is None


def compare_goal(
    config: ReplConfig, goal: GoalEntry, *, spawn: CaptureSpawn = _default_spawn
) -> GoalVerdict:
    """Capture both REPLs for ``goal`` and produce the equivalence verdict.

    Cross-checks the golden against the tutorial's approved outcome (decision 2)
    REGARDLESS of the verdict — a golden that drifts from the approved outcome is
    surfaced (``outcome_matches_expected is False``), never silently recorded.
    When the candidate did not run, ``verdict`` is ``None`` (needs_agent_work),
    a typed state, never a crash (R12 / DISCIPLINE §1.7)."""
    cap = capture_pair(config, goal.source, goal.goal, spawn=spawn)
    outcome = _outcome_matches_expected(cap, goal)
    if not cap.have_both:
        return GoalVerdict(goal, None, cap, outcome)
    result = _verdict.compare_recorded(
        cap.golden_trace,
        cap.candidate_trace,
        compare_mode=goal.compare_mode,
        tier=goal.tier,
    )
    return GoalVerdict(goal, result.verdict, cap, outcome)


__all__ = [
    "ReplConfig",
    "RawCapture",
    "CaptureSpawn",
    "CaptureResult",
    "GoalVerdict",
    "default_config",
    "parse_repl_outcome",
    "capture_pair",
    "compare_goal",
]
