//// `_output/1` output-capture tests (feature 050, T034; US2).
////
//// Three layers: (1) `format_ground_term` renders like the reference kernel (Dart
//// `formatGroundTerm`); (2) `kernels.dispatch` on `_output/1` yields one captured
//// line, heap untouched, no reactivations; (3) end-to-end â€” a loaded program's
//// `'_output'(T?)` body call flows a captured line through kernel â†’ runner â†’
//// scheduler â†’ engine, returned as DATA (not the R4-excluded envelope field).

import gleeunit/should
import glp/codec/result_envelope
import glp/engine
import glp/engine/kernels
import glp/engine/output_capture
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, StructTerm, cons, nil}

fn atom(a: String) {
  ConstTerm(ConstAtom(a))
}

// â”€â”€ format_ground_term (Dart formatGroundTerm) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

pub fn format_atom_test() {
  output_capture.format_ground_term(heap.new(), atom("hello"))
  |> should.equal("hello")
}

pub fn format_int_test() {
  output_capture.format_ground_term(heap.new(), ConstTerm(ConstInt(42)))
  |> should.equal("42")
}

pub fn format_list_test() {
  let list_term = cons(atom("a"), cons(atom("b"), nil()))
  output_capture.format_ground_term(heap.new(), list_term)
  |> should.equal("[a, b]")
}

pub fn format_struct_test() {
  output_capture.format_ground_term(
    heap.new(),
    StructTerm("p", [atom("a"), ConstTerm(ConstInt(1))]),
  )
  |> should.equal("p(a, 1)")
}

// A struct inside a list â€” nested formatting.
pub fn format_nested_test() {
  let list_term = cons(StructTerm("p", [atom("a")]), cons(atom("b"), nil()))
  output_capture.format_ground_term(heap.new(), list_term)
  |> should.equal("[p(a), b]")
}

// â”€â”€ kernels.dispatch on _output/1 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

pub fn output_is_a_kernel_test() {
  kernels.is_kernel("_output", 1) |> should.be_true
}

// `_output`(hello): one captured line, heap unchanged (same value back), no wakes.
pub fn dispatch_output_captures_line_test() {
  let h = heap.new()
  let assert Ok(kernels.KSuccess(_h, [], ["hello"])) =
    kernels.dispatch(h, "_output", 1, [atom("hello")])
}

// â”€â”€ end-to-end: loaded program â†’ captured output threaded through the engine â”€â”€

const emit_source = "-mode(system).
procedure emit(Constant?).
emit(X) :- ground(X?) | '_output'(X?)."

// emit(hello) runs `'_output'(hello)` in its body; the captured line is returned
// by run_with_limit_capturing (ahead of the outcome), and the run succeeds.
pub fn output_captured_end_to_end_test() {
  let e = engine.new()
  let assert Ok(e) = engine.load(e, "emit", emit_source)
  let #(_e, env, output) =
    engine.run_with_limit_capturing(e, "emit(hello)", 1_000_000)

  output |> should.equal(["hello"])
  env.status |> should.equal(result_envelope.Success)
}
