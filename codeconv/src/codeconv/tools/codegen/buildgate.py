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
    if cp.returncode == 0 and not errors:
        return BuildResult(status=BUILD_PASS, raw=output)
    return BuildResult(status=BUILD_FAIL, errors=tuple(errors), raw=output)


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
    if cp.returncode == 0 and not errors:
        return BuildResult(status=BUILD_PASS, test_pass_rate=rate, raw=output)
    return BuildResult(
        status=BUILD_FAIL, errors=tuple(errors), test_pass_rate=rate, raw=output
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
