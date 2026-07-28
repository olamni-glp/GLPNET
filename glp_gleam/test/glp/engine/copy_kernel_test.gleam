//// T065 — the `_copy/2` body kernel (parity port of Dart `copyKernel`): deref the
//// source term and bind the output writer to it (a snapshot). Unblocks the
//// metainterpreter idiom (`programs/tests/tracing_meta.glp`, now full 3-runtime parity).

import gleeunit/should
import glp/engine/kernels
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm, StructTerm, VarRef}

pub fn copy_binds_output_to_derefed_source_test() {
  let #(h, w, r) = heap.allocate_variable(heap.new())
  // A structured source (a goal-shaped term) snapshotted into the output writer.
  let source = StructTerm("merge", [ConstTerm(ConstAtom("a")), ConstTerm(ConstAtom("b"))])
  let assert Ok(kernels.KSuccess(h2, _woken, _out)) =
    kernels.dispatch(h, "_copy", 2, [source, VarRef(w)])
  // The output reads back the snapshotted term.
  let assert Ok(#(_, heap.Bound(copied))) = heap.deref(h2, r)
  copied |> should.equal(source)
}

pub fn copy_follows_a_bound_source_var_test() {
  // Source is a VarRef bound to a value; `_copy` derefs it before binding the output.
  let #(h, sw, sr) = heap.allocate_variable(heap.new())
  let assert Ok(#(h, _)) = heap.bind_writer(h, sw, ConstTerm(ConstAtom("snapshot")))
  let #(h, w, r) = heap.allocate_variable(h)
  let assert Ok(kernels.KSuccess(h2, _, _)) =
    kernels.dispatch(h, "_copy", 2, [VarRef(sr), VarRef(w)])
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("snapshot"))))) =
    heap.deref(h2, r)
}
