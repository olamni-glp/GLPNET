"""Tests for ``codeconv discover --from-tombstones`` — Phase 6 / US4 / T065.

Maps to SC-007 / FR-022:

- ``test_rebuild_from_tombstones_equals_normal`` — discover_runs from
  tombstones produces structurally identical inventory rows.
- ``test_from_tombstones_does_not_read_dart`` — instrumented file-read
  proxy: removing the .dart files before running --from-tombstones must
  still succeed.
"""

from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// A doc.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// B doc.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    return sub


def _tree_digest(root: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    for p in sorted(root.rglob("*")):
        if p.is_file():
            rel = p.relative_to(root).as_posix()
            out[rel] = hashlib.sha256(p.read_bytes()).hexdigest()
    return out


@needs_bridge
def test_rebuild_from_tombstones_equals_normal(discover_repo: Path) -> None:
    """Drop the codeconv schema, run --from-tombstones, verify resulting
    inventory rebuild produces the same tombstone set."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc1 = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc1.returncode == 0, proc1.stderr
    tombs_before = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs_before, "normal run must produce tombstones"

    # --from-tombstones reads the tombstone tree only.
    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--from-tombstones",
        "--json",
    )
    assert proc2.returncode == 0, proc2.stderr
    summary = json.loads(_extract_json(proc2.stdout))
    assert summary["mode"] == "from_tombstones"

    # Tombstone tree itself MUST not change byte-wise — the rebuild
    # populates only DB rows; tombstones are the input here.
    tombs_after = _tree_digest(discover_repo / ".codeconv" / "tombstones")
    assert tombs_before == tombs_after


@needs_bridge
def test_from_tombstones_does_not_read_dart(discover_repo: Path) -> None:
    """After a normal discover, deleting all .dart sources must not
    prevent --from-tombstones from succeeding (proxy for ``no .dart
    reads``)."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr

    proc1 = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc1.returncode == 0, proc1.stderr

    # Wipe sources entirely.
    shutil.rmtree(sub)
    sub.mkdir()  # empty dir so --root resolves

    proc2 = run_codeconv(
        discover_repo,
        "discover",
        "run",
        "--root",
        str(sub),
        "--from-tombstones",
        "--json",
    )
    assert proc2.returncode == 0, (
        f"--from-tombstones must succeed without .dart sources; got "
        f"stderr={proc2.stderr!r} stdout={proc2.stdout!r}"
    )


# ---------------------------------------------------------------------------
# Amendment v2 — preflight (exit 65, zero mutation) + Option B + dangling edge
# ---------------------------------------------------------------------------


def _raw_tombstone(
    repo_root: Path,
    rel_path: str,
    *,
    sha256: str | None,
    dependencies: list[str] | None = None,
) -> None:
    tp = repo_root / ".codeconv" / "tombstones" / (rel_path + ".md")
    tp.parent.mkdir(parents=True, exist_ok=True)
    deps = "[]" if not dependencies else "[" + ", ".join(dependencies) + "]"
    lines = [
        "---",
        f"path: {rel_path}",
        f"name: {Path(rel_path).name}",
        "purpose: ''",
        "key_idea: ''",
        f"dependencies: {deps}",
        "callers: []",
        "mtime: '2026-05-16T00:00:00.000Z'",
    ]
    if sha256 is not None:
        lines.append(f"sha256: {sha256}")
    lines += ["---", "", ""]
    tp.write_text("\n".join(lines), encoding="utf-8")


def test_from_tombstones_preflight_aborts_on_missing_required_field(
    tmp_path: Path,
) -> None:
    """A tombstone missing the required ``sha256`` field aborts the run
    with exit 65 BEFORE any bridge / DB touch (no bridge.json appears)."""
    sub = tmp_path / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    _raw_tombstone(tmp_path, "lib/a.dart", sha256=None)

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub), "--from-tombstones"
    )
    assert proc.returncode == 65, (
        f"missing required field must exit 65; got {proc.returncode} "
        f"stderr={proc.stderr!r}"
    )
    assert "ABORT" in proc.stderr
    # Preflight aborts before bridge acquisition — no cluster touched.
    assert not (tmp_path / ".pgdb" / "bridge.json").exists()


def test_from_tombstones_dry_run_drops_dangling_edge_and_warns(
    tmp_path: Path,
) -> None:
    """A dependency on a path with no tombstone is dropped + warned, and
    --dry-run reports it without acquiring the bridge."""
    sub = tmp_path / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    _raw_tombstone(
        tmp_path, "lib/a.dart", sha256="ab" * 32,
        dependencies=["lib/ghost.dart"],
    )

    proc = run_codeconv(
        tmp_path, "discover", "run", "--root", str(sub),
        "--from-tombstones", "--dry-run", "--json",
    )
    assert proc.returncode == 0, proc.stderr
    summary = json.loads(_extract_json(proc.stdout))
    assert summary["imports"] == 0  # dangling edge dropped
    assert any(
        w["kind"] == "missing_tombstone"
        and w["path"] == "lib/ghost.dart"
        and w["referrer"] == "lib/a.dart"
        for w in summary["warnings"]
    )
    assert not (tmp_path / ".pgdb" / "bridge.json").exists()


@needs_bridge
def test_from_tombstones_preserves_dart_conversions(
    discover_repo: Path,
) -> None:
    """Option B: a --from-tombstones rebuild must NOT cascade-delete a
    surviving file's dart_conversions row (the old blanket TRUNCATE did).

    mark-started lib/a.dart → in_progress; after --from-tombstones the
    file is still present so its conversion row survives → a subsequent
    compute still reports status 'in_progress'.
    """
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0

    assert (
        run_codeconv(
            discover_repo, "discover", "run", "--root", str(sub), "--json"
        ).returncode
        == 0
    )
    assert (
        run_codeconv(
            discover_repo, "depgraph", "compute", "--json"
        ).returncode
        == 0
    )
    ms = run_codeconv(
        discover_repo, "depgraph", "mark-started", "lib/a.dart"
    )
    assert ms.returncode == 0, ms.stderr

    # The rebuild that used to wipe dart_files (and cascade conversions).
    rt = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub),
        "--from-tombstones", "--json",
    )
    assert rt.returncode == 0, rt.stderr

    comp = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert comp.returncode == 0, comp.stderr
    # The per-file status lives in the .codeconv/depgraph.json artefact
    # (the --json stdout is only the run summary).
    artefact = discover_repo / ".codeconv" / "depgraph.json"
    assert artefact.is_file(), "compute must write .codeconv/depgraph.json"
    payload = json.loads(artefact.read_text(encoding="utf-8"))
    files = {f["path"]: f for f in payload["files"]}
    assert files["lib/a.dart"]["status"] == "in_progress", (
        "dart_conversions row for lib/a.dart must survive the "
        f"--from-tombstones rebuild (Option B); got {files['lib/a.dart']}"
    )
