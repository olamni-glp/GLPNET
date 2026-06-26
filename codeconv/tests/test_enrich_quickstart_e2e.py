"""Feature 035 / Polish (T022) — quickstart.md end-to-end, FR-014 git-reviewability.

Runs the quickstart flow on an ISOLATED cluster (never the canonical
``.pgdb``): migrate → discover → enrich ``--dry-run`` (mutates nothing) →
scoped enrich → verify the canonical blank-doc example
(``lib/compiler/codegen.dart``) gains a non-blank ``purpose``, a distinct
``key_idea``, ``*_source: inferred``, ``sha256`` unchanged, with markdown ⇔
DB agreement; then proves the inferred fields show up in a real ``git diff``
of ``.codeconv/tombstones/`` (FR-014 — analyze E1).

A real-CORPUS enrichment (against the canonical cluster, driven by the
``/codeconv-enrich`` skill) is intentionally out of this test's scope: it
needs a canonical-cluster migration + the skill seam (out of plan code scope).
"""

from __future__ import annotations

import subprocess
from pathlib import Path

from sqlalchemy import text

from .conftest import BRIDGE_SCRIPT, fake_infer_fn, needs_bridge, run_codeconv
from codeconv.tools.discover.tombstone import read_tombstone, tombstone_path


def _mk_subtree(repo_root: Path) -> Path:
    """The canonical quickstart example: a blank-doc compiler/codegen file."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib" / "compiler").mkdir(parents=True)
    (sub / "lib" / "compiler" / "codegen.dart").write_text(
        "class Codegen {\n"
        "  List<int> emit(Ast ast) {\n"
        "    final out = <int>[];\n"
        "    for (final node in ast.walk()) { out.addAll(node.opcodes()); }\n"
        "    return out;\n"
        "  }\n"
        "}\n",
        encoding="utf-8",
    )
    return sub


def _git(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        capture_output=True, text=True, check=False,
    )


@needs_bridge
def test_quickstart_e2e_and_git_reviewable(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)
    rel = "lib/compiler/codegen.dart"
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    ).returncode == 0

    troot = discover_repo / ".codeconv" / "tombstones"
    tomb = tombstone_path(troot, rel)
    pre = read_tombstone(tomb)
    assert pre["purpose"] == "" and pre["key_idea"] == ""
    pre_sha = pre["sha256"]

    from codeconv.tools.enrich.workflow import run_enrich

    # --dry-run mutates nothing.
    pre_bytes = tomb.read_bytes()
    dry = run_enrich(
        discover_repo, infer_fn=fake_infer_fn, paths=["lib/compiler"],
        dry_run=True, bridge_script=BRIDGE_SCRIPT,
    )
    assert dry["candidates"] >= 1 and dry["run_log"] is None
    assert tomb.read_bytes() == pre_bytes, "--dry-run must not mutate tombstones"

    # Commit the post-discover state so a later git diff shows ONLY enrichment.
    assert _git(discover_repo, "init").returncode == 0
    _git(discover_repo, "config", "user.email", "t@t")
    _git(discover_repo, "config", "user.name", "t")
    assert _git(discover_repo, "add", ".codeconv/tombstones").returncode == 0
    assert _git(discover_repo, "commit", "-m", "baseline tombstones").returncode == 0

    # Real scoped enrich.
    summary = run_enrich(
        discover_repo, infer_fn=fake_infer_fn, paths=["lib/compiler"],
        bridge_script=BRIDGE_SCRIPT,
    )
    assert summary["enriched"] >= 1

    post = read_tombstone(tomb)
    assert post["purpose"].strip() != ""
    assert post["key_idea"] != post["purpose"]
    assert post["purpose_source"] == "inferred"
    assert post["key_idea_source"] == "inferred"
    assert post["sha256"] == pre_sha  # source untouched

    # DB agreement.
    from codeconv.db.engine import connect

    engine = connect(discover_repo)
    with engine.begin() as conn:
        row = conn.execute(
            text(
                "SELECT purpose, purpose_source, key_idea_source "
                "FROM codeconv.dart_files WHERE path = :p"
            ),
            {"p": rel},
        ).first()
    assert row is not None and row[0] == post["purpose"]
    assert row[1] == "inferred" and row[2] == "inferred"

    # FR-014: the inferred fields show up in a real git diff (reviewable).
    diff = _git(discover_repo, "diff", "--", ".codeconv/tombstones")
    assert diff.returncode == 0
    assert "purpose_source: inferred" in diff.stdout
    assert "key_idea_source: inferred" in diff.stdout
    assert post["purpose"].strip().splitlines()[0][:20] in diff.stdout
