"""Normal-mode referential completeness — Amendment v3 / option A′.

Maps to `specs/012-codeconv-runner/contracts/codeconv_discover_cli.md`
§ Steps (normal mode) step 5 and
`specs/015-codeconv-depgraph/contracts/depgraph_cli.md` § compute step 4a
(+ `contracts/depgraph_algorithm.md` § Algorithm step 2 / test obligation 8).

An in-subtree ``import`` directive that resolves (by path shape, R12) to a
file that does not exist on disk is never inventoried into ``dart_files``.
The resulting dangling ``dart_imports`` edge is NON-DESTRUCTIVELY handled:

- discover EMITS a ``missing_target`` warning but KEEPS the edge in
  ``dart_imports`` (faithful, persistent source record — a destructive delete
  would lose it permanently across idempotent runs);
- ``codeconv depgraph compute`` filters the dangling edge out before
  ``algorithm.compute`` (reported as ``dangling_edges_dropped``), so it
  succeeds instead of crashing with ``ValueError: edge endpoint not in
  nodes`` — and is self-healing once the target is inventoried.

Regression for the live-inventory crash in ``codeconv depgraph compute`` and
for the codex P2 (idempotent-skip edge loss from a destructive delete).
"""

from __future__ import annotations

import json
from pathlib import Path

import yaml

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


@needs_bridge
def test_dangling_edge_kept_in_imports_warned_and_filtered_at_compute(
    discover_repo: Path,
) -> None:
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
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
    # Dangling edge is a warned, accepted divergence → exit 0.
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    mt = [
        w for w in summary.get("warnings", [])
        if w.get("kind") == "missing_target"
    ]
    assert mt, f"expected a 'missing_target' warning; got {summary.get('warnings')!r}"
    assert "gone.dart" in (mt[0].get("path") or "")
    assert "a.dart" in (mt[0].get("referrer") or "")

    # A′: the dangling edge is KEPT in dart_imports (faithful record) —
    # a.dart's tombstone dependencies still list the missing target AND
    # the valid sibling.
    tomb = discover_repo / ".codeconv" / "tombstones" / "lib" / "a.dart.md"
    deps = yaml.safe_load(tomb.read_text(encoding="utf-8").split("---", 2)[1]).get(
        "dependencies"
    ) or []
    deps_joined = " ".join(deps)
    assert "gone.dart" in deps_joined, (
        f"A′: dangling edge MUST be retained in dart_imports/tombstone "
        f"(non-destructive); deps = {deps!r}"
    )
    assert any("b.dart" in d for d in deps), (
        f"valid sibling import must be recorded; deps = {deps!r}"
    )

    # compute MUST succeed despite the dangling edge, filtering it out.
    proc = run_codeconv(
        discover_repo, "depgraph", "compute", "--json"
    )
    assert proc.returncode == 0, (
        f"compute must not crash on a dangling edge; stderr={proc.stderr}"
    )
    csum = json.loads(_extract_json(proc.stdout))
    assert csum.get("dangling_edges_dropped", 0) >= 1, (
        f"compute should report >=1 dangling_edges_dropped; got {csum!r}"
    )

    dj = json.loads(
        (discover_repo / ".codeconv" / "depgraph.json").read_text(
            encoding="utf-8"
        )
    )
    node_paths = {f["path"] for f in dj["files"]}
    assert "lib/a.dart" in node_paths and "lib/b.dart" in node_paths
    assert not any(
        "gone.dart" in p for p in node_paths
    ), "the non-existent target must not appear as a depgraph node"
    assert dj["metadata"]["dangling_edges_dropped"] >= 1
