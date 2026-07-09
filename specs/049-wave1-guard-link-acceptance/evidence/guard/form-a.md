# EquivalenceRun — form (a), the (a1) runtime-defined guard

- **form**: a (realized via the T003-ruled (a1) compiler extension)
- **suite_commit**: `7884fbbb` (feat(049): T009+T010 form (a) via ruled a1 runtime-defined guards)
- **host**: gavri (delegated continuation of US1 per Gabi's /bk-implement directive 2026-07-09)
- **date**: 2026-07-09

## SC-002 — worked examples wx1–wx4 under form (a)
- **Criterion**: SC-002 (four proposal worked-example outcomes in the REPL)
- **Host(s)**: gavri
- **Command**: `echo -e 'load programs/tests/typed/policy_guard_worked.glp\ntest_wx1.\ntest_wx2.\ntest_wx3.\ntest_wx4(Rwx4?).\n:quit' | dart run glp_runtime/bin/glp_repl.dart`
- **Output**: `→ succeeds`, `→ failed`, `→ failed`, `→ suspended` (in goal order)
- **Verdict**: PASS (Success / Fail / Fail / Suspend — exactly the proposal outcomes)
- **Date**: 2026-07-09

## worked_examples outcome map (form a)

| id | expected | observed |
|----|----------|----------|
| wx1 | success | succeeds ✓ |
| wx2 | fail | failed ✓ |
| wx3 | fail | failed ✓ |
| wx4 | suspend | suspended ✓ (step-limited, not a hang — R6) |

## vectors outcome map (form a) — all 12 vectors.json entries

- **Command**: `echo -e 'load programs/tests/typed/policy_guard_vectors.glp\n<12 goals>\n:quit' | dart run glp_runtime/bin/glp_repl.dart` (goals: `test_wx1.` … `test_v12(Rv12?).` per the file's header comments)

| id | expected_guard | observed |
|----|----------------|----------|
| wx1 | success | succeeds ✓ |
| wx2 | fail | failed ✓ |
| wx3 | fail | failed ✓ |
| wx4 | suspend | suspended ✓ |
| v05 | success (T003-ruled: vacuous empty targets) | succeeds ✓ |
| v06 | fail | failed ✓ |
| v07 | success | succeeds ✓ |
| v08 | success (waypoints advisory) | succeeds ✓ |
| v09 | fail | failed ✓ |
| v10 | fail | failed ✓ |
| v11 | suspend (needed prefix unbound) | suspended ✓ |
| v12 | fail (T003-ruled: exclusion decidable without reachability — fail dominates suspend) | failed ✓ |

- **Verdict**: PASS — 12/12 outcome map matches vectors.json `expected_guard`; zero silent
  fallbacks (suspend cases suspend; fail cases print `→ failed` loudly)

## repl_baseline
- Pre-change baseline on this host: **524/525** (single failure = the pre-existing Section Q
  AOT-smoke case; scratchpad `baseline-repl.txt`).
- Post-change full-suite run (with new A29 block: worked + vectors ordered-outcome asserts):
  recorded in the follow-up entry below once the run completes.

## Realization notes (deviations recorded, no semantics changed)

1. **(a1) seat** (all additive, constitution IV-b): `partial_evaluator.dart` passes test-only
   procedures through in guard position (admission: every clause body empty/`true`, guards ⊆
   {ground/1, known/1, =?=/2} ∪ recursively test-only, defined-guard calls never negated);
   `codegen.dart` emits a `definedGuards` clause-spec side table into `BytecodeProgram`;
   `runner.dart` evaluates the specs three-valued at the Guard opcode BEFORE the blanket
   unbound-reader pre-suspend (fail dominates suspend within a clause's guard conjunction —
   this is what makes v12 = fail); `glp_engine.dart` merges the side table across loaded
   programs. New file: `glp_runtime/lib/bytecode/guard_defs.dart`.
2. **Type-lawful [] coverage**: the T007/T008-era clause sets left the `[]` alternative of
   `intersects`/`in_list` uncovered (contravariance error). Added decidable-fail arms
   `intersects([], _) :- ~ground(a) | true.` and `in_list(_, []) :- ~ground(a) | true.`
   (`~ground` on a constant never holds). Note: `a =?= b` with constant operands does NOT
   parse in guard position — recorded as a parser observation, worked around only in the
   sense of choosing an equivalent expressible guard.
3. **sink/1 removed**: `sink(_)` had an anonymous writer at a produce position (mode error).
   Suspend cases now receive the unbound reader as a goal argument (`test_wx4(Rwx4?).`),
   matching the suite's established idiom (A24 `bob(Xbob?).`).
4. **Pre-existing codegen gap (flagged for Gabi, NOT fixed here — out of task scope)**:
   NAMED anonymous variables (`_W`, `_Ts`, …) inside head structures fail codegen with
   `[codegen] Undefined variable`, although typed-glp-manual §9.3 endorses them. Plain `_`
   compiles fine; the three guard files now use plain `_`. Repro:
   `never([_X|_Xs]) :- ~ground(b) | true.` → `[codegen] Undefined variable: _X`.
