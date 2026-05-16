"""Integration tests for ``codeconv init`` — Feature 016 / US1 + US4.

Maps to ``specs/016-codeconv-init-scaffold-langpair/contracts/codeconv_init_cli.md``
(spec FR-006..FR-012, FR-011) and the US1 / US4 independent tests.

These are ``@needs_bridge`` integration tests: they spawn the unified
bridge, run ``codeconv migrate`` then ``codeconv init`` via the CLI
subprocess harness, and assert against the emitted summary + the
``codeconv``-schema workspace tables + the delegated discover inventory.

Per the watchdog discipline these are kept small; the suite as a whole
is reconciled by the orchestrator (PGLite cold-init ~7s on this exFAT
checkout — the ``discover_repo`` fixture uses ``tmp_path``).
"""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


# ---------------------------------------------------------------------------
# Fixtures / helpers
# ---------------------------------------------------------------------------


def _mk_subtree(repo_root: Path) -> Path:
    """Synthetic ``glp_runtime_net/`` with a tool dir + an in-scope file."""
    sub = repo_root / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "a.dart").write_text(
        "/// File A.\nclass A {}\n", encoding="utf-8"
    )
    (sub / "lib" / "b.dart").write_text(
        "/// File B.\nimport 'a.dart';\nclass B {}\n", encoding="utf-8"
    )
    # A tool subtree the dart_csharp pair's tool_exclusion_globs() prunes.
    (sub / ".dart_tool").mkdir()
    (sub / ".dart_tool" / "junk.dart").write_text(
        "/// junk\nclass J {}\n", encoding="utf-8"
    )
    return sub


def _engine(repo_root: Path):
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine

    endpoint = acquire_or_discover(repo_root, ready_timeout=60.0)
    return build_engine(endpoint)


def _settings(repo_root: Path) -> dict[str, str]:
    from sqlalchemy import text

    eng = _engine(repo_root)
    with eng.connect() as c:
        rows = c.execute(
            text("SELECT key, value FROM codeconv.workspace_settings")
        ).all()
    return {k: v for k, v in rows}


def _table_count(repo_root: Path, table: str) -> int:
    from sqlalchemy import text

    eng = _engine(repo_root)
    with eng.connect() as c:
        return int(
            c.execute(
                text(f"SELECT COUNT(*) FROM codeconv.{table}")
            ).scalar()
            or 0
        )


def _run_init(repo_root: Path, sub: Path, *extra: str):
    return run_codeconv(
        repo_root,
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
        *extra,
    )


# ---------------------------------------------------------------------------
# US1 — initialise a conversion workspace
# ---------------------------------------------------------------------------


@needs_bridge
def test_init_writes_workspace_settings_and_pair(discover_repo: Path) -> None:
    """FR-006: init records (source_lang,target_lang,source_path,target_path)
    into ``codeconv.workspace_settings`` resolving to the dart→csharp pair."""
    sub = _mk_subtree(discover_repo)
    proc = run_codeconv(discover_repo, "migrate")
    assert proc.returncode == 0, proc.stderr
    proc = _run_init(discover_repo, sub)
    assert proc.returncode == 0, f"init failed: {proc.stderr}\n{proc.stdout}"

    s = _settings(discover_repo)
    assert s.get("source_lang") == "dart", s
    assert s.get("target_lang") == "csharp", s
    assert s.get("source_path") == "glp_runtime_net", s
    assert s.get("target_path") == "out/csharp", s


@needs_bridge
def test_init_seeds_exclusions_and_phase_tables(discover_repo: Path) -> None:
    """FR-007/FR-008: init seeds ``excluded_directories`` from the pair's
    tool_exclusion_globs() and seeds ``phase_sequence``/``phase_status``."""
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = _run_init(discover_repo, sub)
    assert proc.returncode == 0, proc.stderr

    assert _table_count(discover_repo, "excluded_directories") >= 1
    assert _table_count(discover_repo, "phase_sequence") >= 1
    assert _table_count(discover_repo, "phase_status") >= 1

    from sqlalchemy import text

    eng = _engine(discover_repo)
    with eng.connect() as c:
        kinds = {
            r[0]
            for r in c.execute(
                text("SELECT DISTINCT kind FROM codeconv.excluded_directories")
            ).all()
        }
    # The dart_csharp tool globs are recorded with kind='tool'.
    assert "tool" in kinds, kinds


@needs_bridge
def test_init_delegates_inventory_to_discover(discover_repo: Path) -> None:
    """FR-009/D3: init delegates the inventory to discover —
    ``codeconv.dart_files`` is populated with the in-scope files (the
    excluded ``.dart_tool/junk.dart`` is NOT inventoried)."""
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = _run_init(discover_repo, sub)
    assert proc.returncode == 0, proc.stderr

    from sqlalchemy import text

    eng = _engine(discover_repo)
    with eng.connect() as c:
        paths = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
    assert "lib/a.dart" in paths, paths
    assert "lib/b.dart" in paths, paths
    # Excluded tool subtree must not be inventoried.
    assert not any("dart_tool" in p for p in paths), paths


@needs_bridge
def test_init_idempotent_already_initialized(discover_repo: Path) -> None:
    """FR-010/SC-002: a second init with unchanged inputs is idempotent —
    zero workspace-state change, reports already-initialized, exit 0."""
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _run_init(discover_repo, sub).returncode == 0
    s1 = _settings(discover_repo)
    excl1 = _table_count(discover_repo, "excluded_directories")

    proc2 = _run_init(discover_repo, sub)
    assert proc2.returncode == 0, proc2.stderr
    summary = json.loads(_extract_json(proc2.stdout))
    assert summary.get("already_initialized") is True, summary
    assert _settings(discover_repo) == s1
    assert _table_count(discover_repo, "excluded_directories") == excl1


@needs_bridge
def test_init_rejects_unregistered_pair_exit5_no_state(
    discover_repo: Path,
) -> None:
    """FR-005: an unregistered pair → exit 5, names registered pairs,
    writes NO workspace state."""
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = run_codeconv(
        discover_repo,
        "init",
        "run",
        "--source",
        "glp_runtime_net",
        "--target",
        "out/rust",
        "--source-lang",
        "dart",
        "--target-lang",
        "rust",
        "--accept-suggested-exclusions",
        "--non-interactive",
    )
    assert proc.returncode == 5, (proc.returncode, proc.stdout, proc.stderr)
    out = (proc.stdout + proc.stderr).lower()
    assert "dart" in out and "csharp" in out, out
    # No workspace state written.
    assert _table_count(discover_repo, "workspace_settings") == 0


@needs_bridge
def test_init_rejects_invalid_source_path_exit2_no_state(
    discover_repo: Path,
) -> None:
    """FR-012: a source path outside the repo / non-existent → exit 2,
    no partial workspace state."""
    _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = run_codeconv(
        discover_repo,
        "init",
        "run",
        "--source",
        "../outside",
        "--target",
        "out/csharp",
        "--source-lang",
        "dart",
        "--target-lang",
        "csharp",
        "--accept-suggested-exclusions",
        "--non-interactive",
    )
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert _table_count(discover_repo, "workspace_settings") == 0


@needs_bridge
def test_init_rebuild_requires_confirmation(discover_repo: Path) -> None:
    """FR-010: a destructive ``--rebuild`` without explicit confirmation
    is refused (exit 2, no state discarded); ``--confirm-rebuild`` (the
    skill-driven confirmation token) lets it proceed."""
    sub = _mk_subtree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _run_init(discover_repo, sub).returncode == 0
    s1 = _settings(discover_repo)
    assert s1.get("source_lang") == "dart"

    # --rebuild without the confirmation token: refused, state intact.
    proc = _run_init(discover_repo, sub, "--rebuild")
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert _settings(discover_repo) == s1

    # --rebuild --confirm-rebuild: proceeds (idempotent same inputs → 0).
    proc = _run_init(discover_repo, sub, "--rebuild", "--confirm-rebuild")
    assert proc.returncode == 0, proc.stderr
    assert _settings(discover_repo).get("source_lang") == "dart"


# ---------------------------------------------------------------------------
# US4 — exclusion management on an existing workspace (T027)
# ---------------------------------------------------------------------------


@needs_bridge
def test_add_exclude_drops_files_and_persists(discover_repo: Path) -> None:
    """FR-011: ``init add-exclude <dir>`` removes files under it from the
    in-scope inventory (discover re-synced) and persists the exclusion."""
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "keep.dart").write_text(
        "/// keep\nclass K {}\n", encoding="utf-8"
    )
    (sub / "lib" / "generated").mkdir()
    (sub / "lib" / "generated" / "gen.dart").write_text(
        "/// gen\nclass G {}\n", encoding="utf-8"
    )
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _run_init(discover_repo, sub).returncode == 0

    from sqlalchemy import text

    eng = _engine(discover_repo)
    with eng.connect() as c:
        before = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
    assert "lib/generated/gen.dart" in before, before

    proc = run_codeconv(
        discover_repo,
        "init",
        "add-exclude",
        "lib/generated",
        "--json",
    )
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"

    with eng.connect() as c:
        after = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
        excl = {
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.excluded_directories")
            ).all()
        }
    assert "lib/generated/gen.dart" not in after, after
    assert "lib/keep.dart" in after, after
    assert "lib/generated" in excl, excl


@needs_bridge
def test_remove_exclude_restores_files(discover_repo: Path) -> None:
    """FR-011: ``init remove-exclude <dir>`` returns previously-excluded
    files to the in-scope inventory."""
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib" / "generated").mkdir(parents=True)
    (sub / "lib" / "keep.dart").write_text(
        "/// keep\nclass K {}\n", encoding="utf-8"
    )
    (sub / "lib" / "generated" / "gen.dart").write_text(
        "/// gen\nclass G {}\n", encoding="utf-8"
    )
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _run_init(discover_repo, sub).returncode == 0
    assert (
        run_codeconv(
            discover_repo, "init", "add-exclude", "lib/generated"
        ).returncode
        == 0
    )

    proc = run_codeconv(
        discover_repo, "init", "remove-exclude", "lib/generated", "--json"
    )
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"

    from sqlalchemy import text

    eng = _engine(discover_repo)
    with eng.connect() as c:
        after = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
        excl = {
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.excluded_directories")
            ).all()
        }
    assert "lib/generated/gen.dart" in after, after
    assert "lib/generated" not in excl, excl


@needs_bridge
def test_exclude_kind_recorded_manual(discover_repo: Path) -> None:
    """FR-011 / data-model §1.2: a user-added exclusion is recorded with
    kind='manual' (distinct from the pair's kind='tool' recommendations)."""
    sub = discover_repo / "glp_runtime_net"
    (sub / "lib").mkdir(parents=True)
    (sub / "lib" / "keep.dart").write_text(
        "/// keep\nclass K {}\n", encoding="utf-8"
    )
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _run_init(discover_repo, sub).returncode == 0
    assert (
        run_codeconv(
            discover_repo, "init", "add-exclude", "lib/keep_dir"
        ).returncode
        == 0
    )

    from sqlalchemy import text

    eng = _engine(discover_repo)
    with eng.connect() as c:
        kind = c.execute(
            text(
                "SELECT kind FROM codeconv.excluded_directories "
                "WHERE path = :p"
            ),
            {"p": "lib/keep_dir"},
        ).scalar()
    assert kind == "manual", kind
