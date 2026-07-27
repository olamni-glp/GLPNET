//// T088 item 3 — an UNKNOWN guard predicate emits a `[WARN] Unknown guard
//// predicate: <name>` diagnostic (parity with Dart runner.dart:5284 and the C#
//// REPL), instead of silently failing. Backs `close-runtime-arithmetic-expression`
//// detail_ids `suspension-diagnostics` / `system-predicate-registry`.
////
//// The warning is emitted during guard evaluation on a clause that then FAILS, so
//// it must survive the non-committing path: `Failed`/`Suspended`/`BudgetExhausted`
//// now carry `output` (like `Reduced`), and `clear_clause` preserves it, so the
//// line reaches the engine's captured output even though the goal fails. This is
//// exactly the `test_time_guard.glp` differential case (Dart/C#/Gleam now AGREE).

import gleam/list
import gleam/string
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm}

// `time/1` is not a builtin guard, so it takes the unknown → FAIL + `[WARN]` arm.
// The head is a bare variable so it always matches the ground goal arg, guaranteeing
// the guard is reached (a reader-in-head clause could soft-fail at head unification
// before the guard, which would not exercise the diagnostic).
const src = "t(X) :- time(X?) | true."

pub fn unknown_guard_predicate_emits_warning_and_fails_test() {
  let assert Ok(outcome) = loader.load(src, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "t/1")

  // Goal `t(a)` with a ground argument — head `t(X)` binds X = a, guard `time(X?)`
  // resolves X? to `a` (ground) and hits the unknown-predicate arm.
  let regs =
    program.new_regs() |> program.set_reg(0, ConstTerm(ConstAtom("a")))
  let engine = scheduler.new(prog, heap.new())
  let #(engine, _id) = scheduler.boot(engine, "t/1", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 1000)

  // Unknown guard → the clause fails, and (only clause) the goal fails.
  status |> should.equal(scheduler.Failed)

  // ... AND the `[WARN]` line was emitted and SURVIVED the failing path.
  let warned =
    scheduler.captured_output(engine)
    |> list.any(fn(line) {
      string.contains(line, "[WARN] Unknown guard predicate: time")
    })
  warned |> should.be_true
}
