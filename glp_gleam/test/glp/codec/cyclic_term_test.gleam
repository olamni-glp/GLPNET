//// T041 cyclic-term defer-to-runtime (FR-008, D5/FORK-1 OPEN): a cyclic term encodes via the
//// depth-bounded deref and NEVER loops. This test asserts consistency with the runtime deref +
//// the existing depth bound (R5); it does NOT define a codec-local cycle policy — that is an
//// OWNER decision (D5/FORK-1), deliberately left open. A self-referential STRUCT resolves to
//// the depth-bounded $truncated marker (parity with Dart/C#); the 034 heap ALSO surfaces a raw
//// pointer cycle as a loud HeapError.Cycle. Either way: no infinite loop.

import gleam/list
import gleam/option.{None}
import gleeunit/should
import glp/codec/result_envelope.{ResultEnvelope, Success, decode, encode}
import glp/codec/result_envelope_builder.{deep_resolve}
import glp/codec/term_codec.{type Term, StructTerm}
import glp/runtime/heap
import glp/runtime/terms

const inst = "glpnet-test-0001"

fn contains_truncated(t: Term) -> Bool {
  case t {
    StructTerm("$truncated", _) -> True
    StructTerm(_, args) -> list.any(args, contains_truncated)
    _ -> False
  }
}

pub fn t041_self_referential_struct_truncates_test() {
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  // s(Self): the struct bound to w references w itself → a cycle.
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w, terms.StructTerm("s", [terms.VarRef(w)]))
  let assert Ok(#(_, resolved)) = deep_resolve(h2, terms.VarRef(w), inst, 0)
  // terminated (no infinite loop) AND surfaced the explicit marker (never a silent cut)
  contains_truncated(resolved) |> should.equal(True)
  // the bounded term is a normal codec value that round-trips
  let env = ResultEnvelope(Success, [#("C", resolved)], [], [], <<>>, None)
  decode(encode(env)) |> should.equal(Ok(env))
}
