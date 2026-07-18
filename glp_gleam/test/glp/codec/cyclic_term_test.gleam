//// Cyclic-term deep-resolve (FORK-1, C-19). FORK-1 was an OPEN owner-gated fork —
//// the circular-term deref discriminator (loud-all vs structural-vs-cycle). It was
//// RESOLVED owner-directed on 2026-07-13 in favour of structural cycle detection,
//// converging with the Dart/C# REPL deref: a self-referential term resolves to the
//// `<circular>` marker at the point the deref path revisits a variable, NEVER loops,
//// and remains a normal round-trippable codec value. (The depth-bounded `$truncated`
//// marker still applies to genuinely deep, non-cyclic terms.)

import gleam/list
import gleam/option.{None}
import gleam/set
import gleeunit/should
import glp/codec/result_envelope.{ResultEnvelope, Success, decode, encode}
import glp/codec/result_envelope_builder.{circular_marker, deep_resolve}
import glp/codec/term_codec.{type Term, StructTerm}
import glp/runtime/heap
import glp/runtime/terms

const inst = "glpnet-test-0001"

fn contains_circular(t: Term) -> Bool {
  case t == circular_marker() {
    True -> True
    False ->
      case t {
        StructTerm(_, args) -> list.any(args, contains_circular)
        _ -> False
      }
  }
}

pub fn self_referential_struct_renders_circular_test() {
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  // s(Self): the struct bound to w references w itself → a cycle.
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w, terms.StructTerm("s", [terms.VarRef(w)]))
  let assert Ok(#(_, resolved)) =
    deep_resolve(h2, terms.VarRef(w), inst, 0, set.new())
  // terminated (no infinite loop) AND surfaced the explicit <circular> marker at the
  // revisit — never a silent cut, never a loop (FORK-1 resolved to cycle detection).
  contains_circular(resolved) |> should.equal(True)
  // the resolved term is a normal codec value that round-trips.
  let env = ResultEnvelope(Success, [#("C", resolved)], [], [], <<>>, None)
  decode(encode(env)) |> should.equal(Ok(env))
}
