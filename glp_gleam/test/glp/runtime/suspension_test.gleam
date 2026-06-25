//// US3 (T019) — suspension storage + activation/forwarding (FR-008). Cross-validated
//// against the Dart source-of-truth tests glp_runtime/test/heap/{suspension,binding}_
//// pointer_test.dart (see specs/.../parity-evidence.md). Dart source of truth:
//// glp_runtime/lib/runtime/heap_fcp.dart suspendOnWriter / _walkAndActivate /
//// _forwardSuspensions.

import gleeunit/should
import glp/runtime/heap.{NotAWriter}
import glp/runtime/suspension.{GoalRef, Suspension}
import glp/runtime/terms.{ConstInt, ConstTerm}

// suspend on an unbound writer → binding the writer fires the armed suspension as a GoalRef
// (← suspension_pointer_test.dart "On wake, activation pc equals kappa": g=77, pc=1).
pub fn suspend_then_activate_test() {
  let #(h1, w, _r) = heap.allocate_variable(heap.new())
  let s = Suspension(goal_id: 77, resume_pc: 1, armed: True)
  let assert Ok(h2) = heap.suspend_on_writer(h1, w, s)
  let assert Ok(#(_h3, activations)) =
    heap.bind_writer(h2, w, ConstTerm(ConstInt(1)))
  activations |> should.equal([GoalRef(goal_id: 77, resume_pc: 1)])
}

// multiple suspensions all activate; newest-first list order (← "Multiple suspensions on
// same variable all activate"). Suspensions prepend, so the activation order is reverse.
pub fn multiple_suspensions_activate_test() {
  let #(h1, w, _r) = heap.allocate_variable(heap.new())
  let assert Ok(h2) =
    heap.suspend_on_writer(h1, w, Suspension(goal_id: 10, resume_pc: 100, armed: True))
  let assert Ok(h3) =
    heap.suspend_on_writer(h2, w, Suspension(goal_id: 20, resume_pc: 200, armed: True))
  let assert Ok(#(_h4, activations)) =
    heap.bind_writer(h3, w, ConstTerm(ConstInt(0)))
  activations
  |> should.equal([
    GoalRef(goal_id: 20, resume_pc: 200),
    GoalRef(goal_id: 10, resume_pc: 100),
  ])
}

// a disarmed suspension produces no activation (the double-activation guard;
// ← "Disarmed suspensions do not activate").
pub fn disarmed_not_activated_test() {
  let #(h1, w, _r) = heap.allocate_variable(heap.new())
  let assert Ok(h2) =
    heap.suspend_on_writer(h1, w, Suspension(goal_id: 3, resume_pc: 4, armed: False))
  let assert Ok(#(_h3, activations)) =
    heap.bind_writer(h2, w, ConstTerm(ConstInt(0)))
  activations |> should.equal([])
}

// bind-to-variable forwards the armed suspension to the target writer and fires nothing
// yet; binding the TARGET then fires it (← "Suspension forwarding when binding to another
// variable": record(42, 500)).
pub fn forward_on_var_bind_test() {
  let #(h1, w1, _r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let s = Suspension(goal_id: 42, resume_pc: 500, armed: True)
  let assert Ok(h3) = heap.suspend_on_writer(h2, w1, s)

  // forwarding fires nothing, returns []
  let assert Ok(#(h4, [])) = heap.bind_writer_to_var(h3, w1, r2)

  // binding the target writer now fires the forwarded suspension
  let assert Ok(#(_h5, activations)) =
    heap.bind_writer(h4, w2, ConstTerm(ConstInt(9)))
  activations |> should.equal([GoalRef(goal_id: 42, resume_pc: 500)])
}

// a chain of variable bindings forwards the suspension all the way to the terminal writer
// (← "Chain of variable bindings forwards suspensions correctly": record(99, 999)).
pub fn forward_through_chain_test() {
  let #(h1, w1, _r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let #(h3, w3, r3) = heap.allocate_variable(h2)
  let s = Suspension(goal_id: 99, resume_pc: 999, armed: True)
  let assert Ok(h4) = heap.suspend_on_writer(h3, w1, s)
  let assert Ok(#(h5, [])) = heap.bind_writer_to_var(h4, w1, r2)
  let assert Ok(#(h6, [])) = heap.bind_writer_to_var(h5, w2, r3)
  let assert Ok(#(_h7, activations)) =
    heap.bind_writer(h6, w3, ConstTerm(ConstInt(7)))
  activations |> should.equal([GoalRef(goal_id: 99, resume_pc: 999)])
}

// suspending on a non-writer (a reader) is reported loudly (FR-008).
pub fn suspend_on_non_writer_errors_test() {
  let #(h1, _w, r) = heap.allocate_variable(heap.new())
  heap.suspend_on_writer(h1, r, Suspension(goal_id: 1, resume_pc: 1, armed: True))
  |> should.equal(Error(NotAWriter(r)))
}

// FR-008 regression (forward-to-chained-target): forwarding onto a target whose immediate
// paired writer is ALREADY bound onward (`WriterBound`) must ride to the chain's TERMINAL
// writer, not silently drop. Chain wb→rc first; suspend on wa; bind wa→rb (rb's immediate
// paired writer wb is already WriterBound, terminal = wc); binding wc fires the suspension.
pub fn forward_onto_chained_target_fires_test() {
  let #(h1, wa, _ra) = heap.allocate_variable(heap.new())
  let #(h2, wb, rb) = heap.allocate_variable(h1)
  let #(h3, wc, rc) = heap.allocate_variable(h2)
  let assert Ok(#(h4, [])) = heap.bind_writer_to_var(h3, wb, rc)
  let assert Ok(h5) =
    heap.suspend_on_writer(h4, wa, Suspension(goal_id: 7, resume_pc: 3, armed: True))
  let assert Ok(#(h6, [])) = heap.bind_writer_to_var(h5, wa, rb)
  let assert Ok(#(_h7, activations)) =
    heap.bind_writer(h6, wc, ConstTerm(ConstInt(0)))
  activations |> should.equal([GoalRef(goal_id: 7, resume_pc: 3)])
}
