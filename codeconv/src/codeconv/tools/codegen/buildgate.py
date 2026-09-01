"""Deterministic ``dotnet build`` (and, Inc-2, ``dotnet test``) gate.

Implements ``specs/019-codeconv-codegen/contracts/metric_contract.md`` §
"Build gate (hard)": a non-compiling candidate scores 0.0 — there is no
partial credit. This module shells out to the .NET SDK and parses the
result into a typed :class:`BuildResult`; it makes NO judgement about
code quality and runs NO LLM/network. It is reused verbatim by both the
production gate (``tools/codegen/workflow``) and the optimizer metric
(``codegen_opt/metric``) so the two agree by construction.

Build failure is NOT raised as an exception — it is a recorded
``build_status='fail'`` feedback fact (contract dbos_codegen_stage.md §
Invariants). Only an environment failure (no SDK, timeout) is surfaced
as a distinct ``not_built`` status with a reason.

🔴 INSTANCE 6 AND WHY ``pass`` IS NOT ``PASS`` (task T054, feature 078).
Inventory CD-03: *"a build gate is compile-only, so a behaviourally-wrong
generated file can be promoted."* ``run_build`` returning ``pass`` is a true
statement about ONE dimension — it compiles — and it was being read as a
statement about promotability, which has two: **compiles** and **behaves**.

So ``BuildResult`` now records WHICH dimensions it examined, and
:func:`emit_gate_receipt` maps that onto a 078 receipt where the counts carry the
distinction the status word cannot:

    run_build  clean   -> examined 1 / total 2 -> UNREAD  (compile only)
    run_test   clean, tests ran  -> 2 / 2      -> PASS
    run_test   clean, ZERO tests -> 1 / 2      -> UNREAD  (nothing behavioural
                                                  was examined; a vacuous green
                                                  is instance 12)
    fail                          -> 2 / 2 + problems -> FAIL
    not_built                     -> unresolved target -> UNSEARCHABLE

The ``status`` field is UNCHANGED and still means exactly what it always meant —
callers scoring candidates keep working. What is new is that the gate can now
state, in a receipt, what it did *not* look at.
"""

from __future__ import annotations

import re
import shutil
import subprocess
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional


# A C# compiler diagnostic line: ``…: error CS0246: …`` (also matches
# MSBuild ``error MSBxxxx``). We count distinct ``error`` diagnostics.
_ERROR_LINE = re.compile(r":\s*error\s+([A-Z]{1,4}\d+)\s*:\s*(.*)", re.IGNORECASE)
# ``dotnet test`` summary: ``Passed!  - Failed: 0, Passed: 12, …`` /
# ``Failed: 3, Passed: 9``. Parse Passed/Failed counts for test_pass_rate.
_TEST_PASSED = re.compile(r"Passed:\s*(\d+)", re.IGNORECASE)
_TEST_FAILED = re.compile(r"Failed:\s*(\d+)", re.IGNORECASE)

BUILD_PASS = "pass"
BUILD_FAIL = "fail"
NOT_BUILT = "not_built"

#: The dimensions a build gate can examine. Promotability requires BOTH; a gate
#: that examines only ``compiles`` has examined half the criteria (instance 6).
DIM_COMPILES = "compiles"
DIM_BEHAVES = "behaves"
GATE_DIMENSIONS = (DIM_COMPILES, DIM_BEHAVES)


@dataclass(frozen=True)
class BuildResult:
    """Outcome of a build (and optionally a test) invocation.

    ``status`` is one of ``pass｜fail｜not_built`` (the
    ``dart_codegen.build_status`` domain). ``errors`` are the parsed
    compiler diagnostics (``(code, message)`` tuples flattened to
    strings). ``test_pass_rate`` is set only when tests were run (Inc-2),
    else ``None``.
    """

    status: str
    errors: tuple[str, ...] = ()
    test_pass_rate: Optional[float] = None
    reason: Optional[str] = None  # populated for not_built (env failure)
    raw: str = field(default="", repr=False)
    #: Which of :data:`GATE_DIMENSIONS` this invocation ACTUALLY examined. Empty
    #: means none were (an environment failure). Never inferred from ``status``:
    #: a green status is exactly what cannot tell you what was looked at.
    dimensions_examined: tuple[str, ...] = ()

    @property
    def passed(self) -> bool:
        return self.status == BUILD_PASS


def dotnet_available() -> bool:
    """True iff the ``dotnet`` SDK CLI is on PATH (test gating)."""
    return shutil.which("dotnet") is not None


def parse_build_errors(output: str) -> list[str]:
    """Parse distinct ``error CSxxxx``/``error MSBxxxx`` diagnostics.

    Deduplicated, order-preserving — dotnet repeats the same error per
    target framework / project reference.
    """
    seen: set[str] = set()
    out: list[str] = []
    for m in _ERROR_LINE.finditer(output):
        line = f"{m.group(1)}: {m.group(2).strip()}"
        if line not in seen:
            seen.add(line)
            out.append(line)
    return out


def parse_test_pass_rate(output: str) -> Optional[float]:
    """Parse ``test_pass_rate ∈ [0,1]`` from ``dotnet test`` output.

    ``passed / (passed + failed)``; ``None`` if no counts are present.
    A run with 0 tests is treated as ``1.0`` (vacuously passing — nothing
    failed), consistent with the metric's "no tests in scope" Inc-1 path.
    """
    passed = sum(int(m.group(1)) for m in _TEST_PASSED.finditer(output))
    failed = sum(int(m.group(1)) for m in _TEST_FAILED.finditer(output))
    total = passed + failed
    if total == 0:
        return None
    return passed / total


def run_build(
    project: Path,
    *,
    timeout: float = 600.0,
    extra_args: Optional[list[str]] = None,
) -> BuildResult:
    """Run ``dotnet build`` on ``project`` (a .csproj or a directory).

    Returns a :class:`BuildResult`. A clean build ⇒ ``pass``; compiler
    errors ⇒ ``fail`` (with parsed diagnostics — NOT an exception); a
    missing SDK or a timeout ⇒ ``not_built`` with a ``reason`` (an
    environment failure the caller escalates, not a code defect).
    """
    if not dotnet_available():
        return BuildResult(status=NOT_BUILT, reason="dotnet SDK not on PATH")
    proj = Path(project)
    args = [
        "dotnet",
        "build",
        str(proj),
        "--nologo",
        "-v",
        "quiet",
        *(extra_args or []),
    ]
    try:
        cp = subprocess.run(
            args,
            capture_output=True,
            text=True,
            timeout=timeout,
            cwd=str(proj if proj.is_dir() else proj.parent),
        )
    except subprocess.TimeoutExpired:
        return BuildResult(
            status=NOT_BUILT, reason=f"dotnet build timed out after {timeout}s"
        )
    output = (cp.stdout or "") + "\n" + (cp.stderr or "")
    errors = parse_build_errors(output)
    # COMPILE ONLY. Nothing behavioural was examined, whatever the status says.
    dims = (DIM_COMPILES,)
    if cp.returncode == 0 and not errors:
        return BuildResult(status=BUILD_PASS, raw=output, dimensions_examined=dims)
    return BuildResult(status=BUILD_FAIL, errors=tuple(errors), raw=output,
                       dimensions_examined=dims)


def run_test(
    project: Path,
    *,
    timeout: float = 900.0,
    extra_args: Optional[list[str]] = None,
) -> BuildResult:
    """Run ``dotnet test`` (Inc-2) — build + execute, parse pass rate.

    A build/compile failure ⇒ ``fail`` (no tests ran). A successful
    compile with a test summary ⇒ ``pass`` with ``test_pass_rate``
    populated (``None`` when the project has 0 tests). Missing SDK /
    timeout ⇒ ``not_built``.
    """
    if not dotnet_available():
        return BuildResult(status=NOT_BUILT, reason="dotnet SDK not on PATH")
    proj = Path(project)
    args = [
        "dotnet",
        "test",
        str(proj),
        "--nologo",
        "-v",
        "quiet",
        *(extra_args or []),
    ]
    try:
        cp = subprocess.run(
            args,
            capture_output=True,
            text=True,
            timeout=timeout,
            cwd=str(proj if proj.is_dir() else proj.parent),
        )
    except subprocess.TimeoutExpired:
        return BuildResult(
            status=NOT_BUILT, reason=f"dotnet test timed out after {timeout}s"
        )
    output = (cp.stdout or "") + "\n" + (cp.stderr or "")
    errors = parse_build_errors(output)
    rate = parse_test_pass_rate(output)
    # The BEHAVES dimension counts as examined only if tests actually RAN. rate is
    # None when the project reported no counts at all, and zero tests is not
    # evidence of behaviour -- it is the absence of it (instance 12).
    dims = (DIM_COMPILES, DIM_BEHAVES) if rate is not None else (DIM_COMPILES,)
    if cp.returncode == 0 and not errors:
        return BuildResult(status=BUILD_PASS, test_pass_rate=rate, raw=output,
                           dimensions_examined=dims)
    return BuildResult(
        status=BUILD_FAIL, errors=tuple(errors), test_pass_rate=rate, raw=output,
        dimensions_examined=dims
    )


__all__ = [
    "BUILD_FAIL",
    "BUILD_PASS",
    "NOT_BUILT",
    "BuildResult",
    "dotnet_available",
    "parse_build_errors",
    "parse_test_pass_rate",
    "run_build",
    "run_test",
]


# ---- 078 receipt emission (task T054, instance 6) ---------------------------

def gate_counts(result: "BuildResult") -> tuple[int, int]:
    """``(examined, total)`` over :data:`GATE_DIMENSIONS` for one gate run.

    The total is the number of dimensions that MUST be examined for a promotion
    decision, not the number this run happened to look at. That asymmetry is the
    whole point: a compile-only run is 1 of 2, and 1 of 2 is UNREAD.
    """
    return len(result.dimensions_examined), len(GATE_DIMENSIONS)


def emit_gate_receipt(
    result: "BuildResult",
    *,
    project,
    run_id: str,
    root,
    check_id: str = "codegen.build-gate",
    area: str = "build-gate",
    write: bool = True,
):
    """Emit the 078 receipt for a build-gate run (FR-001/003/006, instance 6).

    The outcome is DERIVED from what was resolved and examined, never from
    ``result.status``:

    * ``not_built`` -> the target could not be resolved (no SDK, timeout), so the
      receipt is UNSEARCHABLE and carries the environment reason. It is emphatically
      not a pass, and equally not a code FAIL -- conflating the two is how an
      environment outage gets recorded as a clean gate.
    * ``fail`` -> both dimensions are treated as examined (the compiler looked and
      reported), the diagnostics are the problems, so the outcome is FAIL.
    * ``pass`` -> examined = the dimensions actually looked at. Compile-only gives
      1 of 2 and classifies UNREAD; a test run that really executed tests gives
      2 of 2 and classifies PASS.

    Returns the :class:`codeconv.receipts.Receipt`.
    """
    from codeconv.receipts import Target, emit  # local: keep the gate importable standalone

    proj = str(project)
    if result.status == NOT_BUILT:
        return emit(
            check_id=check_id, area=area,
            target=Target(kind="path", identity=proj, resolved=False,
                          unresolved_reason=result.reason or "build not attempted"),
            examined_count=0, total_count=None,
            run_id=run_id, root=root, write=write,
        )

    examined_dims = (
        GATE_DIMENSIONS if result.status == BUILD_FAIL else result.dimensions_examined
    )
    examined, total = len(examined_dims), len(GATE_DIMENSIONS)
    return emit(
        check_id=check_id, area=area,
        target=Target(kind="path", identity=proj, resolved=True),
        examined_count=examined, total_count=total,
        examined=list(examined_dims),
        problems=list(result.errors),
        run_id=run_id, root=root, write=write,
    )


__all__ += [
    "DIM_BEHAVES",
    "DIM_COMPILES",
    "GATE_DIMENSIONS",
    "emit_gate_receipt",
    "gate_counts",
]
