//// US1 (T010) — allocate / deref / bind / path-compression / WxW for the immutable
//// threaded store (SC-002 + SC-004 deref). Dart source of truth:
//// glp_runtime/lib/runtime/heap_fcp.dart.

import gleeunit/should
import glp/runtime/heap.{AlreadyBound, Bound, Unbound, WriterToWriter}
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm}

// A fresh variable: role read from the tag; deref of either half reaches the unbound
// writer (SC-002, AS US1#4).
pub fn fresh_var_derefs_unbound_test() {
  let h0 = heap.new()
  let #(h1, w, r) = heap.allocate_variable(h0)

  heap.is_writer(h1, w) |> should.be_true
  heap.is_reader(h1, r) |> should.be_true
  heap.is_value(h1, w) |> should.be_false

  let assert Ok(#(_, Unbound(uw))) = heap.deref(h1, r)
  uw |> should.equal(w)
  let assert Ok(#(_, Unbound(uw2))) = heap.deref(h1, w)
  uw2 |> should.equal(w)
}

// Bind the writer to a ground value; the reader derefs back to it; role flips (SC-002).
pub fn bind_writer_then_deref_value_test() {
  let h0 = heap.new()
  let #(h1, w, r) = heap.allocate_variable(h0)
  let value = ConstTerm(ConstAtom("bound_atom"))

  let assert Ok(#(h2, activations)) = heap.bind_writer(h1, w, value)
  activations |> should.equal([])

  heap.is_value(h2, w) |> should.be_true
  heap.is_writer(h2, w) |> should.be_false

  let assert Ok(#(_, Bound(out))) = heap.deref(h2, r)
  out |> should.equal(value)
}

// Single-assignment: a second bind of an already-bound writer is reported loudly (FR-005).
pub fn single_assignment_test() {
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) = heap.bind_writer(h1, w, ConstTerm(ConstInt(1)))
  heap.bind_writer(h2, w, ConstTerm(ConstInt(2)))
  |> should.equal(Error(AlreadyBound(w)))
}

// deref through a writer→variable chain resolves to the terminal value.
pub fn bind_writer_to_var_then_value_test() {
  let h0 = heap.new()
  let #(h1, w1, r1) = heap.allocate_variable(h0)
  let #(h2, w2, r2) = heap.allocate_variable(h1)

  let assert Ok(#(h3, [])) = heap.bind_writer_to_var(h2, w1, r2)
  let value = ConstTerm(ConstInt(99))
  let assert Ok(#(h4, [])) = heap.bind_writer(h3, w2, value)

  let assert Ok(#(_, Bound(out))) = heap.deref(h4, r1)
  out |> should.equal(value)
}

// Path compression (SC-002): a long chain compresses on first deref (heap changes), and a
// repeated deref on the returned heap is a fixpoint — no re-traversal, no further change.
pub fn deref_path_compression_test() {
  let h0 = heap.new()
  let #(h1, w1, r1) = heap.allocate_variable(h0)
  let #(h2, w2, r2) = heap.allocate_variable(h1)
  let #(h3, w3, r3) = heap.allocate_variable(h2)

  // v1 -> v2 -> v3 (v3 unbound terminal)
  let assert Ok(#(h4, [])) = heap.bind_writer_to_var(h3, w1, r2)
  let assert Ok(#(h5, [])) = heap.bind_writer_to_var(h4, w2, r3)

  let assert Ok(#(h6, res1)) = heap.deref(h5, r1)
  res1 |> should.equal(Unbound(w3))
  // first deref shortened the chain → the heap value changed (compression marker)
  h6 |> should.not_equal(h5)

  // re-deref on the returned heap reaches a fixpoint (no re-traversal)
  let assert Ok(#(h7, res2)) = heap.deref(h6, r1)
  res2 |> should.equal(Unbound(w3))
  h7 |> should.equal(h6)
}

// WxW (SC-004): binding a writer to another WRITER is a violation, reported loudly,
// never a silent writer→writer chain.
pub fn wxw_writer_to_writer_test() {
  let h0 = heap.new()
  let #(h1, w1, _r1) = heap.allocate_variable(h0)
  let #(h2, w2, _r2) = heap.allocate_variable(h1)
  heap.bind_writer_to_var(h2, w1, w2)
  |> should.equal(Error(WriterToWriter(w1, w2)))
}
