"""Tests for ``codeconv discover`` outside-subtree warning — Phase 6 / US4 / T066.

Maps to FR-023 / SC-011:

- ``test_outside_caller_warns_no_edge`` — a synthetic ``.dart`` file
  outside ``glp_runtime_net/`` (e.g. in ``glp_runtime/``) imports an
  inside file; discover MUST emit a warning naming both files and MUST
  NOT record a caller edge for it.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


@needs_bridge
def test_outside_caller_warns_no_edge(discover_repo: Path) -> None:
    # Inside-subtree file.
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "heap.dart").write_text(
        "/// Heap.\nclass Heap {}\n", encoding="utf-8"
    )

    # Outside-subtree file in a sibling subtree, importing inside.
    outside = discover_repo / "glp_runtime"
    outside.mkdir()
    (outside / "legacy.dart").write_text(
        "import '../glp_runtime_net/lib/heap.dart';\n"
        "class Legacy {}\n",
        encoding="utf-8",
    )

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    warnings_list = summary.get("warnings", [])
    outside_warnings = [
        w for w in warnings_list if w.get("kind") == "outside_caller"
    ]
    assert outside_warnings, (
        "expected at least one 'outside_caller' warning naming the "
        "outside file and the inside file; got warnings = %r" % (warnings_list,)
    )
    # The warning identifies both files.
    w0 = outside_warnings[0]
    assert "legacy.dart" in (w0.get("outside_file") or "")
    assert "heap.dart" in (w0.get("inside_file") or "")

    # Caller edge MUST NOT have been recorded — verifiable via the
    # inside file's tombstone callers list.
    import yaml

    tomb = (
        discover_repo / ".codeconv" / "tombstones" / "lib" / "heap.dart.md"
    )
    text = tomb.read_text(encoding="utf-8")
    parts = text.split("---", 2)
    fm = yaml.safe_load(parts[1])
    callers = fm.get("callers") or []
    for c in callers:
        assert "legacy" not in c, (
            f"outside file 'legacy.dart' must NOT appear as a caller edge; "
            f"got {callers!r}"
        )
