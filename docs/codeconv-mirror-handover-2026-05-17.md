# codeconv mirror (feature 016 / spec Amendment 1) — Handover

**Date:** 2026-05-17
**Author:** Claude Code session (run from the GLPNET 015 worktree, all work in **GLPNET-016**)
**Status:** COMPLETE & GREEN. Live e2e GREEN end-to-end (init→mirror→discover→depgraph→scaffold). Issue #1 RESOLVED. Part B implemented. Pure unit 32 ✓; bridged regression 35 ✓ + 1 `bridge unreachable` cold-init flake (isolation-green, 1 passed/11.7s — known-environmental, not a regression). Only open item: Gabi's cross-branch commit/merge decision on `016-codeconv-init-scaffold-langpair`.

---

## Summary

Added a new `codeconv mirror` stage (feature-016 spec Amendment 1) that reproduces spec
`001-d2net-scaffold` generically via the language-pair registry, producing the inventory
subtree (`glp_runtime_net/`) from the source-language tree (`glp_runtime/`) so the
`init → mirror → discover → depgraph → scaffold` pipeline can run on a fresh worktree.
Owner-approved decisions: distinct stage (1b), pair set solely via `codeconv init`,
`init` defers inventory when the configured source is absent, and **Option 1** for the
`.dart` vs `.dart.src` contradiction (mirror keeps source verbatim as `.dart` so
`discover` inventories it; still emits the 9 companions + tracker — the single documented
spec-001 FR-004 deviation, FR-032).

A pre-existing bug was fixed en route: `tools/init/workflow.py run_inspect` deadlocked
(nested `engine.connect()` on a `pool_size=1` engine) — `_read_settings` hoisted out of
the outer connection.

---

## Completed work (all in `D:\BSTDEV\research\GLP\GLPNET-016`, branch `016-codeconv-init-scaffold-langpair`)

- **Spec:** `specs/016-codeconv-init-scaffold-langpair/spec.md` Amendment 1 (US6,
  FR-027..FR-041, amended FR-009/FR-012, FR-032 Option-1 deviation, `mirror_source_root`/
  FR-029); new `contracts/codeconv_mirror_cli.md`; extended
  `contracts/langpair_plugin_contract.md`; plan.md/tasks.md amendment deltas.
- **Langpair:** `langpairs/base.py` (+5 mirror hooks), new
  `langpairs/dart_csharp/mirror_dart.py` (`preserved_source_suffix()==""` — Option 1;
  prune `.dart_tool build .git .idea .vscode`; 9 companions; `d2net-tracker.json`;
  `// TODO:` stub), wired in `langpairs/dart_csharp/__init__.py`.
- **Tool:** new `codeconv/src/codeconv/tools/mirror/{__init__.py,workflow.py}` —
  auto-discovered (no registry edit; SC-011).
- **init:** `tools/init/{__init__.py,workflow.py}` — `--mirror-source`/`mirror_source_root`;
  configured source validated `must_exist=False`; inventory deferred (warning, no
  hard-fail) when source absent; `run_inspect` pool-deadlock fix.
- **Skill:** `.claude/skills/codeconv-mirror/SKILL.md`.
- **Tests:** new `codeconv/tests/test_mirror_hooks.py`, `test_mirror.py`, `test_mirror_gitignore.py`.
- **Issue #1 fix:** restored feature-015 option-A' dangling-edge filter in
  `tools/depgraph/workflow.py` (filter `dart_imports` edges to inventoried nodes before
  `compute`; surfaced as `dangling_edges_filtered`).
- **Part B (FR-042/FR-043):** `init` `--include-pruned` (force-include a standard-pruned
  dir) + `--mirror-exclude` (gitignore-style, repeatable) → workspace settings
  `mirror_force_include` / `mirror_exclude_patterns`; `mirror` computes effective prune =
  standard − force-include + gitignore matches via new internal
  `tools/mirror/gitignore.py` (no new dependency).

## Test status

| Suite | Result |
|---|---|
| `test_mirror_hooks.py` (+`test_langpair_registry.py`) | 23 passed |
| `test_mirror.py` (integration + full chain) | 7 passed (80s) |
| Regression: init/scaffold/pipeline/langpair/runner | 38 passed; 1 `bridge unreachable` cold-init flake — isolation-green (1 passed/14.9s), NOT a logic regression |
| Live e2e (GLPNET-016, `--data-dir C:/pglite/research/glpnet-016`) | migrate ✓ · init ✓ (deferred) ✓ · mirror ✓ (193 preserved, 1737 companions, 193 tracker, 127 non-source, 52 dirs) · discover ✓ · scaffold ✓ (193) · **depgraph compute ✗ — issue #1** |

## Issue #1 — RESOLVED 2026-05-17

`depgraph compute` raised `ValueError: edge endpoint not in nodes` on the faithful full
mirror. Root cause: 016's `tools/depgraph/workflow.py` passed raw `dart_imports` edges to
the SCC algorithm without the feature-015 **option-A'** self-healing filter (the curated
128-file `glp_runtime_net` had no dangling edges so it was never exercised). Owner
approved two fixes, both applied: (1) extend the dart_csharp standard prune set with
`archive`/`backup` (build already present); (2) restore the option-A' filter in
`depgraph compute`. Verified e2e: `depgraph` exit 0, `files_total=178`,
`dangling_edges_filtered=35`, `cycle_count=1`; full chain green. No open blockers.

## Open decision — commit/merge (Gabi only)

All changes are uncommitted in the `016` worktree. This session ran from the `015`
worktree and **cannot push to `016`**. Gabi decides whether/how to commit on
`016-codeconv-init-scaffold-langpair` and merge. Changed/new files listed above.

## Resume steps (next session)

1. Decide issue #1 (recommend option a); if (a): edit
   `langpairs/dart_csharp/mirror_dart.py` `MIRROR_PRUNE_SEGMENTS`, add a spec note,
   add a regression assertion, re-run live e2e (`migrate→init→mirror→discover→depgraph→scaffold`
   from GLPNET-016 with `--data-dir C:/pglite/research/glpnet-016`).
2. Re-run the full pytest suite (re-run any `bridge unreachable` failure in isolation).
3. Surface the commit diff to Gabi for the cross-branch commit/merge decision.

## Env (load-bearing)

- Worktree `D:\BSTDEV\research\GLP\GLPNET-016`, branch `016-codeconv-init-scaffold-langpair`.
- venv `GLPNET-016/codeconv/.venv`; invoke `codeconv/.venv/Scripts/codeconv.exe`.
- Live cluster `--data-dir C:/pglite/research/glpnet-016` (alembic 0003). Tests use
  `tmp_path` (no override). PGLite cold-init ~7s; first bridge call ≥60s timeout.
- Do NOT clobber `GLPNET-016/docs/current_plan.md` (feature-016's own completed plan).
