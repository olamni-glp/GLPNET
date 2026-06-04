"""REPL backend drivers — C# (mandated default) + Dart (on demand) — research D6.

PURE / BRIDGE-FREE (research D1): ``subprocess`` + file reads only. No bridge,
no DBOS, no SQLAlchemy/psycopg, no LM. Guarded by ``test_tutorials_no_bridge``.

Both REPLs are line-oriented loops: the driver writes a stdin script
(``<load>`` … ``[:limit N]`` … ``<goal>.`` … ``:quit``) and captures stdout,
which carries the identical outcome grammar for both (so ``outcome.py`` parses
both). Verified 2026-06-04: the C# REPL accepts piped stdin and reproduces the
goldens for both chapter shapes.

C#-failure policy (FR-007/018): a non-working / wrong C# backend is a **critical
P1 defect** — surfaced loudly (exit 8 at the CLI), never a silent hang/crash/pass.
A run ``timeout`` converts a non-terminating goal into a reported P1. An optional
Dart fallback is allowed ONLY with a prominent ``p1_notice`` and never masks the
C# failure.
"""

from __future__ import annotations

import shutil
import subprocess
from dataclasses import dataclass
from enum import Enum
from pathlib import Path

from . import outcome as _oc
from .resolve import RunnableExample


class BackendKind(str, Enum):
    CSHARP = "cs"
    DART = "dart"


_DEFAULT_TIMEOUT = 120  # seconds; bounds a non-terminating goal → reported P1


@dataclass(frozen=True)
class Backend:
    kind: BackendKind
    available: bool
    invocation: list[str]  # argv prefix to launch the REPL
    cwd: str | None  # working directory (Dart resolves relative loads from cwd)
    unavailable_reason: str | None = None


@dataclass(frozen=True)
class BackendResult:
    backend_used: BackendKind
    goal_outcomes: list[tuple[str, _oc.Outcome]]  # (goal-text, actual outcome), in run order
    p1: bool  # a critical C# P1 defect was hit
    p1_notice: str | None
    raw_stdout: str
    error: str | None  # set when the run could not produce outcomes


# --------------------------------------------------------------------------- #
# Backend resolution                                                          #
# --------------------------------------------------------------------------- #
def resolve_backend(kind: BackendKind, *, repo_root: Path, sibling_glp_root: Path) -> Backend:
    """Locate the requested backend's launch argv (D6).

    C#: prefer a built ``glp_repl.exe`` under ``out/csharp/glp_repl/bin``; else
    ``dotnet run --project out/csharp/glp_repl``. Dart: prefer the sibling
    prebuilt ``glp_runtime/glp_repl.exe``; else ``dart run bin/glp_repl.dart``.
    """
    if kind == BackendKind.CSHARP:
        built = sorted((repo_root / "out" / "csharp" / "glp_repl" / "bin").rglob("glp_repl.exe"))
        if built:
            return Backend(kind, True, [str(built[-1])], cwd=str(repo_root))
        if shutil.which("dotnet"):
            proj = repo_root / "out" / "csharp" / "glp_repl"
            if proj.is_dir():
                return Backend(kind, True, ["dotnet", "run", "--project", str(proj)], cwd=str(repo_root))
            return Backend(kind, False, [], None, f"C# project not found: {proj}")
        return Backend(kind, False, [], None,
                       "C# backend unavailable: no built glp_repl.exe and `dotnet` not on PATH")
    # Dart
    exe = sibling_glp_root / "glp_runtime" / "glp_repl.exe"
    if exe.is_file():
        return Backend(kind, True, [str(exe)], cwd=str(sibling_glp_root))
    if shutil.which("dart"):
        script = sibling_glp_root / "glp_runtime" / "bin" / "glp_repl.dart"
        if script.is_file():
            return Backend(kind, True, ["dart", "run", str(script)], cwd=str(sibling_glp_root))
    return Backend(kind, False, [], None,
                   f"Dart backend unavailable: no {exe} and `dart` not on PATH")


# --------------------------------------------------------------------------- #
# stdin script construction (contract repl_backend.md)                         #
# --------------------------------------------------------------------------- #
def build_script(example: RunnableExample, goals, *, limit_override: int | None) -> str:
    """Build the stdin script: load target(s), per-goal ``:limit``, goals, quit."""
    lines: list[str] = [t.exec_path for t in example.load_targets]
    for g in goals:
        lim = limit_override if limit_override is not None else g.needs_limit
        if lim is not None:
            lines.append(f":limit {lim}")
        text = g.text if g.text.strip().endswith(".") else g.text.strip() + "."
        lines.append(text)
    lines.append(":quit")
    return "\n".join(lines) + "\n"


# --------------------------------------------------------------------------- #
# Drive                                                                        #
# --------------------------------------------------------------------------- #
def _drive(backend: Backend, script: str, timeout: int) -> tuple[str, str | None]:
    """Feed ``script`` to ``backend`` over stdin, capture stdout. Returns
    (stdout, error). ``error`` is set on launch failure / timeout / crash."""
    try:
        proc = subprocess.run(
            backend.invocation, input=script, capture_output=True, text=True,
            cwd=backend.cwd, timeout=timeout,
        )
    except subprocess.TimeoutExpired as exc:
        partial = exc.stdout.decode() if isinstance(exc.stdout, bytes) else (exc.stdout or "")
        return partial, f"timed out after {timeout}s (non-terminating goal — bounded to a reported P1)"
    except OSError as exc:
        return "", f"failed to launch backend: {exc}"
    if proc.returncode != 0:
        return proc.stdout, f"backend exited {proc.returncode}: {proc.stderr.strip()[:500]}"
    return proc.stdout, None


def _match_outcomes(stdout: str, goals) -> list[tuple[str, _oc.Outcome]]:
    """Match executed goals (in order) to the stdout outcome blocks.

    Uses :func:`outcome.parse_outcome_segments`, which handles the C# REPL's
    piped stdout (it does NOT echo the goal — the first binding is glued to the
    ``GLP>`` prompt) the same way it handles glued-output goldens.
    """
    outcomes = _oc.parse_outcome_segments(stdout)
    return [(g.text, outcomes[i]) for i, g in enumerate(goals) if i < len(outcomes)]


def run_example(
    example: RunnableExample,
    *,
    backend: BackendKind = BackendKind.CSHARP,
    repo_root: Path,
    sibling_glp_root: Path,
    goals=None,
    limit_override: int | None = None,
    timeout: int = _DEFAULT_TIMEOUT,
    allow_dart_fallback: bool = False,
) -> BackendResult:
    """Run ``example``'s goals on the selected backend, capturing outcome-only.

    Default backend is C# (mandated). A C# failure (unavailable / crash / timeout)
    is a P1: returned with ``p1=True`` and a ``p1_notice``. If
    ``allow_dart_fallback`` and the failure was on C#, retries on Dart and keeps
    the prominent ``p1_notice`` (never masks the C# failure).
    """
    run_goals = list(goals) if goals is not None else list(example.goals)
    primary = resolve_backend(backend, repo_root=repo_root, sibling_glp_root=sibling_glp_root)

    if not primary.available:
        notice = primary.unavailable_reason
        if backend == BackendKind.CSHARP and allow_dart_fallback:
            return _fallback_to_dart(example, run_goals, repo_root, sibling_glp_root,
                                     limit_override, timeout, f"C# P1: {notice}")
        return BackendResult(backend, [], backend == BackendKind.CSHARP, notice, "", notice)

    script = build_script(example, run_goals, limit_override=limit_override)
    stdout, error = _drive(primary, script, timeout)
    if error is not None:
        notice = f"{backend.value} backend error: {error}"
        if backend == BackendKind.CSHARP and allow_dart_fallback:
            return _fallback_to_dart(example, run_goals, repo_root, sibling_glp_root,
                                     limit_override, timeout, f"C# P1: {error}", raw=stdout)
        return BackendResult(backend, _match_outcomes(stdout, run_goals),
                             backend == BackendKind.CSHARP, notice, stdout, error)

    return BackendResult(backend, _match_outcomes(stdout, run_goals), False, None, stdout, None)


def _fallback_to_dart(example, run_goals, repo_root, sibling_glp_root, limit_override, timeout, p1_notice, raw=""):
    dart = resolve_backend(BackendKind.DART, repo_root=repo_root, sibling_glp_root=sibling_glp_root)
    if not dart.available:
        return BackendResult(BackendKind.CSHARP, [], True,
                             f"{p1_notice}; Dart fallback also unavailable: {dart.unavailable_reason}",
                             raw, p1_notice)
    script = build_script(example, run_goals, limit_override=limit_override)
    stdout, error = _drive(dart, script, timeout)
    notice = f"{p1_notice} — fell back to Dart (C# failure NOT masked)"
    return BackendResult(BackendKind.DART, _match_outcomes(stdout, run_goals), True, notice,
                         stdout, error)


__all__ = ["BackendKind", "Backend", "BackendResult", "resolve_backend", "build_script", "run_example"]
