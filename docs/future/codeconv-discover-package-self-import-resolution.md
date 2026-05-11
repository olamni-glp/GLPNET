# Follow-up: codeconv-discover should resolve `package:glp_runtime/...` as in-subtree edges

**Filed**: 2026-05-11 (post v2026.05.11-2)
**Predecessor**: feature 012 (`specs/012-codeconv-runner/`)
**Status**: Open. No work started.

## Context

After feature 012 landed (`v2026.05.11` / `v2026.05.11-2`), running `/codeconv-discover` against the live `glp_runtime_net/` produced:

```
files:    128
edges:    146
isolated: 70 (no in-subtree imports + no in-subtree callers)
```

55% of files (70 / 128) show as isolated in the import graph. The graph captured is technically correct per the current spec but is a strict subset of the real dependency graph.

## Root cause

`codeconv/src/codeconv/tools/discover/parse.py::extract_imports` (per research note R12) unconditionally skips `package:` and `dart:` import targets. The intent was to filter genuinely external dependencies (pub packages, the Dart SDK). But the `glp_runtime_net/` subtree uses `package:`-form imports for its OWN internal references:

```dart
// In glp_runtime_net/lib/runtime/heap_fcp.dart:
import 'package:glp_runtime/runtime/terms.dart';
import 'package:glp_runtime/runtime/suspension.dart';
import 'package:glp_runtime/runtime/machine_state.dart';
import 'package:glp_runtime/multiagent/variable_table.dart' show VariableEntry;
```

These are self-references — they should resolve to `glp_runtime_net/lib/{runtime/terms.dart, ...}` per Dart's `package:` resolution rules — but `extract_imports` discards them along with truly external `package:` imports.

`glp_runtime_net/pubspec.yaml`:
```yaml
name: glp_runtime
```

So `package:glp_runtime/X` ↔ `glp_runtime_net/lib/X` is a direct, unambiguous mapping.

## Proposed fix

Special-case the self-package prefix in `extract_imports` (and in `workflow._scan_outside_callers`):

1. Read the subtree's `pubspec.yaml` to learn the package name (`glp_runtime`) on discover start.
2. For each `import 'package:<NAME>/<rest>';` where `<NAME>` matches: rewrite to `<rest>` relative to `<subtree>/lib/`, then record as a normal in-subtree edge.
3. All other `package:` and `dart:` imports continue to be skipped (truly external).

After the fix, the same `glp_runtime_net/` should yield closer to 128 files × ~3-5 imports each → ~400-600 edges, dramatically reducing the isolated count.

## Scope

- `codeconv/src/codeconv/tools/discover/parse.py` — pass `package_name` + `package_root` through `extract_imports`.
- `codeconv/src/codeconv/tools/discover/workflow.py::_scan_outside_callers` — same treatment so outside-subtree `package:` imports that point INTO the subtree generate proper `outside_caller` warnings instead of being silently dropped.
- `codeconv/src/codeconv/tools/discover/walker.py` — likely unchanged.
- `codeconv/tests/test_parse.py` — add `test_resolves_self_package_imports` + `test_external_package_imports_still_skipped`.
- `codeconv/tests/test_discover_*.py` — at least one end-to-end assertion that, after the fix, `heap_fcp.dart` shows 4 dependencies (terms, suspension, machine_state, variable_table) instead of 0.
- `specs/012-codeconv-runner/research.md` § R12 — amend the rule, or supersede with a follow-up research note in the new feature's spec dir.

## Spec impact

| FR | Impact |
|---|---|
| FR-019 (UNIQUE `(from_path, to_path)`) | Unchanged. |
| FR-023 (caller-graph scope inside-only) | Unchanged; `package:`-resolved edges that fall inside the subtree are inside-subtree by construction. |
| FR-018 (subtree scope: `<root>/**/*.dart`) | Unchanged. |
| FR-024 (idempotence) | Preserved; the per-file `(mtime, sha256)` short-circuit still works because re-running on unchanged source produces the same edges. |

## Out of scope

- Other `package:` self-references where the importing project is not `glp_runtime` itself (no such projects in this repo today).
- Pub workspace resolution (where one package depends on another by path). Not currently used.
- Translating `package:` references to canonical `glp_runtime_net/lib/` prefixed paths in `dart_imports.to_path` — the current convention is "POSIX path relative to the subtree root" which `lib/runtime/terms.dart` already satisfies.

## Acceptance

- Re-run `/codeconv-discover` against this repo: `isolated` count drops well below the current 70.
- `lib/runtime/heap_fcp.dart` tombstone shows `dependencies:` with `lib/runtime/{terms,suspension,machine_state}.dart` and `lib/multiagent/variable_table.dart`.
- All existing tests still pass (no regression of the "external `package:` skipped" behaviour).
- New tests cover the self-package rewrite path.

## Why not just patch this in 012

Feature 012's contract was sealed and shipped. The R12 decision was explicit ("regex-extract; skip `package:`/`dart:`; resolve relative paths"). Reopening 012 to amend R12 would re-litigate a closed clarification. Cleaner to track this as its own small feature.

## Suggested feature ID

`014-package-self-import-resolution` (or whatever the next available NNN slot is). See `docs/BRANCHING.md` for branch naming.
