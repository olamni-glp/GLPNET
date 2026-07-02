//// T022/T023 acceptance — Gleam deep-resolve + envelope builder over the real 034
//// heap (glp/runtime/heap). Mirrors the Dart reference behaviour in
//// glp_runtime/lib/codec/result_envelope_builder.dart.
////
//// Runtime terms are imported QUALIFIED (`terms.`) so the runtime Term/Constant
//// constructors do not collide with the codec's identically-named constructors
//// (which stay unqualified for the assertions).

import gleam/list
import gleam/option.{None}
import gleeunit/should
import glp/codec/result_envelope.{Success, decode, encode}
import glp/codec/result_envelope_builder.{build_result_envelope, deep_resolve}
import glp/codec/term_codec.{ConstInt, ConstTerm, GlobalVarId, StructTerm, VarRef}
import glp/runtime/heap
import glp/runtime/terms

const inst = "inst-A"

pub fn deep_resolve_bound_int_test() {
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w, terms.ConstTerm(terms.ConstInt(42)))
  let assert Ok(#(_, got)) = deep_resolve(h2, terms.VarRef(w), inst, 0)
  got |> should.equal(ConstTerm(ConstInt(42)))
}

pub fn deep_resolve_string_is_atom_faithful_test() {
  // 034 already distinguishes atom vs string; a runtime ConstAtom maps to codec ConstAtom.
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  let assert Ok(#(h2, _)) =
    heap.bind_writer(h1, w, terms.ConstTerm(terms.ConstAtom("foo")))
  let assert Ok(#(_, got)) = deep_resolve(h2, terms.VarRef(w), inst, 0)
  got |> should.equal(ConstTerm(term_codec.ConstAtom("foo")))
}

pub fn deep_resolve_unbound_var_test() {
  let h0 = heap.new()
  let #(h1, w, _r) = heap.allocate_variable(h0)
  let assert Ok(#(_, got)) = deep_resolve(h1, terms.VarRef(w), inst, 0)
  got |> should.equal(VarRef(GlobalVarId(inst, w)))
}

pub fn deep_resolve_nested_struct_with_unbound_arg_test() {
  let h0 = heap.new()
  let #(h1, w1, _r1) = heap.allocate_variable(h0)
  let #(h2, w2, _r2) = heap.allocate_variable(h1)
  let assert Ok(#(h3, _)) =
    heap.bind_writer(
      h2,
      w1,
      terms.StructTerm("f", [
        terms.ConstTerm(terms.ConstInt(1)),
        terms.VarRef(w2),
      ]),
    )
  let assert Ok(#(_, got)) = deep_resolve(h3, terms.VarRef(w1), inst, 0)
  got
  |> should.equal(
    StructTerm("f", [ConstTerm(ConstInt(1)), VarRef(GlobalVarId(inst, w2))]),
  )
}

pub fn deep_resolve_depth_bound_truncates_test() {
  let deep = build_nested(40, terms.ConstTerm(terms.ConstInt(0)))
  let assert Ok(#(_, got)) = deep_resolve(heap.new(), deep, inst, 0)
  contains_truncated(got) |> should.equal(True)
  let assert Ok(#(_, shallow)) =
    deep_resolve(heap.new(), terms.ConstTerm(terms.ConstInt(0)), inst, 0)
  contains_truncated(shallow) |> should.equal(False)
}

fn build_nested(n: Int, inner: terms.Term) -> terms.Term {
  case n {
    0 -> inner
    _ -> build_nested(n - 1, terms.StructTerm("s", [inner]))
  }
}

fn contains_truncated(t: term_codec.Term) -> Bool {
  case t {
    StructTerm("$truncated", _) -> True
    StructTerm(_, args) -> list.any(args, contains_truncated)
    _ -> False
  }
}

pub fn build_envelope_bound_unbound_and_roundtrip_test() {
  let h0 = heap.new()
  let #(h1, w_x, _rx) = heap.allocate_variable(h0)
  let #(h2, w_y, _ry) = heap.allocate_variable(h1)
  let assert Ok(#(h3, _)) =
    heap.bind_writer(h2, w_x, terms.ConstTerm(terms.ConstInt(3)))

  // X bound to 3; Y unbound; blocking readers {5,2} must sort → 2,5.
  let assert Ok(#(_, env)) =
    build_result_envelope(
      h3,
      [#("X", w_x), #("Y", w_y)],
      Success,
      [5, 2],
      inst,
      <<>>,
      None,
    )

  env.status |> should.equal(Success)
  env.resolved_bindings |> should.equal([#("X", ConstTerm(ConstInt(3)))])
  env.var_to_writer |> should.equal([#("Y", GlobalVarId(inst, w_y))])
  env.suspended
  |> should.equal([GlobalVarId(inst, 2), GlobalVarId(inst, 5)])

  // T021-equivalent: the built envelope round-trips through the shipped codec.
  decode(encode(env)) |> should.equal(Ok(env))
}
