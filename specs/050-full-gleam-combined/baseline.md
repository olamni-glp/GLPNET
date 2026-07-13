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

## T030 — US1 smoke-set outcomes (2026-07-13)

US1 engine facade (T029) delivered in three slices on branch `050-full-gleam-combined`:
Slice 0 scheduler refinement `13312dfb`, Slice 1 facade `8f5b7766`, Slice 2 goal-boot + run
`0f3817c4`. Native gleam **377 / 377**, warning-free. Slice 0b (output capture) deferred per
R4 (`captured` excluded from byte-parity; no live Dart producer of `buildResultEnvelope`) —
every envelope carries `captured = <<>>`.

Smoke set run through the engine value API (`engine.new()/new_with_prelude()/load()/run()`):

| Case | Goal / source | Gleam outcome | Dart oracle | Agree |
|---|---|---|---|---|
| Arithmetic (headline) | `X := 2+3` (prelude-only, real on-disk self.glp) | Success, `X = ConstInt(5)`, no var→writer, no suspended | `X = 5 → succeeds` (Dart REPL, 2026-07-13) | ✔ |
| Suspension | `flip(In?, Out)` (In unbound) | Suspended, no bindings, `Out` → var→writer, exactly 1 blocking reader | (structural — heap addrs not pinned, FR-009) | ✔ (shape) |
| SRSW negative | `dup(X,X,X)` at a union type | rejected at **load** — `StagedError{SrswStage, SrswViolation}` | reference section D shape | ✔ |
| Type negative | `f(a,a)` producing a `U` from a `T` | rejected at **load** — `StagedError{TypeCheckStage, TypeError}` | reference section C shape | ✔ |
| Unknown predicate | `no_such_pred(1,2)` | Failed envelope, `error = Some(...)` | (Dart failed) | ✔ |

The headline arithmetic case is **verified byte-for-value identical against the Dart REPL**
(`X := 2+3.` → `X = 5`). The exact blocking-reader address for the suspension case is pinned at
the scheduler layer (`scheduler_test.suspended_boot_reports_blocking_readers_test` = `[in_reader]`);
at the envelope layer only the shape (count/roles) is asserted because heap addresses are excluded
from parity (FR-009). `step`/`Event` (REPL `:trace` seam) is deferred to the US2 REPL slice; the
faithful single-step primitive it wraps (`scheduler.step`) is delivered + tested in Slice 0.
