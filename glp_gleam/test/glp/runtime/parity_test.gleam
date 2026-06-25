//// US3 (T020) — Dart-derived observable-outcome parity corpus (SC-005 / FR-009 / R-010).
////
//// A fixed set of micro-scenarios, each asserting an OBSERVABLE outcome (deref result ·
//// unify verdict · activation set) against the value the Dart source-of-truth produces
//// for the same scenario. Expected values are baked in (hermetic — no Dart dependency at
//// test time). Internal heap layout is NEVER asserted. Each scenario is cross-validated in
//// specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md (row #N cited per test).
//// Dart source of truth: glp_runtime/lib/runtime/{terms,heap_fcp,suspension}.dart.

import gleeunit/should
import glp/runtime/heap.{Bound, Cycle, Unbound, WriterToWriter}
import glp/runtime/suspension.{GoalRef, Suspension}
import glp/runtime/terms.{ConstInt, ConstTerm, StructTerm, VarRef}
import glp/runtime/unify.{Fail, Success, Suspend, unify}

// #1 — allocate → a fresh variable derefs to its unbound writer.
pub fn parity_allocate_deref_unbound_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(#(_, dr)) = heap.deref(h1, r)
  dr |> should.equal(Unbound(w))
}

// #2 — bind-to-value (int) → the reader derefs to the value.
pub fn parity_bind_value_int_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(#(h2, _)) = heap.bind_writer(h1, w, ConstTerm(ConstInt(42)))
  let assert Ok(#(_, dr)) = heap.deref(h2, r)
  dr |> should.equal(Bound(ConstTerm(ConstInt(42))))
}

// #3 — bind-to-value (struct, args preserved) → deref the whole struct back.
pub fn parity_bind_value_struct_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let pt = StructTerm("point", [ConstTerm(ConstInt(10)), ConstTerm(ConstInt(20))])
  let assert Ok(#(h2, _)) = heap.bind_writer(h1, w, pt)
  let assert Ok(#(_, dr)) = heap.deref(h2, r)
  dr |> should.equal(Bound(pt))
}

// #4 — bind-to-variable chain → deref resolves through the chain to the terminal value.
pub fn parity_chain_to_value_test() {
  let #(h1, w1, r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let assert Ok(#(h3, [])) = heap.bind_writer_to_var(h2, w1, r2)
  let assert Ok(#(h4, _)) = heap.bind_writer(h3, w2, ConstTerm(ConstInt(7)))
  let assert Ok(#(_, dr)) = heap.deref(h4, r1)
  dr |> should.equal(Bound(ConstTerm(ConstInt(7))))
}

// #5 — unbound bind-to-variable chain → deref reaches the terminal unbound writer.
pub fn parity_unbound_chain_test() {
  let #(h1, w1, r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let assert Ok(#(h3, [])) = heap.bind_writer_to_var(h2, w1, r2)
  let assert Ok(#(_, dr)) = heap.deref(h3, r1)
  dr |> should.equal(Unbound(w2))
}

// #6 — unify const/const: equal → Success (heap unchanged); differ → Fail.
pub fn parity_unify_const_test() {
  let h = heap.new()
  unify(h, ConstTerm(ConstInt(5)), ConstTerm(ConstInt(5)))
  |> should.equal(Ok(Success(h)))
  unify(h, ConstTerm(ConstInt(5)), ConstTerm(ConstInt(6)))
  |> should.equal(Ok(Fail))
}

// #7 — unify writer/value → Success (writer bound); reader/value → Suspend; writer/writer
// → Error(WriterToWriter), never a silent Fail (SC-004).
pub fn parity_unify_writer_reader_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  // writer vs value → Success
  let assert Ok(Success(_)) = unify(h1, VarRef(w), ConstTerm(ConstInt(1)))
  // reader vs value → Suspend on the reader's paired writer
  let assert Ok(Suspend(_, on)) = unify(h1, VarRef(r), ConstTerm(ConstInt(1)))
  on |> should.equal(w)

  let #(h2, w1, _r1) = heap.allocate_variable(heap.new())
  let #(h3, w2, _r2) = heap.allocate_variable(h2)
  unify(h3, VarRef(w1), VarRef(w2))
  |> should.equal(Error(WriterToWriter(w1, w2)))
}

// #8/#9 — suspend → activate (armed yields a GoalRef); a disarmed record yields nothing.
pub fn parity_suspend_activate_test() {
  let #(h1, w, _r) = heap.allocate_variable(heap.new())
  let assert Ok(h2) =
    heap.suspend_on_writer(h1, w, Suspension(goal_id: 77, resume_pc: 1, armed: True))
  let assert Ok(#(_, acts)) = heap.bind_writer(h2, w, ConstTerm(ConstInt(0)))
  acts |> should.equal([GoalRef(goal_id: 77, resume_pc: 1)])
}

pub fn parity_disarmed_no_activation_test() {
  let #(h1, w, _r) = heap.allocate_variable(heap.new())
  let assert Ok(h2) =
    heap.suspend_on_writer(h1, w, Suspension(goal_id: 5, resume_pc: 9, armed: False))
  let assert Ok(#(_, acts)) = heap.bind_writer(h2, w, ConstTerm(ConstInt(0)))
  acts |> should.equal([])
}

// #10 — suspend → forward on bind-to-variable (fires nothing); binding the target fires it.
pub fn parity_forward_on_var_bind_test() {
  let #(h1, w1, _r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let assert Ok(h3) =
    heap.suspend_on_writer(h2, w1, Suspension(goal_id: 42, resume_pc: 500, armed: True))
  let assert Ok(#(h4, [])) = heap.bind_writer_to_var(h3, w1, r2)
  let assert Ok(#(_, acts)) = heap.bind_writer(h4, w2, ConstTerm(ConstInt(0)))
  acts |> should.equal([GoalRef(goal_id: 42, resume_pc: 500)])
}

// #11 — no occurs-check: a writer↔own-reader cycle forms, and deref reports it loudly.
pub fn parity_cycle_deref_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(Success(h2)) = unify(h1, VarRef(w), VarRef(r))
  heap.deref(h2, r) |> should.equal(Error(Cycle(r)))
}
