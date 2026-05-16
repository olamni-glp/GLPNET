# Current Plan: 015-codeconv-depgraph — resume at #16/#17 (Amendment v2 discover changes)

Branch: `015-codeconv-depgraph`. Started resume: 2026-05-16. Base commit `08f37312` (clean).

Source of truth for #16/#17 behaviour:
- `specs/012-codeconv-runner/contracts/codeconv_discover_cli.md` § Steps (--from-tombstones mode) / § Steps (--verify-tombstones mode) / § Exit codes
- `specs/015-codeconv-depgraph/data-model.md` § 4 (Option B rationale)
- `specs/015-codeconv-depgraph/contracts/tombstone_format_delta.md`

The `#16..#19` numbering is the codex-review punch list (NOT tasks.md T0xx).

## Steps
- [x] 1. Mandatory reading + state reconciliation (plan file was stale/014)
- [x] 2. Read governing contracts + current discover/depgraph impl
- [ ] 3. #16a — discover exit-code plumbing: `run_discover` summaries carry `exit_code`; `discover/__init__.py::_emit_summary` raises `typer.Exit(code)` + prints `summary["error"]` to stderr (mirror depgraph). Normal-mode stays exit 0 (no regression). exit 73 "all skipped" is pre-existing/out-of-scope — NOT introduced here (would regress SC-008 idempotence); flagged for separate spec-first discussion.
- [ ] 4. #16b — `_preflight_from_tombstones`: walk T (excl `.orphaned/`) → parse YAML → required-012-field validate (path+sha256 non-empty; name/purpose/key_idea/mtime default; deps/callers list-coerce) → optional-015-key type-invalid warn-not-drop → referential-completeness (drop+warn dangling edges). Runs BEFORE bridge/discover_runs/DB. Abort → `{exit_code:65}`, zero bridge/DB touch.
- [ ] 5. #16c — `_run_from_tombstones` Option B: ONE transaction — TRUNCATE dart_imports/dart_callers; UPSERT dart_files (ON CONFLICT path DO UPDATE); DELETE only paths absent from T (cascades vanished conversions/depgraph); reinsert ref-complete edges; UPSERT `.orphaned/` → dart_files_orphaned (contract step 6).
- [ ] 6. #17 — `--verify-tombstones` flag (mutually exclusive w/ `--from-tombstones`), `mode="verify_tombstones"`: NO bridge (skip acquire), reads `.dart`, builds whole-subtree import graph once (extract_imports + package_name), per-tombstone sha256+deps+callers diff (stale→warn), missing-source / missing-tombstone warnings, skip `.orphaned/`, zero `.dart`→exit 1, parse-fail→exit 65. Read-only.
- [ ] 7. Tests: extend `test_from_tombstones.py` (2 reds now green + preflight-abort + Option-B-preserves-conversions + dangling-edge); new `test_verify_tombstones.py`; target_path round-trip.
- [ ] 8. #19 — full `codeconv` pytest re-verify (was 74 passed / 3 failed; the 3 = empty_inventory [Bug1, done] + 2× from_tombstones [Option B]).
- [ ] 9. Then 015 Phase 4–7 per `specs/015-codeconv-depgraph/tasks.md` (mark-* tests, cycle fixture, stamp/rebuild, SKILL.md, buildkit P2s, quickstart Flow H, tombstone refresh T043).

## Context
Restructure `codeconv/src/codeconv/tools/discover/workflow.py::run_discover` so mode dispatch happens BEFORE bridge acquisition: verify=no bridge; from_tombstones=preflight (exit 65 abort) before bridge then Option-B single-txn apply; normal=unchanged. Plumb `exit_code` through summaries; `_emit_summary` enforces process exit. Spec-First: contract text above is authoritative; any behaviour gap → STOP + amend spec, do not silently patch.

## Cautions
- `--data-dir C:/pglite/research/glpnet` for any production/live invocation (D: is exFAT). Tests use tmp_path (NTFS) — no override needed.
- pytest synchronously (background stdout capture was broken last session); PGLite cold-init ~7s.
- Tombstone refresh (T043) is the LAST commit — don't let interim runs leak tombstone diffs.
- Do NOT implement exit 73 here; out of #16/#17 scope, regresses SC-008.
