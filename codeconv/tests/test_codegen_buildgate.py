"""T013 — build-gate parse + (dotnet-gated) live build pass/fail.

The pure parser tests (``parse_build_errors`` / ``parse_test_pass_rate``)
always run. The live ``dotnet build`` tests are skipped when no .NET SDK
is on PATH. A non-compiling project ⇒ ``fail`` (the hard gate, metric
floor 0.0) — never an exception (contract dbos_codegen_stage.md).
"""

from __future__ import annotations

from pathlib import Path

import pytest

from codeconv.tools.codegen.buildgate import (
    BUILD_FAIL,
    BUILD_PASS,
    dotnet_available,
    parse_build_errors,
    parse_test_pass_rate,
    run_build,
)

needs_dotnet = pytest.mark.skipif(
    not dotnet_available(), reason="requires the .NET SDK on PATH"
)


def test_parse_build_errors_dedup_and_extract() -> None:
    out = (
        "Cell.cs(10,5): error CS0246: type 'Foo' not found [proj.csproj]\n"
        "Cell.cs(10,5): error CS0246: type 'Foo' not found [proj.csproj]\n"
        "Cell.cs(12,1): error CS1002: ; expected [proj.csproj]\n"
        "Build FAILED.\n"
    )
    errs = parse_build_errors(out)
    assert len(errs) == 2  # CS0246 deduped, CS1002 distinct
    assert any("CS0246" in e for e in errs)
    assert any("CS1002" in e for e in errs)


def test_parse_build_errors_clean() -> None:
    assert parse_build_errors("Build succeeded.\n0 Warning(s)\n0 Error(s)\n") == []


def test_parse_test_pass_rate() -> None:
    assert parse_test_pass_rate("Failed: 0, Passed: 12, Skipped: 0") == 1.0
    assert parse_test_pass_rate("Failed: 3, Passed: 9") == 0.75
    assert parse_test_pass_rate("no summary here") is None


def _write_classlib(root: Path, body: str) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    (root / "proj.csproj").write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
        "  <PropertyGroup>\n"
        "    <TargetFramework>net10.0</TargetFramework>\n"
        "    <Nullable>enable</Nullable>\n"
        "    <ImplicitUsings>enable</ImplicitUsings>\n"
        "  </PropertyGroup>\n"
        "</Project>\n",
        encoding="utf-8",
    )
    (root / "Lib.cs").write_text(body, encoding="utf-8")
    return root / "proj.csproj"


@needs_dotnet
def test_live_build_passes_on_valid_csharp(tmp_path: Path) -> None:
    csproj = _write_classlib(
        tmp_path / "ok",
        "namespace Demo;\npublic class Cell { public int Value; }\n",
    )
    res = run_build(csproj, timeout=300.0)
    # not_built means SDK/env issue (e.g. offline restore) — don't assert
    # pass in that case; the parser tests already cover correctness.
    if res.status == "not_built":
        pytest.skip(f"dotnet env unavailable: {res.reason}")
    assert res.status == BUILD_PASS, res.errors


@needs_dotnet
def test_live_build_fails_on_broken_csharp(tmp_path: Path) -> None:
    csproj = _write_classlib(
        tmp_path / "bad",
        "namespace Demo;\npublic class Cell { public Nope Value; }\n",
    )
    res = run_build(csproj, timeout=300.0)
    if res.status == "not_built":
        pytest.skip(f"dotnet env unavailable: {res.reason}")
    assert res.status == BUILD_FAIL
    assert res.errors  # parsed compiler diagnostics, not an exception
