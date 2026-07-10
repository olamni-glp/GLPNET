# Baseline record — 050-full-gleam-combined

## T003 — reference-suite baseline (2026-07-10)

**Dart REPL suite** (`DART=/c/Users/gavri/dart-sdk/bin/dart.exe bash test/run_all_tests.sh`):

- **Total 529 | Passed 528 | Failed 1**
- The single failure is PRE-EXISTING, in Section Q (AOT REPL exe regression smoke), example ex-01:
  the pre-built AOT `glp_repl.exe` emitted only "Loaded root self.glp … Goodbye!" with no example
  output (1 of 9 AOT checks; ex-02/ex-03 pass all checks). Sections A–P and R are fully green,
  including all typed-runtime, negative, module, bonds, and cluster sections.
- Not caused by feature-050 work (no `glp_runtime/` changes on this branch). Suspected stale
  pre-built AOT binary vs current source — REPORTED per Bug Protocol / DISCIPLINE §2.3; to be
  investigated outside the Gleam port. The 528-green set is the working baseline; corpus-parity
  recording (T037) will pin its manifest against sections that are green here.

**Dart↔C# cross-runtime link rig** (`bash test/link/run_link_tests_cross.sh`, 2026-07-10):

- **PASS 16 | FAIL 0** — all 8 cases green in both directions (D→C and C→D):
  pc_integers, pc_strings, pc_terms, link_send_wrapper, link_recv_chain, bidirectional,
  path_b_request_accept, monitor_close.
- Run against a freshly rebuilt C# REPL (`cd out/csharp/glp_repl && dotnet build` — 0 errors;
  148 pre-existing CS8669 warnings in generated code, unchanged).

## Phase 1 verification status

- T001: `gleam build --target erlang` green (gleam 1.17.0, native Windows).
- T004: both Lean scaffolds VERIFIED (`glp_gleam/lean/{WriterMguBindsOnlyWriters,DistDerefConvergence}`,
  toolchain pin v4.30.0) — `lake build` "Build completed successfully" for both (2026-07-10).
- T005: WSL `gleam test` VERIFIED — **91 passed, no failures** (2026-07-10).
  - ⚠️ Operational note: the first run aborted with `ModuleNotFound: glp@codec@canonical_order_test`
    because `glp_gleam/build/` held mixed artifacts from the native-Windows `gleam build` (T001) and
    WSL gleam. Fix: delete the gitignored `glp_gleam/build/` and re-run. Convention going forward:
    `gleam test` runs under WSL only; after any native-Windows gleam invocation, clean `build/`
    before a WSL run (and vice versa).

## T030 — US1 smoke-set outcomes

(to be recorded when US1 completes)
