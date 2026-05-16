"""Normal-mode referential completeness — Amendment v3 / option A.

Maps to `specs/012-codeconv-runner/contracts/codeconv_discover_cli.md`
§ Steps (normal mode) step 5 (REFERENTIAL COMPLETENESS) and feature-015
`contracts/depgraph_algorithm.md` § Algorithm step 2 / test obligation 8.

An in-subtree ``import`` directive that resolves (by path shape, R12) to a
file that does not exist on disk is never inventoried into ``dart_files``;
the resulting dangling ``dart_imports`` edge MUST be dropped and counted as
a ``missing_target`` warning, while a sibling valid in-subtree import on the
same file is still recorded. Regression for the live-inventory crash
``ValueError: edge endpoint not in nodes`` in ``codeconv depgraph compute``.
"""

from __future__ import annotations

import json
from pathlib import Path

import yaml

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


@needs_bridge
def test_dangling_in_subtree_edge_dropped_and_warned(
    discover_repo: Path,
) -> None:
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    # a.dart imports a valid sibling AND a non-existent in-subtree path.
    (sub / "lib" / "a.dart").write_text(
        "/// A.\n"
        "import 'b.dart';\n"
        "import 'missing/gone.dart';\n"
        "class A {}\n",
        encoding="utf-8",
    )
    (sub / "lib" / "b.dart").write_text(
        "/// B.\nclass B {}\n", encoding="utf-8"
    )

    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    # Dropped dangling edges are an accepted, warned divergence → exit 0.
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))

    mt = [w for w in summary.get("warnings", []) if w.get("kind") == "missing_target"]
    assert mt, (
        "expected a 'missing_target' warning for the dangling edge; "
        f"got warnings = {summary.get('warnings')!r}"
    )
    w0 = mt[0]
    assert "gone.dart" in (w0.get("path") or "")
    assert "a.dart" in (w0.get("referrer") or "")

    # The dangling edge MUST NOT be in dart_imports; verify via a.dart's
    # tombstone dependencies. The valid sibling edge MUST remain.
    tomb = discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    parts = tomb.read_text(encoding="utf-8").split("---", 2)
    deps = yaml.safe_load(parts[1]).get("dependencies") or []
    deps_joined = " ".join(deps)
    assert "gone.dart" not in deps_joined, (
        f"dangling edge to gone.dart must be dropped; deps = {deps!r}"
    )
    assert any("b.dart" in d for d in deps), (
        f"valid sibling import b.dart must still be recorded; deps = {deps!r}"
    )
