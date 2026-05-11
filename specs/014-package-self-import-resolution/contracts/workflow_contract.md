# Contract: `codeconv.tools.discover.workflow` and `pubspec` module — feature 014 delta

This document specifies the workflow-level contract: how the cached `pubspec.yaml`-derived `package_name` flows through `run_discover` → `_run_normal` → `_process_one_file` → `extract_imports`, and how the same value is used inside `_scan_outside_callers`.

## Source of truth references

- Spec FRs covered: FR-004 (cache once per run), FR-005 (warning on absent/unparseable/no_name), FR-006 (outside-caller scan parity), FR-008 (idempotence preserved)
- Research notes: R15 (caching shape), R16 (warning shape), R17 (idempotence)
- Code today: `codeconv/src/codeconv/tools/discover/workflow.py` (entire module)

## New module: `codeconv/src/codeconv/tools/discover/pubspec.py`

```python
"""Pubspec.yaml loader for /codeconv-discover — feature 014 / FR-004 / FR-005.

One function: read_package_name(subtree_root) -> (name_or_None, warning_or_None).
Called exactly once per /codeconv-discover invocation by workflow.run_discover.
"""

from __future__ import annotations

from pathlib import Path
from typing import Optional

import yaml


def read_package_name(
    subtree_root: Path,
    *,
    repo_root: Optional[Path] = None,
) -> tuple[Optional[str], Optional[dict]]:
    """Read the subtree's pubspec.yaml; return (name, warning_or_None).

    Behaviour:
      - File absent          → (None, {"kind": "pubspec_missing", "path": ..., "reason": "absent"})
      - YAMLError on load    → (None, {"kind": "pubspec_missing", "path": ..., "reason": "unparseable"})
      - Loads but no 'name'  → (None, {"kind": "pubspec_missing", "path": ..., "reason": "no_name_field"})
      - Happy path           → (name, None)

    'path' in the warning is POSIX-relative against repo_root if repo_root is
    supplied AND the pubspec is under it; otherwise the absolute path string
    of the expected location.
    """
    expected = (subtree_root / "pubspec.yaml").resolve()

    def _path_for_warning() -> str:
        if repo_root is not None:
            try:
                return expected.relative_to(repo_root.resolve()).as_posix()
            except ValueError:
                pass
        return str(expected)

    if not expected.exists():
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "absent",
        }

    try:
        text = expected.read_text(encoding="utf-8", errors="replace")
        data = yaml.safe_load(text)
    except (OSError, yaml.YAMLError):
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "unparseable",
        }

    if not isinstance(data, dict):
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "unparseable",
        }

    name = data.get("name")
    if not isinstance(name, str) or not name.strip():
        return None, {
            "kind": "pubspec_missing",
            "path": _path_for_warning(),
            "reason": "no_name_field",
        }

    return name.strip(), None


__all__ = ["read_package_name"]
```

## `workflow.run_discover` — call sequence (insertion points)

The current `run_discover` (workflow.py lines 65-137) gains the following changes:

```python
def run_discover(
    repo_root: Path,
    *,
    mode: str = "normal",
    root: Optional[Path] = None,
    dry_run: bool = False,
    no_orphan_revival: bool = False,
    quiet: bool = True,
    bridge_script: Optional[Path] = None,
    data_dir: Optional[Path] = None,
) -> dict:
    repo_root = Path(repo_root).resolve()
    subtree = (root or (repo_root / "glp_runtime_net")).resolve()
    tombstones_root = repo_root / ".codeconv" / "tombstones"
    tombstones_root.mkdir(parents=True, exist_ok=True)

    started_at = _utc_now()
    run_id = str(uuid.uuid4())

    # NEW (feature 014): read pubspec ONCE; cache (name, warning) for the run.
    # Per FR-004 / R15: per-run, in-memory, single read.
    package_name, pubspec_warning = read_package_name(
        subtree, repo_root=repo_root
    )

    endpoint = acquire_or_discover(...)
    engine = build_engine(endpoint)

    with engine.begin() as conn:
        conn.execute(text("INSERT INTO codeconv.discover_runs ..."), ...)

    try:
        if mode == "from_tombstones":
            summary = _run_from_tombstones(...)  # UNCHANGED
        else:
            summary = _run_normal(
                engine, run_id, repo_root, subtree, tombstones_root,
                dry_run, no_orphan_revival, quiet,
                package_name=package_name,        # NEW
                pubspec_warning=pubspec_warning,  # NEW
            )
    finally:
        with engine.begin() as conn:
            conn.execute(text("UPDATE codeconv.discover_runs ..."), ...)

    summary["mode"] = mode
    ...
    return summary
```

### `_run_normal` — propagation

`_run_normal` accepts the two new keyword arguments and:

1. **Pubspec warning (one shot)**: if `pubspec_warning is not None`, append it to `warnings_list` exactly once at the start of the function, BEFORE the per-file loop. Per FR-005 — emit exactly one warning regardless of file count.
2. **Per-file processing**: pass `package_name` to `_process_one_file(... , package_name=package_name)`. The latter forwards it to `extract_imports(abs_path, subtree, package_name)`.
3. **Outside-caller scan**: pass `package_name` to `_scan_outside_callers(repo_root, subtree, package_name=package_name)`.

### `_process_one_file` — propagation only

```python
def _process_one_file(
    engine, run_id, abs_path, rel_path, subtree, tombstones_root,
    dry_run, warnings_list,
    *, package_name: Optional[str] = None,   # NEW
) -> str:
    ...
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        imports_list = extract_imports(abs_path, subtree, package_name)  # MODIFIED
    ...
```

The default `package_name=None` keeps any direct test caller working unchanged.

### `_scan_outside_callers` — rewrite parity (FR-006)

The inline `_IMPORT_RE` loop in `_scan_outside_callers` (workflow.py lines 487-509) gains the same rewrite step as `extract_imports`:

```python
def _scan_outside_callers(
    repo_root: Path,
    subtree: Path,
    *,
    package_name: Optional[str] = None,   # NEW
) -> list[dict]:
    out: list[dict] = []
    if not repo_root.is_dir():
        return out
    subtree_real = subtree.resolve()
    self_prefix = (
        f"package:{package_name}/" if package_name is not None else None
    )
    for sibling in repo_root.iterdir():
        if not sibling.is_dir() or sibling.resolve() == subtree_real:
            continue
        if sibling.name in {".git", ".pgdb", ".codeconv", "node_modules", ".venv"}:
            continue
        for abs_path, _ in walk_dart_files(sibling):
            try:
                content = abs_path.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for m in _IMPORT_RE.finditer(content):
                target = m.group("target").strip()

                # NEW (feature 014): self-package rewrite parity.
                if (
                    self_prefix is not None
                    and target.startswith(self_prefix)
                    and len(target) > len(self_prefix)
                ):
                    rest = target[len(self_prefix):]
                    try:
                        resolved = (subtree_real / "lib" / rest).resolve()
                    except (OSError, RuntimeError):
                        continue
                    try:
                        inside_rel = resolved.relative_to(subtree_real).as_posix()
                    except ValueError:
                        continue
                    try:
                        outside_rel = abs_path.resolve().relative_to(repo_root).as_posix()
                    except ValueError:
                        outside_rel = str(abs_path)
                    out.append({
                        "kind": "outside_caller",
                        "outside_file": outside_rel,
                        "inside_file": inside_rel,
                    })
                    continue

                if target.startswith(("package:", "dart:", "dart-ext:")):
                    continue

                # Existing relative-path resolution (UNCHANGED) ...
```

The relative-path branch is unchanged. The new branch handles the self-package form before the external-skip branch, in exact parity with `extract_imports`.

## Idempotence (R17 / FR-008)

- The per-file `(mtime, sha256)` short-circuit (workflow.py lines 322-328) is UNTOUCHED.
- The first run after this feature lands triggers reparse for every file because no `(mtime, sha256)` row exists yet for the new edge set's POV — wait, that's not right: the existing rows have `sha256` matching the file content. The short-circuit fires and skips reparse.
- This is a CORRECT outcome: the existing tombstones still need to be refreshed because their `dependencies` lists are stale (they reflect feature-012's parser). The SC-007 single tombstone-refresh recipe in `quickstart.md` Flow G addresses this by:
  1. After the code change lands, manually delete the existing rows in `codeconv.dart_files` (via `TRUNCATE codeconv.dart_files, codeconv.dart_imports, codeconv.dart_callers`) — forces a full re-parse.
  2. Run `/codeconv-discover` once; tombstones are rewritten with the new edges.
  3. Commit the resulting tombstone diff alongside the code.
- The SECOND consecutive run (the SC-004 idempotence test) finds every `(mtime, sha256)` matches; takes the short-circuit; produces zero diff in DB rows AND zero diff in tombstone files. This is the FR-008 / SC-004 invariant; it is verified by `test_discover_idempotence.py` (existing, must pass without modification).

**Subtlety**: The "refresh trick" (TRUNCATE before discover) is a one-time operational step on the feature branch, NOT part of the workflow's runtime contract. It is not encoded in `run_discover` itself. Operators perform it manually (or via a `--refresh` flag we choose NOT to add — out of scope per spec line 92).

## Performance (R18 / SC-006)

The added work per `run_discover`:

- One `read_package_name(subtree)` call → ~5 ms on Windows (one stat + one ~1 KB read + one `yaml.safe_load`).
- One `target.startswith(prefix)` check + one slice per import directive → ~640 ops total → < 1 ms aggregate.

Total added cost: ~5 ms per discover run. Well under the 60 s / 5 s SLO bounds. The existing `test_discover_perf.py` (gated by `pytest --run-perf`) is the regression guard.

## Test obligations (per `tasks.md`)

### `test_pubspec.py` (NEW)

1. `test_happy_path` — fixture pubspec with `name: glp_runtime` → `("glp_runtime", None)`.
2. `test_pubspec_absent` → `(None, {"kind": "pubspec_missing", "reason": "absent", ...})`.
3. `test_pubspec_unparseable` — fixture with malformed YAML → `(None, {... "reason": "unparseable", ...})`.
4. `test_pubspec_no_name_field` — fixture with valid YAML but no `name:` → `(None, {... "reason": "no_name_field", ...})`.
5. `test_pubspec_name_empty_string` → reason `no_name_field`.
6. `test_pubspec_name_non_string` → reason `no_name_field`.
7. `test_warning_path_is_posix_relative_when_repo_root_supplied` — verifies the path field's shape.

### `test_discover_self_package_e2e.py` (NEW)

1. `test_heap_fcp_style_fanin` — synthesises a fixture mirroring `lib/runtime/heap_fcp.dart`'s four `package:glp_runtime/...` imports plus its target files; runs full discover; asserts:
   - `dart_imports` contains the four expected `(from, to)` pairs.
   - The file's tombstone `dependencies:` list contains the four expected paths, lex-sorted.
2. `test_external_package_still_skipped_e2e` — synthesises a file with `package:meta/meta.dart`; verifies no edge recorded, no warning emitted.
3. `test_pubspec_absent_falls_back_to_isolated` — synthesises a subtree without `pubspec.yaml`; verifies discover completes successfully, emits the `pubspec_missing` warning exactly once, and produces an isolated graph for any `package:glp_runtime/...` imports.

### `test_outside_subtree_warning.py` (MODIFIED)

Add one new test function (existing test stays as the relative-path baseline):

- `test_outside_caller_via_package_form_warns` — synthesises an outside-subtree file with `import 'package:glp_runtime/lib/heap.dart';`; verifies the same `outside_caller` warning shape as the relative-path case; verifies no caller edge recorded.

The existing `test_outside_caller_warns_no_edge` MUST continue to pass without modification — it uses a relative `'../glp_runtime_net/lib/heap.dart'` import which is unaffected by the rewrite logic.
