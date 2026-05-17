"""Integration + full-chain tests for ``codeconv mirror`` — Feature 016 /
spec Amendment 1 (US6, FR-040/FR-041).

``@needs_bridge``: migrate → init (defers inventory; source absent) →
mirror → (full chain) discover → depgraph → scaffold, via the CLI
subprocess harness. Asserts the spec-`001` acceptance set with the
FR-032 Option-1 deviation (source mirrored verbatim as ``.dart``),
deferred-inventory init, refuse-existing/idempotence, ``--refresh``
preservation, collision pre-flight abort, no-workspace refusal, and
cross-stage consistency.

Kept small per the watchdog discipline (PGLite cold-init ~7s;
``discover_repo`` uses ``tmp_path``).
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from .conftest import needs_bridge, run_codeconv
from .test_discover_idempotence import _extract_json


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _mk_source_tree(repo_root: Path) -> Path:
    """The mirror INPUT: a synthetic ``glp_runtime/`` source-lang tree."""
    src = repo_root / "glp_runtime"
    (src / "lib" / "runtime").mkdir(parents=True)
    (src / "pubspec.yaml").write_text("name: glpnet\n", encoding="utf-8")
    (src / "lib" / "a.dart").write_text(
        "/// A.\nclass A {}\n", encoding="utf-8"
    )
    (src / "lib" / "runtime" / "heap.dart").write_text(
        "/// Heap.\nimport '../a.dart';\nclass Heap {}\n", encoding="utf-8"
    )
    (src / ".dart_tool").mkdir()
    (src / ".dart_tool" / "junk.dart").write_text(
        "/// junk\nclass J {}\n", encoding="utf-8"
    )
    return src


def _init(repo_root: Path, *extra: str):
    return run_codeconv(
        repo_root,
        "init",
        "run",
        "--mirror-source",
        "glp_runtime",
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


def _mirror(repo_root: Path, *extra: str):
    return run_codeconv(repo_root, "mirror", "run", "--json", *extra)


def _tree(root: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not root.is_dir():
        return out
    for p in sorted(root.rglob("*")):
        if p.is_file():
            out[p.relative_to(root).as_posix()] = hashlib.sha256(
                p.read_bytes()
            ).hexdigest()
        elif p.is_dir():
            out[p.relative_to(root).as_posix() + "/"] = "<dir>"
    return out


_NINE = (".cs", ".ana", ".tst", ".con", ".dep", ".cgn", ".iss", ".sta", ".ver")


# ---------------------------------------------------------------------------
# FR-009/FR-012 amended — init defers the inventory when source absent
# ---------------------------------------------------------------------------


@needs_bridge
def test_init_defers_inventory_when_source_absent(
    discover_repo: Path,
) -> None:
    _mk_source_tree(discover_repo)  # glp_runtime present; net absent
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    proc = _init(discover_repo)
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"
    s = json.loads(_extract_json(proc.stdout))
    assert s.get("inventory_deferred") is True, s
    assert s.get("warning"), s
    assert s.get("inventory_files") == 0, s
    assert s.get("mirror_source_root") == "glp_runtime", s


# ---------------------------------------------------------------------------
# US6 / FR-030..FR-034 — mirror produces the spec-001 tree (Option 1)
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_produces_tree_companions_tracker(
    discover_repo: Path,
) -> None:
    _mk_source_tree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    proc = _mirror(discover_repo)
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"

    net = discover_repo / "glp_runtime_net"
    files = _tree(net)

    # FR-032 Option 1: source preserved VERBATIM as .dart (no .src).
    assert "lib/a.dart" in files
    assert "lib/runtime/heap.dart" in files
    src_a = (discover_repo / "glp_runtime" / "lib" / "a.dart").read_bytes()
    assert (net / "lib" / "a.dart").read_bytes() == src_a
    # FR-031: non-source byte-identical.
    assert (net / "pubspec.yaml").read_text() == "name: glpnet\n"
    # FR-030: pruned dir absent.
    assert not any("dart_tool" in k for k in files), files
    # FR-033: nine companions per source file, each a // TODO line.
    for ext in _NINE:
        comp = net / "lib" / f"a{ext}"
        assert comp.is_file(), ext
        assert comp.read_text().startswith("// TODO:"), ext
    # FR-034: root tracker, one record per source file, status todo.
    tracker = json.loads(
        (net / "d2net-tracker.json").read_text(encoding="utf-8")
    )
    assert isinstance(tracker, list)
    srcs = {r["source"] for r in tracker}
    assert srcs == {"lib/a.dart", "lib/runtime/heap.dart"}, srcs
    for rec in tracker:
        assert len(rec["companions"]) == 9
        assert all(c["status"] == "todo" for c in rec["companions"])


# ---------------------------------------------------------------------------
# FR-035 — refuse existing without --refresh; idempotent
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_refuses_existing_without_refresh_idempotent(
    discover_repo: Path,
) -> None:
    _mk_source_tree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    assert _mirror(discover_repo).returncode == 0

    net = discover_repo / "glp_runtime_net"
    before = _tree(net)
    proc = _mirror(discover_repo)
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert _tree(net) == before  # untouched
    assert not (
        discover_repo / "glp_runtime_net.codeconv-mirror-tmp"
    ).exists()


# ---------------------------------------------------------------------------
# FR-035 / spec-001 FR-011 — --refresh preserves companions + tracker
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_refresh_preserves_companions_and_tracker(
    discover_repo: Path,
) -> None:
    src = _mk_source_tree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0
    assert _mirror(discover_repo).returncode == 0

    net = discover_repo / "glp_runtime_net"
    # Hand-edit a companion (simulate in-progress port work).
    edited = net / "lib" / "a.cs"
    edited.write_text("// real C# port\nclass A {}\n", encoding="utf-8")
    tracker_bytes = (net / "d2net-tracker.json").read_bytes()
    # A newly-added source file appears.
    (src / "lib" / "new.dart").write_text(
        "/// New.\nclass N {}\n", encoding="utf-8"
    )

    proc = _mirror(discover_repo, "--refresh")
    assert proc.returncode == 0, f"{proc.stdout}\n{proc.stderr}"

    # Existing hand-edited companion preserved byte-identical (FR-011 d).
    assert edited.read_text() == "// real C# port\nclass A {}\n"
    # Tracker byte-identical (FR-011 f).
    assert (net / "d2net-tracker.json").read_bytes() == tracker_bytes
    # Newly-found source file got fresh companions (FR-011 e).
    assert (net / "lib" / "new.cs").is_file()
    assert (net / "lib" / "new.dart").is_file()
    # Preserved source rewritten from current source (FR-011 c).
    assert (net / "lib" / "a.dart").read_bytes() == (
        src / "lib" / "a.dart"
    ).read_bytes()


# ---------------------------------------------------------------------------
# FR-036 — companion collision pre-flight abort (nothing written)
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_collision_preflight_aborts(discover_repo: Path) -> None:
    src = _mk_source_tree(discover_repo)
    # A pre-existing NON-source file that the a.dart companion would hit.
    (src / "lib" / "a.cs").write_text("pre-existing\n", encoding="utf-8")
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo).returncode == 0

    proc = _mirror(discover_repo)
    assert proc.returncode == 1, (proc.returncode, proc.stdout, proc.stderr)
    assert "collision" in (proc.stdout + proc.stderr).lower()
    # Nothing written.
    assert not (discover_repo / "glp_runtime_net").exists()


# ---------------------------------------------------------------------------
# FR-028 — refuse without an initialised workspace
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_refuses_without_workspace(discover_repo: Path) -> None:
    _mk_source_tree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    # No init → no workspace pair.
    proc = _mirror(discover_repo)
    assert proc.returncode == 2, (proc.returncode, proc.stdout, proc.stderr)
    assert "init" in (proc.stdout + proc.stderr).lower()
    assert not (discover_repo / "glp_runtime_net").exists()


# ---------------------------------------------------------------------------
# FR-042 — init --include-pruned force-includes a standard-pruned dir
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_force_include_resurrects_pruned_dir(
    discover_repo: Path,
) -> None:
    src = _mk_source_tree(discover_repo)
    # `build` is standard-pruned; `.dart_tool` also standard-pruned.
    (src / "build").mkdir()
    (src / "build" / "gen.dart").write_text(
        "/// gen\nclass G {}\n", encoding="utf-8"
    )
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(discover_repo, "--include-pruned", "build").returncode == 0
    assert _mirror(discover_repo).returncode == 0

    net = discover_repo / "glp_runtime_net"
    # build/ force-included → present + preserved + companions.
    assert (net / "build" / "gen.dart").is_file()
    assert (net / "build" / "gen.cs").is_file()
    # .dart_tool was NOT force-included → still pruned.
    assert not (net / ".dart_tool").exists()


# ---------------------------------------------------------------------------
# FR-043 — init --mirror-exclude (gitignore-style) prunes extra paths
# ---------------------------------------------------------------------------


@needs_bridge
def test_mirror_exclude_pattern_prunes_dir_and_file(
    discover_repo: Path,
) -> None:
    src = _mk_source_tree(discover_repo)
    (src / "lib" / "gen").mkdir()
    (src / "lib" / "gen" / "x.dart").write_text(
        "/// x\nclass X {}\n", encoding="utf-8"
    )
    (src / "lib" / "keep.tmp").write_text("scratch\n", encoding="utf-8")
    assert run_codeconv(discover_repo, "migrate").returncode == 0
    assert _init(
        discover_repo,
        "--mirror-exclude",
        "gen/",
        "--mirror-exclude",
        "*.tmp",
    ).returncode == 0
    assert _mirror(discover_repo).returncode == 0

    net = discover_repo / "glp_runtime_net"
    assert not (net / "lib" / "gen").exists()  # dir pattern pruned
    assert not (net / "lib" / "keep.tmp").exists()  # file pattern pruned
    # Unrelated source still mirrored.
    assert (net / "lib" / "a.dart").is_file()


# ---------------------------------------------------------------------------
# FR-041 — full chain init → mirror → discover → depgraph → scaffold
# ---------------------------------------------------------------------------


@needs_bridge
def test_full_chain_init_mirror_discover_depgraph_scaffold(
    discover_repo: Path,
) -> None:
    _mk_source_tree(discover_repo)
    assert run_codeconv(discover_repo, "migrate").returncode == 0

    # init defers (glp_runtime_net absent).
    pi = _init(discover_repo)
    assert pi.returncode == 0, f"{pi.stdout}\n{pi.stderr}"
    assert json.loads(_extract_json(pi.stdout)).get("inventory_deferred")

    # mirror produces glp_runtime_net from glp_runtime.
    assert _mirror(discover_repo).returncode == 0

    # discover now has a subtree to walk.
    pd = run_codeconv(discover_repo, "discover", "run", "--json")
    assert pd.returncode == 0, f"discover: {pd.stdout}\n{pd.stderr}"

    # depgraph compute (feature 015 — must interoperate).
    pg = run_codeconv(discover_repo, "depgraph", "compute", "--json")
    assert pg.returncode == 0, f"depgraph: {pg.stdout}\n{pg.stderr}"

    # scaffold mirrors glp_runtime_net → out/csharp/*.cs.
    ps = run_codeconv(discover_repo, "scaffold", "run", "--json")
    assert ps.returncode == 0, f"scaffold: {ps.stdout}\n{ps.stderr}"

    # Cross-stage consistency.
    from codeconv.bridge_client import acquire_or_discover
    from codeconv.db.engine import build_engine
    from sqlalchemy import text

    endpoint = acquire_or_discover(discover_repo, ready_timeout=60.0)
    engine = build_engine(endpoint)
    with engine.connect() as c:
        files = sorted(
            r[0]
            for r in c.execute(
                text("SELECT path FROM codeconv.dart_files")
            ).all()
        )
        # Mirror's verbatim .dart files were inventoried by discover.
        assert "lib/a.dart" in files, files
        assert "lib/runtime/heap.dart" in files, files
        assert not any("dart_tool" in f for f in files), files
    # Scaffold produced the C# target tree from the mirrored subtree.
    assert (discover_repo / "out" / "csharp" / "lib" / "a.cs").is_file()
