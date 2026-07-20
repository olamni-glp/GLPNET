//// `_output/1` output-capture tests (feature 050, T034; US2).
////
//// Three layers: (1) `format_ground_term` renders like the reference kernel (Dart
//// `formatGroundTerm`); (2) `kernels.dispatch` on `_output/1` yields one captured
//// line, heap untouched, no reactivations; (3) end-to-end — a loaded program's
//// `'_output'(T?)` body call flows a captured line through kernel → runner →
//// scheduler → engine, returned as DATA (not the R4-excluded envelope field).

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

// ── format_ground_term (Dart formatGroundTerm) ───────────────────────────────

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

// A struct inside a list — nested formatting.
pub fn format_nested_test() {
  let list_term = cons(StructTerm("p", [atom("a")]), cons(atom("b"), nil()))
  output_capture.format_ground_term(heap.new(), list_term)
  |> should.equal("[p(a), b]")
}

// ── kernels.dispatch on _output/1 ────────────────────────────────────────────

pub fn output_is_a_kernel_test() {
  kernels.is_kernel("_output", 1) |> should.be_true
}

// `_output`(hello): one captured line, heap unchanged (same value back), no wakes.
// The `ui` sink stays EMPTY — `_output` must never leak into the UI channel.
pub fn dispatch_output_captures_line_test() {
  let h = heap.new()
  let assert Ok(kernels.KSuccess(_h, [], ["hello"], [])) =
    kernels.dispatch(h, "_output", 1, [atom("hello")])
}

// `'_send_to_ui'`(hello): the mirror image — one UI line, and the program-output sink
// stays EMPTY. Together with the test above this pins the §12.6 separation at the
// kernel boundary: neither sink ever picks up the other's traffic.
pub fn dispatch_send_to_ui_captures_ui_line_test() {
  let h = heap.new()
  let assert Ok(kernels.KSuccess(_h, [], [], ["hello"])) =
    kernels.dispatch(h, "_send_to_ui", 1, [atom("hello")])
}

// ── end-to-end: loaded program → captured output threaded through the engine ──

const emit_source = "-mode(system).
procedure emit(Constant?).
emit(X) :- ground(X?) | '_output'(X?)."

// emit(hello) runs `'_output'(hello)` in its body; the captured line is returned
// by run_with_limit_capturing (ahead of the outcome), and the run succeeds.
pub fn output_captured_end_to_end_test() {
  let e = engine.new()
  let assert Ok(e) = engine.load(e, emit_source)
  let #(_e, env, output) =
    engine.run_with_limit_capturing(e, "emit(hello)", 1_000_000)

  output |> should.equal(["hello"])
  env.status |> should.equal(result_envelope.Success)
}

// ── end-to-end: the UI sink, and its separation from program output ──

// Drives the SHIPPED wrapper `send_to_ui/1` (mad_predicates.glp §12.4) rather than
// calling the kernel directly, so this covers the whole path: GLP clause → ground/1
// guard → '_send_to_ui' kernel → runner → scheduler → facade.
const ui_source = "-mode(system).
procedure send_to_ui(Stream(X)?).
send_to_ui([X|In]) :- ground(X?) | '_send_to_ui'(X?), send_to_ui(In?).
send_to_ui([]).

procedure ui_and_out(Constant?).
ui_and_out(X) :- ground(X?) | send_to_ui([X?]), '_output'(other)."

// The load-bearing property of Gabi's §1.14 ruling: the two sinks stay SEPARATE all
// the way out to the embedder. One goal emits to both; `ui` must carry only the UI
// term and `output` only the program-output term — never merged, never cross-fed.
pub fn ui_sink_is_separate_from_output_end_to_end_test() {
  let e = engine.new()
  let assert Ok(e) = engine.load(e, ui_source)
  let #(_e, env, output, ui) =
    engine.run_with_limit_capturing_ui(e, "ui_and_out(hello)", 1_000_000)

  ui |> should.equal(["hello"])
  output |> should.equal(["other"])
  env.status |> should.equal(result_envelope.Success)
}
