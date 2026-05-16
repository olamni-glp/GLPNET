"""End-to-end tests for ``codeconv depgraph compute``.

Maps to ``specs/015-codeconv-depgraph/contracts/depgraph_cli.md`` §
``compute`` behaviour. Gated by ``@needs_bridge``.
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_chain_subtree(repo_root: Path) -> Path:
    """A→B→C linear chain (3 files, 2 edges)."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// File A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// File B.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    (sub / "lib" / "c.dart").write_text(
        "/// File C.\nimport 'b.dart';\nclass C {}\n", encoding="utf-8"
    )
    return sub


def _migrate_and_discover(repo_root: Path, sub: Path) -> None:
    proc = run_codeconv(repo_root, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        repo_root, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr


@needs_bridge
def test_compute_writes_jsonfile_and_table(discover_repo: Path) -> None:
    """A successful compute writes ``.codeconv/depgraph.json`` and
    populates ``codeconv.dart_depgraph``."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert proc.returncode == 0, proc.stderr

    json_path = discover_repo / ".codeconv" / "depgraph.json"
    assert json_path.is_file()
    payload = json.loads(json_path.read_text(encoding="utf-8"))
    assert payload["schema_version"] == 1
    assert payload["metadata"]["inventory_files_total"] == 3
    assert payload["metadata"]["inventory_edges_total"] == 2
    # All three files singleton SCCs; cycle_count == 0.
    assert payload["metadata"]["cycle_count"] == 0
    # A is at the bottom of the chain (no deps), so ready.
    assert "lib/a.dart" in payload["ready"]

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT path, topo_level, ready, status "
                "FROM codeconv.dart_depgraph ORDER BY path"
            )
        ).all()
    paths = [r[0] for r in rows]
    assert paths == ["lib/a.dart", "lib/b.dart", "lib/c.dart"]
    by_path = {r[0]: r for r in rows}
    assert by_path["lib/a.dart"][1] == 0
    assert by_path["lib/a.dart"][2] is True
    assert by_path["lib/a.dart"][3] == "ready"
    assert by_path["lib/c.dart"][1] == 2  # depends transitively on a + b


@needs_bridge
def test_empty_inventory_exits_2(discover_repo: Path) -> None:
    """No files in dart_files ⇒ exit 2 with actionable error."""
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert proc.returncode != 0, proc.stdout + proc.stderr
    combined = (proc.stdout + proc.stderr).lower()
    assert "discover" in combined, combined


@needs_bridge
def test_ready_set_matches_sql_query(discover_repo: Path) -> None:
    """The JSON ``ready`` array equals the SQL-derived ready set."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert proc.returncode == 0, proc.stderr

    json_path = discover_repo / ".codeconv" / "depgraph.json"
    payload = json.loads(json_path.read_text(encoding="utf-8"))
    json_ready = sorted(payload["ready"])

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        sql_ready = sorted(
            r[0]
            for r in conn.execute(
                text(
                    "SELECT path FROM codeconv.dart_depgraph WHERE ready"
                )
            ).all()
        )
    assert json_ready == sql_ready


@needs_bridge
def test_topo_invariant_holds_for_every_edge(discover_repo: Path) -> None:
    """For every (A→B) in dart_imports, topo_level(A) > topo_level(B)
    OR cycle_group_id(A) == cycle_group_id(B)."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert proc.returncode == 0, proc.stderr

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        bad = conn.execute(
            text(
                "SELECT COUNT(*) FROM codeconv.dart_imports i "
                "JOIN codeconv.dart_depgraph a ON a.path = i.from_path "
                "JOIN codeconv.dart_depgraph b ON b.path = i.to_path "
                "WHERE NOT (a.topo_level > b.topo_level "
                "           OR a.cycle_group_id = b.cycle_group_id)"
            )
        ).scalar()
    assert bad == 0, f"SC-003 violation: {bad} edges break invariant"


@needs_bridge
def test_compute_respects_json_out_flag(discover_repo: Path) -> None:
    """``--json-out`` redirects the artefact to a custom path."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    custom = discover_repo / "subdir" / "custom.json"
    proc = run_codeconv(
        discover_repo,
        "depgraph",
        "compute",
        "--json-out",
        str(custom),
    )
    assert proc.returncode == 0, proc.stderr
    assert custom.is_file()
    default = discover_repo / ".codeconv" / "depgraph.json"
    assert not default.is_file(), "default path written despite --json-out"


@needs_bridge
def test_compute_quiet_suppresses_per_file_logging(discover_repo: Path) -> None:
    """``--quiet`` produces (near-)empty stdout — no per-file lines."""
    sub = _mk_chain_subtree(discover_repo)
    _migrate_and_discover(discover_repo, sub)
    proc = run_codeconv(discover_repo, "depgraph", "compute", "--quiet")
    assert proc.returncode == 0, proc.stderr
    # Quiet mode should NOT emit the "codeconv-depgraph" header.
    assert "codeconv-depgraph" not in proc.stdout, proc.stdout
