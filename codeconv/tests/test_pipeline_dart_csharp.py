"""Full Dart→C# pipeline regression — Feature 016 / US5.

Maps to ``specs/016-codeconv-init-scaffold-langpair/spec.md`` US5 +
FR-026 + SC-004/SC-005/SC-007. Runs the four stages in order on a
synthetic subtree and asserts the seams between the ported tools and
the existing codeconv tools:

  init → (delegated) discover → depgraph compute → scaffold

Asserts cross-stage consistency:
  (a) ``codeconv.workspace_settings`` records the dart→csharp pair,
  (b) ``codeconv.dart_files`` is populated (delegated discover ran),
  (c) every scaffolded tombstone's ``target_path`` == the produced
      ``.cs`` path (SC-007 — the conversion-tracking surface),
  (d) ``codeconv.phase_status`` reflects scaffold COMPLETE,
  (e) ``depgraph compute`` remains consistent after scaffold (no 015
      regression — SC-005).

``@needs_bridge``; kept small per the watchdog discipline.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    """A→B linear chain + a tool subtree that must be excluded."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib" / "runtime").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// File A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "runtime" / "b.dart").write_text(
        "/// File B.\nimport '../a.dart';\nclass B {}\n", encoding="utf-8"
    )
    (sub / ".dart_tool").mkdir()
    (sub / ".dart_tool" / "junk.dart").write_text(
        "/// junk\nclass J {}\n", encoding="utf-8"
    )
    return sub


@needs_bridge
def test_full_pipeline_init_depgraph_scaffold(discover_repo: Path) -> None:
    sub = _mk_subtree(discover_repo)

    # 0. migrate (applies 0001+0002+0003).
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr

    # 1. init (delegates the inventory to discover).
    proc = run_codeconv(
        discover_repo,
        "init",
        "run",
        "--source",
        "glp_runtime_net",
        "--target",
        "out/csharp",
        "--source-lang",
        "dart",
        "--target-lang",
        "csharp",
        "--accept-suggested-exclusions",
        "--non-interactive",
        "--json",
    )
    assert proc.returncode == 0, f"init failed: {proc.stdout}\n{proc.stderr}"

    # 2. depgraph compute (feature 015 — must interoperate).
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert proc.returncode == 0, f"depgraph failed: {proc.stderr}"

    # 3. scaffold (mirrors source→out/csharp/*.cs + __<base>/).
    proc = run_codeconv(discover_repo, "scaffold", "run", "--json")
    assert proc.returncode == 0, f"scaffold failed: {proc.stdout}\n{proc.stderr}"

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from codeconv.tools.discover.tombstone import read_tombstone
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=60.0)
    engine = build_engine(endpoint)

    with engine.connect() as c:
        # (a) workspace_settings records the pair.
        ws = {
            k: v
            for k, v in c.execute(
                text("SELECT key, value FROM codeconv.workspace_settings")
            ).all()
        }
        assert ws.get("source_lang") == "dart", ws
        assert ws.get("target_lang") == "csharp", ws

        # (b) dart_files populated by the delegated discover; the
        #     excluded tool subtree is NOT inventoried.
        files = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
        assert "lib/a.dart" in files, files
        assert "lib/runtime/b.dart" in files, files
        assert not any("dart_tool" in f for f in files), files

        # (d) phase_status reflects scaffold COMPLETE.
        status = c.execute(
            text(
                "SELECT status FROM codeconv.phase_status "
                "WHERE phase = 'scaffold'"
            )
        ).scalar()
        assert status == "COMPLETE", status

        # (e) depgraph stayed consistent: one dart_depgraph row per
        #     inventoried file.
        depg = c.execute(
            text("SELECT COUNT(*) FROM codeconv.dart_depgraph")
        ).scalar()
        assert int(depg or 0) == len(files), (depg, files)

    # (c) every scaffolded file's tombstone target_path == produced .cs.
    tomb_root = discover_repo / ".codeconv" / "tombstones"
    t_a = read_tombstone(tomb_root / "lib" / "a.dart.md")
    assert t_a.get("target_path") == "lib/a.cs", t_a
    t_b = read_tombstone(tomb_root / "lib" / "runtime" / "b.dart.md")
    assert t_b.get("target_path") == "lib/runtime/b.cs", t_b

    # Produced target tree mirrors source with .cs + __<base>/ workdir.
    target = discover_repo / "out" / "csharp"
    assert (target / "lib" / "a.cs").is_file()
    assert (target / "lib" / "runtime" / "b.cs").is_file()
    assert (target / "lib" / "__a").is_dir()
    assert (target / "lib" / "runtime" / "__b").is_dir()


@needs_bridge
def test_pipeline_idempotent_rerun(discover_repo: Path) -> None:
    """SC-002: re-running init + scaffold on an unchanged workspace is a
    no-op (idempotent) — exit 0, identical target tree."""
    import hashlib

    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate", timeout=180.0).returncode == 0
    init_args = (
        "init", "run",
        "--source", "glp_runtime_net",
        "--target", "out/csharp",
        "--source-lang", "dart",
        "--target-lang", "csharp",
        "--accept-suggested-exclusions",
        "--non-interactive",
        "--json",
    )
    assert run_codeconv(discover_repo, *init_args).returncode == 0
    assert run_codeconv(discover_repo, "scaffold", "run", "--json").returncode == 0

    def _tree(root: Path) -> dict[str, str]:
        out: dict[str, str] = {}
        for p in sorted(root.rglob("*")):
            if p.is_file():
                out[p.relative_to(root).as_posix()] = hashlib.sha256(
                    p.read_bytes()
                ).hexdigest()
        return out

    target = discover_repo / "out" / "csharp"
    before = _tree(target)

    # Re-run both — idempotent.
    proc = run_codeconv(discover_repo, *init_args)
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary.get("already_initialized") is True, summary
    assert run_codeconv(discover_repo, "scaffold", "run", "--json").returncode == 0

    assert _tree(target) == before
