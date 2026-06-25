//// US2 (T015) — the SC-003 writer-MGU truth table: const/const, struct/struct
//// (match / functor-mismatch / arity-mismatch), var/value, value/var, var/var,
//// unbound-reader → Suspend (never Fail), WxW → Error (never Fail, SC-004), and no
//// occurs-check (F4). Dart source of truth: glp_runtime/lib/runtime/ (HEAD-phase unify).

import gleeunit/should
import glp/runtime/heap.{Bound, Cycle, WriterToWriter}
import glp/runtime/terms.{
  ConstAtom, ConstInt, ConstTerm, StructTerm, VarRef, cons, nil,
}
import glp/runtime/unify.{Fail, Success, Suspend, unify}

// ground X = ground X (equal) → Success, heap unchanged
pub fn const_const_match_test() {
  let h = heap.new()
  unify(h, ConstTerm(ConstInt(5)), ConstTerm(ConstInt(5)))
  |> should.equal(Ok(Success(h)))
}

// ground ≠ ground → Fail
pub fn const_const_mismatch_test() {
  let h = heap.new()
  unify(h, ConstTerm(ConstInt(5)), ConstTerm(ConstInt(6)))
  |> should.equal(Ok(Fail))
}

// struct/N = struct/N (same functor, equal args) → Success
pub fn struct_match_test() {
  let h = heap.new()
  let s = StructTerm("f", [ConstTerm(ConstInt(1)), ConstTerm(ConstAtom("a"))])
  unify(h, s, s) |> should.equal(Ok(Success(h)))
}

// struct functor mismatch → Fail
pub fn struct_functor_mismatch_test() {
  let h = heap.new()
  unify(
    h,
    StructTerm("f", [ConstTerm(ConstInt(1))]),
    StructTerm("g", [ConstTerm(ConstInt(1))]),
  )
  |> should.equal(Ok(Fail))
}

// struct arity mismatch → Fail
pub fn struct_arity_mismatch_test() {
  let h = heap.new()
  unify(
    h,
    StructTerm("f", [ConstTerm(ConstInt(1))]),
    StructTerm("f", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))]),
  )
  |> should.equal(Ok(Fail))
}

// struct args unify pairwise; the first mismatch short-circuits → Fail
pub fn struct_args_mismatch_test() {
  let h = heap.new()
  unify(
    h,
    StructTerm("f", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))]),
    StructTerm("f", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(3))]),
  )
  |> should.equal(Ok(Fail))
}

// unbound writer vs ground → Success (writer bound); binding touches ONLY the writer
pub fn writer_value_binds_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(Success(h2)) = unify(h1, VarRef(w), ConstTerm(ConstInt(42)))
  heap.is_value(h2, w) |> should.be_true
  heap.is_reader(h2, r) |> should.be_true
  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(ConstTerm(ConstInt(42)))
}

// ground vs unbound writer → Success (symmetric)
pub fn value_writer_binds_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(Success(h2)) = unify(h1, ConstTerm(ConstInt(42)), VarRef(w))
  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(ConstTerm(ConstInt(42)))
}

// unbound writer vs unbound variable → Success (writer→var); the two vars are now linked
pub fn writer_var_links_test() {
  let #(h1, w1, r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let assert Ok(Success(h3)) = unify(h2, VarRef(w1), VarRef(r2))
  let assert Ok(#(h4, _)) = heap.bind_writer(h3, w2, ConstTerm(ConstInt(7)))
  let assert Ok(#(_, Bound(out))) = heap.deref(h4, r1)
  out |> should.equal(ConstTerm(ConstInt(7)))
}

// a sub-position writer binds during pairwise struct unification: f(W,2) = f(1,2)
pub fn struct_arg_binds_writer_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let lhs = StructTerm("f", [VarRef(w), ConstTerm(ConstInt(2))])
  let rhs = StructTerm("f", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))])
  let assert Ok(Success(h2)) = unify(h1, lhs, rhs)
  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(ConstTerm(ConstInt(1)))
}

// list (cons/nil) unification: [1 | W] = [1, 2] binds W to [2]
pub fn list_unify_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let lhs = cons(ConstTerm(ConstInt(1)), VarRef(w))
  let rhs = cons(ConstTerm(ConstInt(1)), cons(ConstTerm(ConstInt(2)), nil()))
  let assert Ok(Success(h2)) = unify(h1, lhs, rhs)
  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(cons(ConstTerm(ConstInt(2)), nil()))
}

// a needed unbound READER → Suspend (NOT Fail); the verdict carries only the `on` address
// (the reader's paired writer) — unify has no goal context to build a record (F1).
pub fn unbound_reader_suspends_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(Suspend(_, on)) = unify(h1, VarRef(r), ConstTerm(ConstInt(1)))
  on |> should.equal(w)
}

// two unbound readers → Suspend (neither is bindable)
pub fn two_readers_suspend_test() {
  let #(h1, _w1, r1) = heap.allocate_variable(heap.new())
  let #(h2, _w2, r2) = heap.allocate_variable(h1)
  let assert Ok(Suspend(_, _)) = unify(h2, VarRef(r1), VarRef(r2))
  Nil |> should.equal(Nil)
}

// every WxW (writer vs writer) → Error(WriterToWriter), never a silent Fail (SC-004)
pub fn wxw_is_error_not_fail_test() {
  let #(h1, w1, _r1) = heap.allocate_variable(heap.new())
  let #(h2, w2, _r2) = heap.allocate_variable(h1)
  unify(h2, VarRef(w1), VarRef(w2))
  |> should.equal(Error(WriterToWriter(w1, w2)))
}

// No occurs-check (F4): unifying a writer with a struct mentioning that writer's own
// reader SUCCEEDS. deref does not traverse struct args (faithful to Dart derefAddr), so
// the self-reference is term data, not a pointer cycle — deref terminates at the value.
pub fn no_occurs_check_struct_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let self_struct = StructTerm("f", [VarRef(r)])
  let assert Ok(Success(h2)) = unify(h1, VarRef(w), self_struct)
  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(self_struct)
}

// No occurs-check (F4): unifying a writer with its OWN reader succeeds and forms a genuine
// pointer cycle; a subsequent deref reports it loudly as Cycle (faithful to Dart).
pub fn no_occurs_check_cycle_test() {
  let #(h1, w, r) = heap.allocate_variable(heap.new())
  let assert Ok(Success(h2)) = unify(h1, VarRef(w), VarRef(r))
  heap.deref(h2, r) |> should.equal(Error(Cycle(r)))
}
