//// Effectful body kernels `_now/1` + `_send/3` (feature 059, T061
//// `close-body-kernel-now-send`).
////
//// `_now/1` — the external-io wall-clock kernel (Dart `nowKernel`): binds its output
//// to the current time in milliseconds. Tested at the kernel-dispatch seam (an integer
//// is bound; a wrong-shape call is not dispatched) and end-to-end through the engine via
//// self.glp's `now(T?) :- '_now'(T).` wrapper.
////
//// `_send/3` — the madGLP messaging kernel (Dart `sendKernel`): its SUCCESS path (a
//// `MadState` threaded → an outgoing `M_p` message) is `mad_send_seam_test`; here we
//// pin the ABORT paths (Dart "not in madGLP mode" + bad global name), which the runner
//// surfaces as a non-fatal `Failed` (never a crash).

import gleam/option.{Some}
import gleeunit/should
import glp/bytecode/program
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/compiler/loader
import glp/engine
import glp/engine/kernels.{KSuccess}
import glp/engine/runner
import glp/mad/global_name.{WriterName, to_term}
import glp/mad/global_writers_table as wt
import glp/mad/mad_kernels.{type MadState, MadState}
import glp/runtime/heap.{Bound}
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, VarRef}

fn atom(a: String) {
  ConstTerm(ConstAtom(a))
}

// ── _now/1 ─────────────────────────────────────────────────────────────────

pub fn now_is_a_kernel_test() {
  kernels.is_kernel("_now", 1) |> should.be_true
}

// `_now/1` binds its output to a POSITIVE integer (millis since epoch). The value
// varies per call, so we assert its shape, not a fixed number.
pub fn now_binds_current_millis_test() {
  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let assert Ok(KSuccess(h2, _woken, [])) =
    kernels.dispatch(h, "_now", 1, [VarRef(out_writer)])
  let assert Ok(#(_, Bound(ConstTerm(ConstInt(millis))))) =
    heap.deref(h2, out_writer)
  { millis > 0 } |> should.be_true
}

// A wrong-shape `_now` call (no output argument) is not dispatched — the abort path
// (Dart `nowKernel` aborts on `args.length != 1`).
pub fn now_wrong_shape_is_not_dispatched_test() {
  kernels.dispatch(heap.new(), "_now", 1, [])
  |> should.equal(Error(Nil))
}

// End-to-end through the engine: self.glp's `now(T?) :- '_now'(T).` wrapper runs the
// kernel and binds T to an integer time. Driven through the engine facade.
pub fn now_end_to_end_binds_integer_test() {
  let #(_eng, env) = engine.run(engine.new(), "now(T)")
  env.status |> should.equal(result_envelope.Success)
  // T is bound to some integer (the wall-clock time); value unchecked (varies).
  let assert [#("T", term_codec.ConstTerm(term_codec.ConstInt(_)))] =
    env.resolved_bindings
}

// ── _send/3 abort paths ──────────────────────────────────────────────────────

// A tiny system-mode module that forwards to `_send` (as `mad_send_seam_test`).
const seam_source = "-mode(system).
procedure _send(_?, _?, _?).
procedure snd(_?, _?, _?).
snd(T, G, Q) :- '_send'(T?, G?, Q?)."

fn reduce_snd(
  t: terms.Term,
  g: terms.Term,
  q: terms.Term,
  mad: option.Option(MadState),
) -> runner.ReduceOutcome {
  let assert Ok(outcome) = loader.load(seam_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "snd/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, t)
    |> program.set_reg(1, g)
    |> program.set_reg(2, q)
  let ctx = case mad {
    Some(state) -> runner.with_mad(runner.new_context(heap.new(), regs), state)
    option.None -> runner.new_context(heap.new(), regs)
  }
  runner.reduce(prog, ctx, kappa, 1000)
}

// `_send` OUTSIDE madGLP mode (no MadState threaded) aborts — the runner surfaces a
// non-fatal `Failed` (Dart "not in madGLP mode (no MadContext)"), never a crash.
pub fn send_without_mad_context_fails_test() {
  let out =
    reduce_snd(atom("hello"), to_term(WriterName(atom("q"), 5)), atom("q"), option.None)
  let assert runner.Failed(..) = out
}

// `_send` with a bad global name (G is an atom, not a `_w`/`_r` struct) aborts even IN
// madGLP mode (Dart "global name must be _w/2 or _r/2") → non-fatal `Failed`.
pub fn send_bad_global_name_fails_test() {
  let out =
    reduce_snd(
      atom("hello"),
      atom("not_a_global_name"),
      atom("q"),
      Some(MadState(wt.new(atom("p")), [], [])),
    )
  let assert runner.Failed(..) = out
}

// Control: `_send` IN madGLP mode with a well-formed global name SUCCEEDS — the
// reduction commits and carries the outgoing message (the success path, mirrored from
// `mad_send_seam_test` to keep both paths in one place).
pub fn send_in_mad_mode_succeeds_test() {
  let out =
    reduce_snd(
      atom("hello"),
      to_term(WriterName(atom("q"), 5)),
      atom("q"),
      Some(MadState(wt.new(atom("p")), [], [])),
    )
  let assert runner.Reduced(mad: Some(state), ..) = out
  state.m_p |> should.not_equal([])
}
