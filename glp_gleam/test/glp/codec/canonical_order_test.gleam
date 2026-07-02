//// T036 canonical serialization order: bindings / varToWriter / suspended serialize in the
//// producing engine's declaration/insertion order — deterministically and identically across
//// runtimes (data-model §1 parity invariant; list order MUST NOT be reordered). Cross-runtime
//// identity is additionally pinned by the golden multi_binding / var_to_writer vectors.

import gleam/list
import gleam/option.{None}
import gleeunit/should
import glp/codec/result_envelope.{
  type ResultEnvelope, ResultEnvelope, Success, decode, encode,
}
import glp/codec/term_codec.{ConstInt, ConstTerm, GlobalVarId}

// Non-alphabetical declaration order — a leaked sort would reorder these.
fn env() -> ResultEnvelope {
  ResultEnvelope(
    Success,
    [
      #("C", ConstTerm(ConstInt(3))),
      #("A", ConstTerm(ConstInt(1))),
      #("B", ConstTerm(ConstInt(2))),
    ],
    [#("Y", GlobalVarId("a", 2)), #("X", GlobalVarId("a", 1))],
    [],
    <<>>,
    None,
  )
}

pub fn t036_encode_is_deterministic_test() {
  encode(env()) |> should.equal(encode(env()))
}

pub fn t036_serializes_in_declaration_order_test() {
  let assert Ok(decoded) = decode(encode(env()))
  let ResultEnvelope(_, bindings, vtw, _, _, _) = decoded
  bindings |> list.map(fn(b) { b.0 }) |> should.equal(["C", "A", "B"])
  vtw |> list.map(fn(v) { v.0 }) |> should.equal(["Y", "X"])
}
