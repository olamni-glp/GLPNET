# Feature 077 (guarded-term-traversal) — Safe-Restart Handover

**Date:** 2026-08-11
**Author:** Claude (Opus 4.8) session, host Olamnit
**Status:** ✅ **IMPLEMENT COMPLETE (session 4)** — all 27 tasks done, 4 phase commits on branch `077-guarded-term-traversal` (LOCAL-UNPUSHED). NEXT = `/bk-codexreview` → `/bk-ship`+release → `/bk-close`.

---

## Session-4 update — /bk-implement COMPLETE

All 27 tasks in `tasks.md` are done. Committed in 4 phase checkpoints (each behind a full-suite parity gate):

1. `796acfcf` — **Phase 2 (US3 dedup)**: consolidated the PE/analyzer unify/substitution/resolve machinery into ONE shared module `out/csharp/lib/compiler/term_traversal.cs` (`TermTraversal`). All 5 divergences PRESERVED via parameters (not flattened): #1/#3 anonymous-var (`AnalyzerIsAnonymous` = `StartsWith('_')` vs `PeIsUnderscore` = `== "_"`), #2 fresh-var prefix + `long` counter, #5 `IReadOnlyDictionary` return; #4 `TransformClause` orchestration kept in the owning classes. REPL 547/547.
2. `70824192` — **Phase 3 (var-name guard)**: `ApplySubstitution` (the CONFIRMED F-069-1 crash site — `PartialEvaluator.ApplySubstitution`, since PE runs before the analyzer) now carries a var-name active-path set; a self-referential binding raises a catchable `CompileError` (`CyclicTermError`, FR-004) instead of the uncatchable `StackOverflow`. `error.cs`: `term_traversal`→Semantic mapping (T023). Section T added. REPL 549/549.
3. `1c3b4eba` — **Phase 4 (structural guard)**: `StructuralGuard` (identity active-path + fuel backstop) wired to the 6 codegen walkers + 3 analyzer mark/ground walkers (reentrancy-field) + PE `IsGround` + linker `ResolveGoal` (threaded-param). New console probe `out/csharp/term_traversal_probe` (InternalsVisibleTo, in the sln) proves cyclic→CompileError + deep(6000)/DAG→OK. REPL 550/550.
4. `3920a5e7` — **Phase 5 (finalize)**: Section T generalized over every `programs/tests/cyclic/cyclic_*.glp` (T024, demonstrates the whole cyclic-`=` CLASS); `spec.md` Status→Implemented; `docs/known-issues.md` Issue 0 resolved + cross-ref to the occurs-check feature.

**Final gates:** REPL Section T 5/5 PASS (cyclic_eq + cyclic_list → `Cyclic term detected` diagnostic, no overflow; probe → CompileError + deep/DAG OK); full `dotnet build` sln 0 errors. Full suite **551/552** — the single failure is the DOCUMENTED ~1/6 **Gleam×C# cross-runtime rendezvous-timing flake** (`bidirectional`/`link_both_ways`; toggles run-to-run; it also failed in the PRISTINE pre-change baseline; orthogonal to this compiler-only feature per FR-008). Not a regression.

**🔴 Two carry-over flags for `/bk-codexreview`:**
1. **Divergence-#1 owner review** — the anonymous-var semantics (`StartsWith('_')` vs `== "_"`) are preserved as two behaviours via the `isAnonymous` param, NOT unified. Any future unification is a separate owner-approved decision (§II no-silent-behaviour-change).
2. **Test-reality adaptation** — there is NO xUnit harness for the `out/csharp` compiler tree (the `test/*.cs` are Dart mirror-stubs; the compiler is regression-tested through the REPL suite Section I, exactly as feature 069's T022 established). The tasks' xUnit test items (T002/T016/T021/T024) were satisfied through the reality-matching channels: durable `.glp` repro programs + REPL Section T (end-to-end, the F-069-1 path) + a dependency-free console probe (the structural family, which a GLP program cannot express). This is a documented adaptation, not a skip — every SC has a concrete automated check.

**Push status:** the 4 commits are LOCAL (push not yet authorized per CLAUDE.md "push only when the user asks"). Pushing the branch is the recommended safe-restart checkpoint before codexreview.

---

## (Original session-3 handover below)
**Status:** Pipeline specify→analyze COMPLETE; NEXT = `/bk-implement` in a NEW session

---

## Summary

This session (1) scored roadmap Features A & B and signed off; (2) **shipped feature 069 as `v2026.08.11.1`** (DECISION.md ratified); (3) drove roadmap **Feature B** (`guarded-term-traversal-utilities...`) through the full pre-implement pipeline as **spec `077-guarded-term-traversal`** — specify→clarify→plan→tasks→analyze all COMPLETE; (4) synced the roadmap (export 19/102/2879) and committed/pushed. All work is durable (DBOS pipeline state + branch pushed). Nothing lives only in this session.

## Where things stand (objective checks)

- `git branch --show-current` = `077-guarded-term-traversal`, HEAD `5c7e504e`, **clean, 0 ahead / 0 behind origin** (pushed).
- `buildkit-builder status` → active feature `077-guarded-term-traversal`, stages specify/clarify/plan/tasks/analyze all **complete**, implement **not_started**.
- Marathon run `mrun-08079310f325` open for 077 (tracks the arc; divergence-#1 owner-review flag captured).
- Roadmap: Features A & B `refined` under epic `epic-glp-compiler-robustness-occurs-check-term-traversal-hardening`; hard dep B→A.
- 069 SHIPPED `v2026.08.11.1` (PRs #147/#148/#149 merged, tag present); develop @ `4328ff01` (has the 069 back-merge) before 077 branched.

## The feature (077) in one paragraph

Two coupled moves, both mandated by resolved 3rtask escalations: **(1) dedup NOW** — consolidate the DUPLICATED unify/substitution/resolve machinery in `analyzer.cs` (`DefinedGuardEvaluator`) and `partial_evaluator.cs` (`PartialEvaluator`) into ONE shared module (`out/csharp/lib/compiler/term_traversal.cs`, new); **(2) guard every recursive Term walker** (~21 unguarded, re-verified) so a cyclic `Term` raises a catchable `CompileError` instead of an uncatchable `StackOverflowException`. Dedup is foundational (Phase 2) because the guard lands ON the consolidated module. Closes F-069-1's crash class and unblocks the sibling occurs-check feature (A).

## Key decisions locked (do NOT relitigate)

- **FR-004** (clarify): cyclic outcome = **hard-fail raising `CompileError`** (`error.cs:13`), NOT return-revisited-node. No §1.14 gate (compiler-behaviour, not language change — FR-007).
- **Walker count**: ~21 unguarded (analyzer 8 + PE 6 + codegen 6 + linker 1), plus 2 already-guarded `ResolveTerm` (visited-set keyed on `VarTerm.Name`). Full inventory with line numbers in `specs/077-guarded-term-traversal/research.md` Decision 1.
- **Guard strategy** (research Decision 2): substitution/resolve family → var-name visited-set (extend existing `ResolveTerm`); structural family (codegen/linker + AST mark/ground walkers) → fuel/identity bound.
- **5 consolidation divergences** (research Decision 3) — the copies are NOT identical. Divergence #1 (anonymous-var: `StartsWith('_')` vs `== "_"`) is **behaviour-sensitive** → plan preserves BOTH via an `isAnonymous` parameter; DO NOT flatten (that is the §II workaround trap). **Owner review flagged** for implement.
- **Analyze remediations applied**: F1 = mark/ground walkers (`_ExtractAndMarkGroundedVars`, `_MarkVarsInTermAsTypeGrounded`, `_AnalyzeTerm`, PE `IsGround`) go through the STRUCTURAL guard (T019a, Phase 4), not the var-name guard. A1 = fuel sized from max legitimate corpus term-depth × safety.

## NEXT (new session, in order)

1. **`/bk-implement`** on branch `077-guarded-term-traversal`. Execute `specs/077-guarded-term-traversal/tasks.md` (27 tasks). MVP = Phase 1+2+3 (dedup + substitution-family guard) closes F-069-1; Phase 4 completes structural coverage; Phase 5 polish. **Baseline the REPL suite green first** (`DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe bash test/run_all_tests.sh` → 547/547) and after each phase. Every phase ends green + committed (safe restart points).
2. After implement + **safe restart** → **`/bk-codexreview`**.
3. **`/bk-ship`** + release Feature B (via `buildkit-ship.exe --skip-preflight` from `D:/bstdev/research/buildkit/.venv313/Scripts/`; run suites yourself first).
4. **`/bk-close`** Feature B (retro + action reconcile).
5. Then Feature A (occurs-check) — §1.14 propose-first with Udi — lands its single change on B's consolidated module.

## Environment quirks (this host, Olamnit)

- dart at `C:/src/flutter/bin/cache/dart-sdk/bin/dart.exe` (3.11.5); export `DART=` before `test/run_all_tests.sh`; delete stale `glp_runtime/.dart_tool/repl.dill` if the suite misbehaves.
- buildkit exes via `D:/bstdev/research/buildkit/.venv313/Scripts/*.exe`; CLI modules via `python -m buildkit_cli.*` with `PYTHONUTF8=1`.
- **PGlite bridge flakiness**: marathon/roadmap calls occasionally hang ~2min on `pgdb/pglite` bridge contention ("reaped orphaned bridge" is benign). Run marathon/roadmap calls with a bounded timeout; retry once. Sidecar + git are unaffected.
- Ship reconciliation: a stale `implement=in_progress` stage is completed via `python -c "from buildkit_cli.agent.run import run; run('<feat>', source='human')"` (drives ONLY the DBOS stage transition, no task re-run) — NOT `--force`.
- Ship catch-up merges: develop may be many commits ahead; the `.specify/roadmap-sync/.import-manifest.json` conflict resolves via `git checkout --theirs` + `roadmap import --rebuild-manifest` (PGLite is authoritative).

## Files changed this session (077 pipeline + roadmap)

- `specs/077-guarded-term-traversal/` — spec.md, research.md, plan.md, data-model.md, quickstart.md, contracts/guarded-traversal.md, tasks.md, checklists/requirements.md
- `.specify/feature.json` → 077; `CLAUDE.md` BUILDKIT block → 077 plan
- `.specify/roadmap-sync/` — export `olamnit__glpnet__20260811T170205Z.json` + manifest
- (069 ship: merge + roadmap-manifest reconcile on the 069 branch, now merged)
