# Form-(a1) realization design — runtime-defined guards (T009 working design)

Ruled 2026-07-08 (spec Clarifications): form (a) via **(a1) compiler extension** — "allow a declared
guard procedure with multiple guarded clauses to compile to a runtime guard evaluation reusing the
existing three-valued goal machinery" — then evolve to form (b). This note pins the concrete design
so implementation survives session boundaries. No new syntax/directive is introduced (§1.14 scope:
semantics extension only, as ruled).

## Admission rule (what may be called in guard position)

A user procedure is a **runtime-defined guard** iff every clause is **test-only**: body empty or
`true`, and every guard in its clauses is a builtin guard or (recursively) a runtime-defined guard.
Single-clause no-guard procedures keep the EXISTING §8 compile-time unfolding (unchanged behavior);
the extension only admits what was previously a CompileError.

## Evaluation semantics (the existing three-valued discipline, applied per clause)

Evaluating `g(args…)` in guard position at runtime:
1. For each clause of `g` (fresh local frame): match head patterns against the CALLER's dereferenced
   runtime terms — bindings go only into the clause-local frame (pure test; caller heap untouched).
   Encountering an unbound reader where structure is required → that clause SUSPENDS on that reader.
   Mismatch → clause FAILS.
2. If the head matched, evaluate the clause's guard conjunction **order-independently**: evaluate all
   conjuncts; any FAIL ⇒ clause FAILS (fail dominates suspend within a conjunction — this is what
   makes v12 = fail); else any SUSPEND ⇒ clause suspends (union of readers); else clause SUCCEEDS.
3. Across clauses: any SUCCESS ⇒ guard SUCCESS (committed choice); else any suspended clause ⇒
   SUSPEND on the union of collected readers; else FAIL.

Worked consequences: v12 `satisfiable(policy([bob],[],[bob]), R_unbound)` → clause 2's `disjoint`
conjunct fails ⇒ clause fails ⇒ guard FAILS without needing R. wx4 → `intersects` suspends on R ⇒
SUSPEND. v11 → suspends on the unbound tail reader only.

## Code seats (all additive, IV-b)

| Seat | Change |
|---|---|
| `compiler/partial_evaluator.dart` `_transformClause` | where it now throws `Cannot call "x/n" in guard position` for known non-unit procs: if the callee is test-only (new `_collectTestOnlyProcedures(program)`), PASS THROUGH the guard untouched (codegen's generic fallback already emits `bc.Guard(pred, arity)`) |
| `compiler/analyzer.dart` internal `PartialEvaluator` | same pass-through (it only unfolds unit clauses today; verify it doesn't error on pass-through — its `_transformClause` lacks the non-unit error, so pass-through may already fall out) |
| `compiler/codegen.dart` | new: collect test-only guard procedures reachable from guard position and emit a side table `definedGuards` into `BytecodeProgram` — clause specs encoded with runtime term shapes (see below) |
| `bytecode/guard_defs.dart` (NEW) | neutral clause-spec model: `GuardProcSpec{name, arity, clauses: [GuardClauseSpec{headArgs: List<GTerm>, guards: List<GGuardSpec>}]}` with `GTerm ::= GConst(value) | GVar(name, isReader) | GStruct(functor, args)` (lists as `'.'`/2 + `nil`, matching the runtime rep); `GGuardSpec{predicate, args: List<GTerm>, negated}` |
| `bytecode/runner.dart` `BytecodeProgram` | additive field `definedGuards: Map<String, GuardProcSpec>` (key `name/arity`), merged in `merge()` |
| `bytecode/runner.dart` Guard opcode handler | BEFORE the blanket unbound-reader pre-suspend (which would wrongly suspend v12): if `definedGuards` has the key, run the interpretive evaluator on the deref'd arg terms (VarRefs preserved); SUCCESS → `pc++`; FAIL → `_softFailToNextClause` + `_findNextClauseTry`; SUSPEND(readers) → `_suspendAndFailMulti(cx, readers, pc)` |
| runner interpretive evaluator (NEW, ~200 lines) | head match (GConst vs ConstTerm value; GStruct vs StructTerm recursing; GVar writer binds local frame; deref through `cx.rt.heap` reader/value chains collecting unbound-reader addrs); guard eval: builtin subset `ground/1`, `known/1`, `=?=/2` (+negated) delegated to existing helpers/semantics + recursive defined-guard calls (Dart recursion); conjunction = evaluate-all, fail-dominates |

Builtin subset note: `policy_guard.glp` uses only `ground/1`, `=?=/2` (negated once) + recursion, so
the evaluator's builtin table starts with exactly {ground, known, =?=}; anything else inside a
test-only clause is rejected at compile time by the admission rule (kept conservative).

## Form (b) afterwards (T011)

`satisfiable/2` becomes a system guard primitive: registration in `analyzer.dart` guard tables +
native three-valued evaluation in `runner.dart` `_evaluateGuard`-adjacent dispatch (bypassing the
defined-guard table), with the SAME clauses-as-spec semantics. Equivalence = identical outcome maps
on vectors.json + wx1–wx4 under both forms (SC-009); form (a) is the reference.

## Test wiring (T010)

`programs/tests/typed/policy_guard_worked.glp` (wx1–wx4) + `policy_guard_vectors.glp` (all 12) into
`test/run_all_tests.sh` Section A; suspend cases assert `→ suspended` under the step limit (R6).
