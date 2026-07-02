"""Feature 035 / US2 (T013) — enrich is idempotent + change-aware.

SC-002: a no-source-change re-run performs ZERO ``infer_fn`` calls and the
tombstone set is byte-identical. Acceptance 2: a file whose source changed
(re-seeded blank by ``discover``) is re-inferred on the next enrich run.
"""

from __future__ import annotations

import hashlib
from pathlib import Path

from .conftest import BRIDGE_SCRIPT, fake_infer_fn, needs_bridge, run_codeconv
from codeconv.tools.discover.tombstone import read_tombstone, tombstone_path


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "blank.dart").write_text(
        "class Blank {\n  int v = 1;\n}\n", encoding="utf-8"
    )
    (sub / "lib" / "other.dart").write_text(
        "class Other {\n  int w = 2;\n}\n", encoding="utf-8"
    )
    return sub


def _tree_digest(root: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    for p in sorted(root.rglob("*")):
        if p.is_file():
            out[p.relative_to(root).as_posix()] = hashlib.sha256(
                p.read_bytes()
            ).hexdigest()
    return out


@needs_bridge
def test_idempotent_zero_infer_byte_identical(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    from codeconv.tools.enrich.workflow import run_enrich

    calls: list[str] = []

    def counting(req):
        calls.append(req.rel_path)
        return fake_infer_fn(req)

    troot = discover_repo / ".codeconv" / "tombstones"
    s1 = run_enrich(discover_repo, infer_fn=counting, bridge_script=BRIDGE_SCRIPT)
    assert s1["enriched"] == 2, s1
    n1 = len(calls)
    assert n1 == 2
    tombs1 = _tree_digest(troot)

    # 2nd run, no source change: zero inference, byte-identical tombstones.
    s2 = run_enrich(discover_repo, infer_fn=counting, bridge_script=BRIDGE_SCRIPT)
    assert len(calls) == n1, f"2nd run made {len(calls) - n1} infer calls (SC-002 violated)"
    assert s2["candidates"] == 0 and s2["enriched"] == 0, s2
    tombs2 = _tree_digest(troot)
    assert tombs1 == tombs2, "tombstone set not byte-identical across re-run (SC-002)"


@needs_bridge
def test_changed_source_is_reinferred(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    from codeconv.tools.enrich.workflow import run_enrich

    troot = discover_repo / ".codeconv" / "tombstones"
    run_enrich(discover_repo, infer_fn=fake_infer_fn, bridge_script=BRIDGE_SCRIPT)
    tomb = tombstone_path(troot, "lib/blank.dart")
    first = read_tombstone(tomb)
    assert first["purpose_source"] == "inferred"
    first_purpose = first["purpose"]

    # Change the source (more lines → the fake's purpose text changes), then
    # re-discover (re-seeds blank + resets provenance) → re-enrich.
    (sub / "lib" / "blank.dart").write_text(
        "class Blank {\n  int v = 1;\n  int u = 2;\n  int t = 3;\n}\n",
        encoding="utf-8",
    )
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0
    after_discover = read_tombstone(tomb)
    # FR-007: discover re-seeded the changed file blank + reset provenance.
    assert after_discover["purpose"] == ""
    assert after_discover["purpose_source"] == "absent"

    s = run_enrich(discover_repo, infer_fn=fake_infer_fn, bridge_script=BRIDGE_SCRIPT)
    assert s["enriched"] >= 1
    reinferred = read_tombstone(tomb)
    assert reinferred["purpose_source"] == "inferred"
    assert reinferred["purpose"] != first_purpose  # reflects the new source
