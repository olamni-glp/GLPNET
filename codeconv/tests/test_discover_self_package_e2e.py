"""End-to-end tests for ``codeconv discover`` self-package rewrite
— Feature 014 / FR-001..FR-008.

Maps to `contracts/workflow_contract.md` § "test_discover_self_package_e2e.py".

Integration tests: spawn the unified bridge, migrate, run discover, and
assert against the emitted ``--json`` summary plus the tombstone tree.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest
import yaml

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _write(p: Path, text: str) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")


@needs_bridge
def test_heap_fcp_style_fanin(discover_repo: Path) -> None:
    """A heap_fcp.dart-style fixture using ``package:<self>/...`` imports
    must resolve to four in-subtree edges + tombstone with four deps."""
    sub = discover_repo / "glp_runtime_net"
    _write(
        sub / "pubspec.yaml",
        "name: glp_runtime\nversion: 1.0.0\n",
    )
    # heap_fcp-style importer with four self-package imports.
    _write(
        sub / "lib" / "runtime" / "heap_fcp.dart",
        "/// Heap FCP.\n"
        "library;\n"
        "\n"
        "import 'package:glp_runtime/runtime/terms.dart';\n"
        "import 'package:glp_runtime/runtime/suspension.dart';\n"
        "import 'package:glp_runtime/runtime/machine_state.dart';\n"
        "import 'package:glp_runtime/multiagent/variable_table.dart';\n"
        "\n"
        "class Heap {}\n",
    )
    # The four imported files.
    _write(sub / "lib" / "runtime" / "terms.dart", "class Terms {}\n")
    _write(
        sub / "lib" / "runtime" / "suspension.dart",
        "class Suspension {}\n",
    )
    _write(
        sub / "lib" / "runtime" / "machine_state.dart",
        "class Machine {}\n",
    )
    _write(
        sub / "lib" / "multiagent" / "variable_table.dart",
        "class VariableEntry {}\n",
    )

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, f"migrate failed: {proc.stderr}"

    proc = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc.returncode == 0, f"discover failed: {proc.stderr}"
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["files_walked"] == 5
    assert summary["files_processed"] == 5

    # Tombstone: four lex-sorted deps.
    tomb = (
        discover_repo
        / ".codeconv"
        / "tombstones"
        / "lib"
        / "runtime"
        / "heap_fcp.dart.md"
    )
    text = tomb.read_text(encoding="utf-8")
    parts = text.split("---", 2)
    fm = yaml.safe_load(parts[1])
    deps = fm.get("dependencies") or []
    expected = sorted(
        [
            "lib/multiagent/variable_table.dart",
            "lib/runtime/machine_state.dart",
            "lib/runtime/suspension.dart",
            "lib/runtime/terms.dart",
        ]
    )
    assert deps == expected, (
        f"heap_fcp.dart tombstone deps wrong: got {deps!r}, expected "
        f"{expected!r}"
    )

    # No pubspec_missing warning when pubspec is present + valid.
    warnings_list = summary.get("warnings", [])
    assert not any(
        w.get("kind") == "pubspec_missing" for w in warnings_list
    ), f"unexpected pubspec_missing warning: {warnings_list!r}"


@needs_bridge
def test_external_package_still_skipped_e2e(discover_repo: Path) -> None:
    """``package:meta/meta.dart`` and other external package: targets
    remain silently skipped — no edge, no warning."""
    sub = discover_repo / "glp_runtime_net"
    _write(
        sub / "pubspec.yaml",
        "name: glp_runtime\nversion: 1.0.0\n",
    )
    _write(
        sub / "lib" / "main.dart",
        "import 'package:meta/meta.dart';\n"
        "import 'package:json_annotation/json_annotation.dart';\n"
        "class M {}\n",
    )

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, f"migrate failed: {proc.stderr}"

    proc = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc.returncode == 0, f"discover failed: {proc.stderr}"
    summary = json.loads(_extract_json(proc.stdout))
    # No edges recorded.
    assert summary["imports"] == 0

    tomb = (
        discover_repo
        / ".codeconv"
        / "tombstones"
        / "lib"
        / "main.dart.md"
    )
    text = tomb.read_text(encoding="utf-8")
    parts = text.split("---", 2)
    fm = yaml.safe_load(parts[1])
    deps = fm.get("dependencies") or []
    assert deps == [], (
        f"external package: imports must produce no deps; got {deps!r}"
    )

    # No package_missing warnings; no skip warnings for the external targets.
    warnings_list = summary.get("warnings", [])
    assert not any(
        w.get("kind") == "pubspec_missing" for w in warnings_list
    ), f"unexpected pubspec_missing warning: {warnings_list!r}"


@needs_bridge
def test_pubspec_absent_falls_back_to_isolated(discover_repo: Path) -> None:
    """Subtree without pubspec.yaml: discover succeeds, emits exactly one
    ``pubspec_missing`` warning, every ``package:<anything>/...`` import
    is skipped (back to feature-012 behaviour)."""
    sub = discover_repo / "glp_runtime_net"
    # NO pubspec.yaml.
    _write(
        sub / "lib" / "main.dart",
        "import 'package:glp_runtime/runtime/terms.dart';\n"
        "class M {}\n",
    )
    _write(sub / "lib" / "runtime" / "terms.dart", "class Terms {}\n")

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, f"migrate failed: {proc.stderr}"

    proc = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc.returncode == 0, f"discover failed: {proc.stderr}"
    summary = json.loads(_extract_json(proc.stdout))

    warnings_list = summary.get("warnings", [])
    missing = [w for w in warnings_list if w.get("kind") == "pubspec_missing"]
    assert len(missing) == 1, (
        f"expected exactly one pubspec_missing warning; got {warnings_list!r}"
    )
    assert missing[0]["reason"] == "absent"

    # The self-package import is skipped — main.dart has no deps.
    tomb = (
        discover_repo
        / ".codeconv"
        / "tombstones"
        / "lib"
        / "main.dart.md"
    )
    text = tomb.read_text(encoding="utf-8")
    parts = text.split("---", 2)
    fm = yaml.safe_load(parts[1])
    deps = fm.get("dependencies") or []
    assert deps == [], (
        f"with pubspec absent, package:<anything>/... must be skipped; "
        f"got main.dart deps = {deps!r}"
    )
