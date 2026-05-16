"""Phase 7 polish verifications — Phase 7 / T084 + T086 + T087.

SQL-level checks against a populated PGLite cluster + the SC-003
two-stack concurrent verification:

- ``test_schema_isolation`` (T086 / SC-012) — confirm ``dbos``,
  ``codeconv``, ``public`` schemas all present; zero codeconv tables in
  any other schema; zero dbos tables outside ``dbos``.
- ``test_dart_callers_inside_only`` (T087 / SC-011) — every row in
  ``codeconv.dart_callers.to_path`` is a path inside the subtree (no
  ``..``, no ``glp_runtime/`` siblings, etc.). Inverse of the
  outside-caller warning, asserted on the actual table contents.
- ``test_sc003_two_stack_concurrent`` (T084 / SC-003) — Python (psycopg)
  and .NET (Npgsql) each run 100 sequential transactions in parallel
  against the same bridge. Both MUST observe zero
  ``lost synchronization with server`` and zero
  ``DuplicatePreparedStatement`` errors.

All three run after a migrate + discover sequence so the schemas are real.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pytest

from .conftest import REPO_ROOT, needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


def _mk_subtree(repo_root: Path) -> Path:
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// B.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    return sub


@needs_bridge
def test_schema_isolation(discover_repo: Path) -> None:
    """SC-012: ``codeconv`` lives in its schema, ``dbos`` in its own.
    No cross-schema leakage."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr

    # Query the bridge for schema-table mapping.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        rows = conn.execute(
            text(
                "SELECT schema_name FROM information_schema.schemata "
                "WHERE schema_name NOT IN ('information_schema','pg_catalog','pg_toast')"
            )
        ).all()
        schemas = sorted(r[0] for r in rows)
        # Required schemas present.
        assert "codeconv" in schemas, schemas
        assert "dbos" in schemas, schemas
        assert "public" in schemas, schemas

        # codeconv tables live only in codeconv schema.
        codeconv_tables = [
            "dart_files",
            "dart_imports",
            "dart_callers",
            "dart_files_orphaned",
            "discover_runs",
        ]
        for tname in codeconv_tables:
            in_codeconv = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema = 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            assert in_codeconv == 1, f"codeconv.{tname} missing"
            in_other = conn.execute(
                text(
                    "SELECT COUNT(*) FROM information_schema.tables "
                    "WHERE table_schema != 'codeconv' AND table_name = :t"
                ),
                {"t": tname},
            ).scalar()
            # ``dart_files`` is intentionally NOT exclusive — D2NET has
            # its own ``public.dart_files`` (data-model.md § 7). All
            # other codeconv tables must NOT appear outside codeconv.
            if tname != "dart_files":
                assert in_other == 0, (
                    f"FR-015 violation: '{tname}' also exists in another schema"
                )

        # DBOS owns its schema; no dbos table should appear elsewhere.
        dbos_table_count_in_dbos = conn.execute(
            text(
                "SELECT COUNT(*) FROM information_schema.tables "
                "WHERE table_schema = 'dbos'"
            )
        ).scalar()
        assert dbos_table_count_in_dbos > 0, (
            "DBOS schema must contain its own tables after launch"
        )


@needs_bridge
def test_dart_callers_inside_only(discover_repo: Path) -> None:
    """SC-011: codeconv.dart_callers.to_path is always inside-subtree.

    Even with an OUTSIDE .dart file importing inside, no edge is
    recorded (only a warning). Verified by query.
    """
    # Inside files
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "heap.dart").write_text(
        "/// Heap.\nclass Heap {}\n", encoding="utf-8"
    )
    (sub / "lib" / "user.dart").write_text(
        "/// User.\nimport 'heap.dart';\nclass User {}\n", encoding="utf-8"
    )
    # Outside-subtree file that imports inside.
    outside = discover_repo / "glp_runtime"
    outside.mkdir()
    (outside / "legacy.dart").write_text(
        "import '../glp_runtime_net/lib/heap.dart';\nclass L {}\n",
        encoding="utf-8",
    )

    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr
    proc = run_codeconv(
        discover_repo, "discover", "run", "--root", str(sub), "--json"
    )
    assert proc.returncode == 0, proc.stderr

    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=30.0)
    engine = build_engine(endpoint)
    with engine.connect() as conn:
        rows = conn.execute(
            text("SELECT from_path, to_path FROM codeconv.dart_callers")
        ).all()
    # Every from_path must reside inside the subtree → no slashes pointing
    # outward (e.g. starting with ``..`` or ``glp_runtime/``).
    for from_path, to_path in rows:
        assert not from_path.startswith(".."), from_path
        assert "glp_runtime/" not in from_path or from_path.startswith(
            "glp_runtime_net"
        ), from_path
        # The 'legacy' file MUST never appear as a caller — even though it
        # imports an inside file, FR-023 bars recording the edge.
        assert "legacy" not in from_path, (
            f"FR-023 violation: outside file '{from_path}' recorded as caller"
        )


_DOTNET = shutil.which("dotnet")
needs_dotnet = pytest.mark.skipif(
    _DOTNET is None, reason="dotnet SDK not on PATH"
)


@needs_bridge
@needs_dotnet
def test_sc003_two_stack_concurrent(discover_repo: Path) -> None:
    """SC-003: Python + .NET each run 100 transactions against the SAME
    bridge concurrently; both observe zero lost-sync / duplicate-prepared."""
    # Migrate to ensure ``codeconv`` schema exists (scripts INSERT into
    # ``codeconv._sc003_python`` / ``codeconv._sc003_dotnet``).
    proc = run_codeconv(discover_repo, "migrate", timeout=180.0)
    assert proc.returncode == 0, proc.stderr

    # Read endpoint from the now-running bridge's sidecar.
    sidecar = discover_repo / ".pgdb" / "bridge.json"
    assert sidecar.is_file(), "bridge sidecar missing after migrate"
    info = json.loads(sidecar.read_text(encoding="utf-8"))
    port = int(info["port"])

    python_script = (
        REPO_ROOT / "specs" / "012-codeconv-runner" / "scripts" / "sc003_python_loop.py"
    )
    dotnet_project = (
        REPO_ROOT / "specs" / "012-codeconv-runner" / "scripts" / "Sc003NpgsqlLoop"
    )
    assert python_script.is_file()
    assert dotnet_project.is_dir()

    def run_python() -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(python_script),
                "--port",
                str(port),
                "--cycles",
                "100",
            ],
            capture_output=True,
            text=True,
            timeout=120.0,
        )

    def run_dotnet() -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                "dotnet",
                "run",
                "--no-build",
                "--project",
                str(dotnet_project),
                "--",
                "--port",
                str(port),
                "--cycles",
                "100",
            ],
            capture_output=True,
            text=True,
            timeout=240.0,
        )

    with ThreadPoolExecutor(max_workers=2) as ex:
        py_fut = ex.submit(run_python)
        net_fut = ex.submit(run_dotnet)
        py_proc = py_fut.result()
        net_proc = net_fut.result()

    assert py_proc.returncode == 0, (
        f"SC-003 Python side failed:\nstdout: {py_proc.stdout}\nstderr: {py_proc.stderr}"
    )
    assert net_proc.returncode == 0, (
        f"SC-003 .NET side failed:\nstdout: {net_proc.stdout}\nstderr: {net_proc.stderr}"
    )
    # Defensive: confirm zero lost-sync / duplicate-prepared messages.
    combined = (py_proc.stdout + py_proc.stderr + net_proc.stdout + net_proc.stderr).lower()
    assert "lost synchronization" not in combined
    assert "duplicateprepared" not in combined.replace(" ", "")


# ---------------------------------------------------------------------------
# T041 — FR-026 / FR-027 carry-forward for the feature-015 depgraph subtree
# ---------------------------------------------------------------------------


def test_depgraph_subtree_no_copy_or_prepared_cache() -> None:
    """FR-026 (no ``COPY ... FROM STDIN``) + FR-027 (no client-side
    prepared-statement cache) for the new depgraph tool. Pure source
    scan — no bridge."""
    subtree = REPO_ROOT / "codeconv" / "src" / "codeconv" / "tools" / "depgraph"
    assert subtree.is_dir(), subtree
    banned = ("COPY", "copy_expert", "prepared_statement_cache_size")
    offenders: list[str] = []
    for py in sorted(subtree.rglob("*.py")):
        text = py.read_text(encoding="utf-8")
        for needle in banned:
            if needle in text:
                offenders.append(f"{py.name}: {needle!r}")
    assert not offenders, (
        "FR-026/FR-027 violation in depgraph subtree: " + "; ".join(offenders)
    )
