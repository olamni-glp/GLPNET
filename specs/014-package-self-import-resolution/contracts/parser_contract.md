# Contract: `codeconv.tools.discover.parse.extract_imports` — feature 014 delta

This document specifies the SIGNATURE and BEHAVIOUR of `extract_imports` after this feature lands. It is the contract; the implementation in `codeconv/src/codeconv/tools/discover/parse.py` follows it exactly. Any deviation is a bug.

## Source of truth references

- Spec FRs covered: FR-001, FR-002, FR-003, FR-004 (caching is workflow-side, but extract_imports MUST accept the cached value rather than re-read), FR-007 (UNIQUE collapse), FR-009 (path-shape preservation)
- Research notes: R14 (rewrite layering), R18 (perf)
- Code today: `codeconv/src/codeconv/tools/discover/parse.py` lines 132-181

## Signature

### Before this feature (feature 012)

```python
def extract_imports(file_path: Path, subtree_root: Path) -> List[str]:
    ...
```

### After this feature

```python
def extract_imports(
    file_path: Path,
    subtree_root: Path,
    package_name: Optional[str] = None,
) -> List[str]:
    ...
```

The third parameter is OPTIONAL with default `None`. This preserves backwards-compatibility for any test or future caller that does not pass it (current behaviour: every `package:` import skipped — feature-012 R12 verbatim). All in-tree callers MUST pass the cached `package_name` from `workflow.run_discover`.

## Behaviour

For each `import 'X';` or `import "X";` directive matched by the existing `_IMPORT_RE` regex (unchanged), in source order:

1. **Self-package rewrite** (NEW): if `package_name is not None` AND the target equals `f"package:{package_name}/"` followed by at least one character (i.e. `target.startswith(f"package:{package_name}/")` and `len(target) > len(prefix)`):
   - Rewrite the target to `f"lib/{target[len(prefix):]}"` (POSIX, single forward slashes).
   - Continue to step 3 (relative-path resolution) with the rewritten target.
2. **External skip** (UNCHANGED from R12): else if `target.startswith("package:")` OR `target.startswith("dart:")` OR `target.startswith("dart-ext:")`:
   - Skip silently (no warning, no entry in result).
3. **Resolve and check membership** (UNCHANGED from R12): treat the (possibly rewritten) target as a path relative to the importing file's directory. Resolve via `pathlib.Path(file_dir / target).resolve()`. If the resolved path lies inside `subtree_root.resolve()`, compute its POSIX-relative form and continue; else skip.
4. **Dedup + collect** (UNCHANGED from R12): if the POSIX-relative path is already in the per-call `seen` set, append the original target to a duplicates buffer; else add to `seen` and append to the result list.

After all directives are processed: if the duplicates buffer is non-empty, emit a `warnings.warn(...)` with text containing the substring `"duplicate import"` and the file path (current behaviour, FR-019 / FR-007 — UNCHANGED). Sort the result list lexically and return it.

### Critical invariant (FR-009)

When step 1 fires, the rewritten target's resolved POSIX-relative form MUST be byte-identical to what step 3 would produce if the SAME target had been written as a relative path of equivalent semantics. For example:

```
file_path  = glp_runtime_net/lib/runtime/heap_fcp.dart
target #1  = 'package:glp_runtime/runtime/terms.dart'   → rewrite → 'lib/runtime/terms.dart'
target #2  = '../runtime/terms.dart'                    → relative-resolve → 'lib/runtime/terms.dart'
```

Both yield POSIX-relative `lib/runtime/terms.dart`. The dedup `seen` set thus collapses them to one row (FR-007), and the post-rewrite duplicates buffer fires the FR-019 warning if both forms appear in the same file.

The "rewritten then resolved" path equals the "relative then resolved" path because step 1 explicitly converts to a `lib/...`-rooted path RELATIVE TO THE SUBTREE ROOT — but step 3 resolves RELATIVE TO THE IMPORTING FILE'S DIRECTORY, which is also under the subtree. The net effect: when `file_dir / "lib/runtime/terms.dart"` is resolved, the leading `lib/` becomes a no-op only when `file_dir` is itself under `<subtree>/lib/...`. To make step 1 unconditionally produce subtree-relative POSIX, the implementation MUST instead resolve the rewritten target against `subtree_root` directly, not against `file_dir`.

**Implementation note** (binding on parse.py): the simplest correct shape is —

```python
if package_name is not None:
    prefix = f"package:{package_name}/"
    if target.startswith(prefix) and len(target) > len(prefix):
        rest = target[len(prefix):]
        # Resolve against subtree_root, not file_dir, because Dart's
        # package: form is anchored at <package_root>/lib/, not at the
        # importing file's directory.
        try:
            resolved = (sub_real / "lib" / rest).resolve()
        except (OSError, RuntimeError):
            continue
        # Fall through to the in-subtree-membership check below with
        # `resolved` already computed; skip the file_dir relative resolve.
        rel = resolved.relative_to(sub_real).as_posix()
        ...continue with dedup
```

The rewritten path is anchored at `<subtree>/lib/<rest>`. This is by Dart convention (per the pub specification), not a project policy.

## Examples

| Input target                                                  | `package_name` | Result                                |
|---------------------------------------------------------------|----------------|---------------------------------------|
| `'package:glp_runtime/runtime/terms.dart'`                    | `'glp_runtime'`| `lib/runtime/terms.dart`              |
| `'package:glp_runtime/multiagent/variable_table.dart'`        | `'glp_runtime'`| `lib/multiagent/variable_table.dart`  |
| `'package:meta/meta.dart'`                                    | `'glp_runtime'`| (skipped — external)                  |
| `'package:glp_runtime/'` (no rest)                            | `'glp_runtime'`| (skipped — malformed; len check fails)|
| `'package:glp_runtime'` (no slash)                            | `'glp_runtime'`| (skipped — `startswith(prefix)` fails)|
| `'dart:io'`                                                   | `'glp_runtime'`| (skipped — dart: SDK)                 |
| `'dart-ext:vm/process'`                                       | `'glp_runtime'`| (skipped — dart-ext)                  |
| `'../runtime/terms.dart'` from `lib/runtime/heap_fcp.dart`    | `'glp_runtime'`| `lib/runtime/terms.dart`              |
| `'package:glp_runtime/runtime/terms.dart'`                    | `None`         | (skipped — fallback to feature-012 behaviour) |

## What does NOT change

- The `_IMPORT_RE` regex is unchanged. The rewrite is applied to the captured `target` string, not to the regex.
- The 200-line cap from `extract_leading_doc` does NOT apply to `extract_imports`; the latter reads the whole file (unchanged).
- The duplicate-import warning's text and shape are unchanged. FR-019.
- The function's return type is unchanged: `List[str]`, sorted lexically.
- `extract_leading_doc` is untouched by this feature.

## Test obligations (covered in `test_parse.py` and `test_pubspec.py` per `tasks.md`)

1. `test_resolves_self_package_imports` — passes `package_name="glp_runtime"`; verifies a single `package:glp_runtime/foo/bar.dart` import yields `"lib/foo/bar.dart"` in the result.
2. `test_external_package_imports_still_skipped` — passes `package_name="glp_runtime"`; verifies `package:meta/meta.dart` and `package:json_annotation/...` produce empty results.
3. `test_self_package_when_package_name_none` — passes `package_name=None`; verifies `package:glp_runtime/foo.dart` is skipped (regression guard for the optional-arg default).
4. `test_self_package_dedup_against_relative` — passes `package_name="glp_runtime"`; file imports the same module via BOTH `package:glp_runtime/foo.dart` AND `'../foo.dart'`; verifies one row + one duplicate-import warning (FR-007).
5. `test_malformed_self_package_skipped` — `package:glp_runtime/` (no rest), `package:glp_runtime` (no slash); verifies both skipped silently.
6. `test_self_package_outside_lib_skipped` — `package:glp_runtime/test/foo.dart` resolves outside the package's `lib/` root; per Dart convention these are invalid and should be skipped (no warning).

The five existing tests in `test_parse.py` (lines 30-148) MUST continue to pass without modification — they all call `extract_imports(src, sub)` with two args, which is the `package_name=None` fallback shape.
