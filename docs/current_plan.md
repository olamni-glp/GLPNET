# Current Plan: 014-package-self-import-resolution — `/speckit-implement` (next session)

**Branch**: `014-package-self-import-resolution` (NOT yet pushed; specs/ artefacts uncommitted)
**Started**: 2026-05-11 (spec-kit chain — spec / plan / tasks / analyze done in this session)
**Last commit on branch**: `5eaf5f2b` (Merge PR #8 follow-up docs); branch state has the new specs/ tree on top, uncommitted.
**Resume point**: Run `/speckit-implement` from a fresh session to execute `tasks.md` (Phases 1-5).

## What's done in this session

- **/speckit-plan**: wrote `specs/014-package-self-import-resolution/{plan,research,data-model,quickstart}.md` and `contracts/{parser_contract,workflow_contract}.md`. Constitution gate noted as N/A (`.specify/memory/constitution.md` is a placeholder template); plan defers to CLAUDE.md / DISCIPLINE.md per project practice.
- **/speckit-tasks**: wrote `specs/014-package-self-import-resolution/tasks.md` with 31 tasks across Phase 1 (Setup) → Phase 2 (Foundational, intentionally empty) → Phase 3 (US1 P1) → Phase 4 (US2 P2) → Phase 5 (Polish + tombstone refresh + perf + idempotence verification).
- **/speckit-analyze**: cross-artefact consistency check ran. Zero CRITICAL, zero HIGH; 3 MEDIUM (F1/F3/F4) and 6 LOW (F2/F5-F10). 100% requirement-to-task coverage (9/9 FRs, 7/7 SCs).
- **Top-3 remediations applied** (F1, F3, F2):
  - F1: `contracts/workflow_contract.md` `_scan_outside_callers` snippet now shows inline warning dict construction (the prior `_outside_caller_warning(...)` helper does NOT exist in `workflow.py`).
  - F3: `tasks.md` T028 expanded to verify `codeconv doctor --truncate-codeconv` exists OR fall back to manual `psql TRUNCATE` recipe documented in `quickstart.md`.
  - F2: `plan.md` corrected — CLAUDE.md SPECKIT markers exist and were updated this run.
- **CLAUDE.md** SPECKIT block (lines ~536-540) repointed from feature 012's plan to feature 014's plan.

## What feature 014 changes

A pure-Python parser-and-workflow change in `codeconv/`. **No DB schema change. No new tombstone fields. No Dart / .NET / Node touched.**

- `codeconv/src/codeconv/tools/discover/parse.py::extract_imports` gains an optional `package_name: Optional[str] = None` arg. When non-None and a target matches `package:<package_name>/<rest>`, rewrite to `lib/<rest>` and resolve against `subtree_root / "lib"`. Else preserve feature-012 behaviour.
- `codeconv/src/codeconv/tools/discover/pubspec.py` (new module) exposes `read_package_name(subtree_root, *, repo_root=None) -> tuple[str | None, dict | None]`. Reads `<subtree>/pubspec.yaml` once; on absent/unparseable/no-name returns `(None, {"kind": "pubspec_missing", "path": <posix-relative>, "reason": "absent" | "unparseable" | "no_name_field"})`.
- `codeconv/src/codeconv/tools/discover/workflow.py::run_discover` calls `read_package_name` exactly once (FR-004), threads `package_name` through `_run_normal` → `_process_one_file` → `extract_imports` AND through `_scan_outside_callers` (FR-006 outside-caller scan parity).
- Test additions: 6 new functions in `test_parse.py`; new `test_pubspec.py` (7 functions); new `test_discover_self_package_e2e.py` (3 functions, `@needs_bridge`-gated); 1 new function in `test_outside_subtree_warning.py`.
- Tombstones: ONE post-implementation tombstone-refresh commit per SC-007. Use `codeconv doctor --truncate-codeconv` if it exists; otherwise the manual `psql TRUNCATE codeconv.dart_files, dart_imports, dart_callers, dart_files_orphaned;` recipe in `quickstart.md` Flow G step 2.

## What's left for the next session (`/speckit-implement`)

Work straight through `specs/014-package-self-import-resolution/tasks.md` in order:

- Phase 1 (T001-T004): baseline, env confirmation, snapshot the pre-feature `isolated` count.
- Phase 2: empty (no foundational work).
- Phase 3 / US1 (T005-T019): tests-first; then `pubspec.py`, `parse.py`, `workflow.py` plumbing; then run all parse/pubspec/e2e tests + full suite.
- Phase 4 / US2 (T020-T023): outside-caller scan parity + its test.
- Phase 5 (T024-T031): quickstart Flow G full smoke; perf (`--run-perf`); idempotence; tombstone refresh commit; FR-026/FR-027 greps; final full suite.

The PR contains exactly two logical commits:
1. Code commit: `codeconv/src/codeconv/tools/discover/{parse,workflow,pubspec}.py` + the four test files.
2. Tombstone refresh commit: `.codeconv/tombstones/...` only.

## 🔴 Branch instructions

Work on the existing `014-package-self-import-resolution` branch. Do NOT create a new `claude/...` branch.

```
git checkout 014-package-self-import-resolution
git pull origin 014-package-self-import-resolution   # only if branch is pushed; first push happens after the spec commit below
```

## 🔴 Pre-implement commit (for the next session to find these artefacts on the branch)

The new specs/ tree and CLAUDE.md edit are uncommitted in this session. Two acceptable handover paths:

**Path A — commit the spec artefacts now (BEFORE leaving this session)**:
```powershell
git add specs/014-package-self-import-resolution/ CLAUDE.md
git commit -m "spec(014): plan, tasks, analyze + top-3 remediations applied (no code yet)"
git push -u origin 014-package-self-import-resolution
```

**Path B — leave uncommitted; the new session inherits the working tree (lower commit-discipline cost; only safe if no other concurrent Claude session might `git reset` the working tree)**.

Recommendation: Path A. Single small commit; restart-safe.

The `.specify/feature.json` modification is unrelated drift from the speckit-specify run that scaffolded this branch; safe to include in the same commit OR drop with `git restore .specify/feature.json` if Gabi prefers a clean slate.

## Phases

- [x] 1. /speckit-plan
- [x] 2. /speckit-tasks
- [x] 3. /speckit-analyze + top-3 remediations applied
- [ ] 4. /speckit-implement — Phase 1 Setup (T001-T004)
- [ ] 5. /speckit-implement — Phase 3 US1 (T005-T019)
- [ ] 6. /speckit-implement — Phase 4 US2 (T020-T023)
- [ ] 7. /speckit-implement — Phase 5 Polish + tombstone refresh (T024-T031)
- [ ] 8. PR open + review + merge to `main` + same-day CalVer tag

## Resume sequence (next session)

1. Read this file.
2. Read `CLAUDE.md`, `docs/DISCIPLINE.md`, `docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` (mandatory per CLAUDE.md start-of-conversation protocol).
3. Skim memories: `project_012_codeconv_runner_status.md`, `project_012_sibling_lock_path.md`, `project_pglite_cold_init_windows.md`, `reference_d2net_uses_public_schema.md`.
4. Read `specs/014-package-self-import-resolution/spec.md` end-to-end.
5. Read `specs/014-package-self-import-resolution/plan.md`, then `tasks.md`, then both `contracts/*.md`.
6. Run `/speckit-implement` (or proceed task-by-task without the wrapper if Gabi prefers manual control).

## Implementation cautions

- **Tests-first** (T005-T012, T020) — write each test, run it, confirm it FAILS against the unmodified code, THEN implement.
- **`--data-dir` override required** on this Windows + exFAT checkout (memory `project_pglite_cold_init_windows.md` and `docs/known-issues.md` Issue 8). All e2e tests in T012 / T020 / T024-T028 must pass `--data-dir`.
- **`--test-concurrency=1`** for all `pytest` invocations (PGLite cold-init ~7s; concurrent tests deadlock).
- **Tombstone refresh commit MUST be the last thing**. Don't let interim test runs leak into the staged tombstone diff.
- **No commits beyond the two enumerated in the plan**. The bigger spec-only commit (this session's handover) is its own commit; the code commit and the tombstone refresh commit are the two from `tasks.md`.
- **Spec-First**: if any test reveals a behaviour gap not in the spec/contracts, STOP and amend the spec before fixing the test. Per CLAUDE.md.

## Open issues / known traps

- F4 from analyze (mid-run pubspec mutation): structurally guaranteed by the contract (single read at workflow entry); no test added. If the next session finds a reason to add `test_pubspec_cached_within_run`, file it under T011 with a `[P]` marker.
- T028's `--truncate-codeconv` flag may not exist; T028 itself includes the verify-or-fallback step. Don't blindly run the flag.
- The `_scan_outside_callers` rewrite (T021) MUST run BEFORE the existing `target.startswith(("package:", "dart:", "dart-ext:"))` skip. Order matters.

## Files written this session (all uncommitted)

- `specs/014-package-self-import-resolution/plan.md`
- `specs/014-package-self-import-resolution/research.md`
- `specs/014-package-self-import-resolution/data-model.md`
- `specs/014-package-self-import-resolution/quickstart.md`
- `specs/014-package-self-import-resolution/tasks.md`
- `specs/014-package-self-import-resolution/contracts/parser_contract.md`
- `specs/014-package-self-import-resolution/contracts/workflow_contract.md`
- `CLAUDE.md` (one-line SPECKIT marker edit)
- `docs/current_plan.md` (this file)

`spec.md` and `checklists/requirements.md` were written by `/speckit-specify` previously and are also uncommitted on the branch.
