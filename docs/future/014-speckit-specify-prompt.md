# Prepared `/speckit-specify` prompt — feature 014

Copy-paste the block below into a `/speckit-specify` invocation when you're ready to start the feature. The body is intended to be the natural-language input the slash command consumes.

---

```
Feature: codeconv-discover resolves `package:glp_runtime/...` imports as in-subtree edges.

Motivation. Feature 012 shipped `/codeconv-discover` with research-note R12 explicitly skipping every `package:` and `dart:` import target. That was correct for genuinely external dependencies but wrong for self-references: the `glp_runtime_net/` subtree (declared in `glp_runtime_net/pubspec.yaml` as `name: glp_runtime`) uses `package:glp_runtime/...` form for its OWN internal imports. As a result, 70 of the 128 inventoried files (55%) currently appear as isolated in `codeconv.dart_imports` even though they have real in-subtree dependencies. The graph as captured is a strict subset of the true dependency graph.

Concrete example. `glp_runtime_net/lib/runtime/heap_fcp.dart` has these imports verbatim:
  import 'package:glp_runtime/runtime/terms.dart';
  import 'package:glp_runtime/runtime/suspension.dart';
  import 'package:glp_runtime/runtime/machine_state.dart';
  import 'package:glp_runtime/multiagent/variable_table.dart' show VariableEntry;
After 012, its tombstone shows `dependencies: []`. After this feature, it should show four entries: `lib/multiagent/variable_table.dart`, `lib/runtime/machine_state.dart`, `lib/runtime/suspension.dart`, `lib/runtime/terms.dart`.

Scope of work.
- Extend `codeconv/src/codeconv/tools/discover/parse.py::extract_imports` so that imports of the form `package:<PACKAGE_NAME>/<rest>` — where `<PACKAGE_NAME>` matches the `name:` field of the subtree's `pubspec.yaml` — are rewritten to `<subtree>/lib/<rest>` and recorded as normal in-subtree edges.
- Apply the same rewrite in `codeconv/src/codeconv/tools/discover/workflow.py::_scan_outside_callers`, so that an outside-subtree `.dart` file importing into the subtree via `package:glp_runtime/...` produces a proper `outside_caller` warning (per FR-023) instead of being silently dropped.
- Truly external `package:` imports (e.g. `package:meta/meta.dart`, `package:json_annotation/...`) MUST continue to be skipped, with no warning. Same for `dart:` and `dart-ext:`.
- `pubspec.yaml` read should be cached per-discover-run (don't re-read for every file). Behaviour when `pubspec.yaml` is absent or unparseable: fall back to feature-012 behaviour (skip all `package:` imports), emit one warning.

Out of scope.
- Pub workspace resolution where multiple packages in the same repo cross-reference by `package:`. Not currently used in this repo.
- `package:`-prefix references that resolve OUTSIDE the subtree (e.g. if `glp_runtime` ever has additional library dirs outside `lib/`). Today the mapping `package:glp_runtime/X` → `<subtree>/lib/X` is total.
- Changing `dart_imports.to_path` encoding. Stay with the existing "POSIX path relative to the subtree root" convention.
- Reopening any feature-012 contract. R12 stays as written; this feature ADDS the self-package rewrite as a layered rule and supersedes the relevant lines of R12 with its own research note.

Success criteria.
- Re-run `/codeconv-discover` against the live `glp_runtime_net/`: `isolated` count drops well below 70 (target: under 20).
- `lib/runtime/heap_fcp.dart`'s tombstone shows the four `package:glp_runtime/...`-resolved dependencies listed above.
- All 39 existing `pytest codeconv/tests/` cases still pass without modification.
- At least two new unit tests in `codeconv/tests/test_parse.py`: one that asserts `package:<self>/foo.dart` resolves correctly, one that asserts `package:meta/meta.dart` is still skipped.
- At least one integration test confirming the heap_fcp-style fan-in works end-to-end (covers the workflow path, not just parse.py).
- Idempotence preserved: a second `/codeconv-discover` after this feature lands produces zero diff in DB rows + zero diff in tombstone files. Verifiable by running discover twice and asserting `files_skipped_idempotent == files_walked` on the second.
- Tombstones on `main` are refreshed once the feature lands (the inventory commits in `v2026.05.11-2` will need a single re-run + commit to pick up the new edges).

Constraints.
- Stay on the existing `codeconv/` Python package, the existing `prereq-patterns/pglite/pglite_bridge.mjs`, and the existing `.codeconv/tombstones/` layout. No new external dependencies (the `pubspec.yaml` parse can use stdlib YAML or `python-frontmatter`'s yaml dependency which is already pinned).
- Must work with both `--data-dir` override and the default `<repo>/.pgdb`. Feature 013's wiring stays in place untouched.
- Preserve FR-026 (no `COPY ... FROM STDIN`) and FR-027 (no client-side prepared-statement caching). Greps in feature 012's Phase 7 stay green.

Branch and version.
- Branch `014-package-self-import-resolution` off `main` per `docs/BRANCHING.md`.
- New release tag after merge: same-day CalVer suffix (e.g. `v2026.05.11-3` if same day; otherwise `vYYYY.MM.DD`).

Reference.
- Full background: `docs/future/codeconv-discover-package-self-import-resolution.md`.
- Predecessor feature: `specs/012-codeconv-runner/`.
- Research note to amend / supersede: `specs/012-codeconv-runner/research.md` § R12.
```

---

## How to use

1. Open a fresh Claude Code session at this repo root.
2. Type `/speckit-specify` followed by the entire block above (between the `---` markers).
3. The skill will scaffold `specs/014-package-self-import-resolution/spec.md` from your input.
4. Continue with `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` → `/speckit-implement` as with feature 012.
