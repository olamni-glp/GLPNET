//// US3 deref + var→writer fidelity (T033/T034/T037) — Gleam reproduces the Dart-
//// referenced resolved outcomes (deref-corpus.md, T035) over the real 034 heap. Pins the
//// depth-32 boundary EXACTLY: a 32-deep struct chain resolves; a 33-deep chain yields the
//// explicit $truncated marker at depth 33. var→writer identity preserved by GlobalVarId.

import gleam/list
import gleam/option.{None}
import gleeunit/should
import glp/codec/result_envelope.{ResultEnvelope, Success, decode, encode}
import glp/codec/result_envelope_builder.{
  build_result_envelope, deep_resolve, truncated_marker,
}
import glp/codec/term_codec.{
  type Term, ConstAtom, ConstInt, ConstTerm, GlobalVarId, StructTerm, VarRef,
}
import glp/runtime/heap.{type Heap}
import glp/runtime/terms

const inst = "glpnet-test-0001"

// A chain of n nested single-arg s(·) structs over a ConstInt(0) leaf; the leaf sits at
// depth n. Returns #(heap, top_writer).
fn chain(n: Int) -> #(Heap, Int) {
  let h0 = heap.new()
  let #(h1, w0, _r0) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w0, terms.ConstTerm(terms.ConstInt(0)))
  case n {
    0 -> #(h2, w0)
    _ ->
      list.repeat(Nil, n)
      |> list.fold(#(h2, w0), fn(acc, _i) {
        let #(h, child) = acc
        let #(h_a, w, _r) = heap.allocate_variable(h)
        let assert Ok(#(h_b, _)) =
          heap.bind_writer(h_a, w, terms.StructTerm("s", [terms.VarRef(child)]))
        #(h_b, w)
      })
  }
}

fn depth_to_marker(t: Term) -> Int {
  case t {
    StructTerm("$truncated", _) -> 0
    StructTerm(_, [inner]) -> {
      let d = depth_to_marker(inner)
      case d < 0 {
        True -> -1
        False -> 1 + d
      }
    }
    _ -> -1
  }
}

fn contains_truncated(t: Term) -> Bool {
  case t {
    StructTerm("$truncated", _) -> True
    StructTerm(_, args) -> list.any(args, contains_truncated)
    _ -> False
  }
}

// T033 --------------------------------------------------------------------

pub fn t033_bound_nested_struct_resolves_fully_test() {
  let h0 = heap.new()
  let #(h1, w1, _) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w1, terms.ConstTerm(terms.ConstInt(1)))
  let #(h3, w2, _) = heap.allocate_variable(h2)
  let assert Ok(#(h4, _)) =
    heap.bind_writer(h3, w2, terms.ConstTerm(terms.ConstInt(2)))
  let #(h5, sw, _) = heap.allocate_variable(h4)
  let assert Ok(#(h6, _)) =
    heap.bind_writer(
      h5,
      sw,
      terms.StructTerm("point", [terms.VarRef(w1), terms.VarRef(w2)]),
    )
  let assert Ok(#(_, r)) = deep_resolve(h6, terms.VarRef(sw), inst, 0)
  r
  |> should.equal(
    StructTerm("point", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))]),
  )
}

pub fn t033_depth32_leaf_resolves_no_truncation_test() {
  let #(h, top) = chain(32)
  let assert Ok(#(_, r)) = deep_resolve(h, terms.VarRef(top), inst, 0)
  contains_truncated(r) |> should.equal(False)
  depth_to_marker(r) |> should.equal(-1)
}

// T037 --------------------------------------------------------------------

pub fn t037_depth33_truncated_marker_at_exact_depth_test() {
  let #(h, top) = chain(33)
  let assert Ok(#(_, r)) = deep_resolve(h, terms.VarRef(top), inst, 0)
  contains_truncated(r) |> should.equal(True)
  depth_to_marker(r) |> should.equal(33)
}

pub fn t037_truncated_marker_is_normal_decodable_term_test() {
  let env =
    ResultEnvelope(Success, [#("T", truncated_marker())], [], [], <<>>, None)
  let assert Ok(decoded) = decode(encode(env))
  let assert ResultEnvelope(_, [#("T", got)], _, _, _, _) = decoded
  got |> should.equal(truncated_marker())
}

// T034 --------------------------------------------------------------------

pub fn t034_multiple_unbound_query_vars_ordered_var_to_writer_test() {
  let h0 = heap.new()
  let #(h1, wx, _) = heap.allocate_variable(h0)
  let #(h2, wy, _) = heap.allocate_variable(h1)
  let #(h3, wz, _) = heap.allocate_variable(h2)
  let assert Ok(#(_, env)) =
    build_result_envelope(
      h3,
      [#("X", wx), #("Y", wy), #("Z", wz)],
      Success,
      [],
      inst,
      <<>>,
      None,
    )
  let ResultEnvelope(_, _, vtw, _, _, _) = env
  vtw
  |> should.equal([
    #("X", GlobalVarId(inst, wx)),
    #("Y", GlobalVarId(inst, wy)),
    #("Z", GlobalVarId(inst, wz)),
  ])
  // identity survives the codec round-trip
  decode(encode(env)) |> should.equal(Ok(env))
}

pub fn t034_unbound_var_in_bound_struct_keeps_global_id_test() {
  let h0 = heap.new()
  let #(h1, wa, _) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, wa, terms.ConstTerm(terms.ConstAtom("a")))
  let #(h3, wu, _) = heap.allocate_variable(h2)
  // wu is never bound
  let #(h4, sw, _) = heap.allocate_variable(h3)
  let assert Ok(#(h5, _)) =
    heap.bind_writer(
      h4,
      sw,
      terms.StructTerm("pair", [terms.VarRef(wa), terms.VarRef(wu)]),
    )
  let assert Ok(#(_, r)) = deep_resolve(h5, terms.VarRef(sw), inst, 0)
  let assert StructTerm("pair", [
    ConstTerm(ConstAtom("a")),
    VarRef(GlobalVarId(agent, _local)),
  ]) = r
  agent |> should.equal(inst)
}
