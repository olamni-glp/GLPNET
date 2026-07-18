//// Goal-format + `:trace` tests (feature 050, US2 polish).
////
//// `goal_format` renders a live goal (procedure label + arg registers) as the
//// reference trace/goal shape (Dart `_formatGoal`/`_formatTerm`, arity-stripped,
//// reader-marked); the engine's traced run emits `head :- body` reduction lines
//// when `:trace` is on and nothing when off.

import gleam/int
import gleam/list
import gleam/string
import gleeunit/should
import glp/bytecode/program
import glp/engine
import glp/engine/goal_format
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstTerm, StructTerm, VarRef}

fn atom(a: String) {
  ConstTerm(ConstAtom(a))
}

// ── format_goal / format_term ────────────────────────────────────────────────

pub fn format_goal_strips_arity_test() {
  let regs =
    program.new_regs()
    |> program.set_reg(0, atom("zero"))
    |> program.set_reg(1, atom("one"))
  goal_format.format_goal("flip/2", regs, heap.new())
  |> should.equal("flip(zero, one)")
}

pub fn format_goal_arity_zero_is_bare_name_test() {
  goal_format.format_goal("done/0", program.new_regs(), heap.new())
  |> should.equal("done")
}

pub fn split_label_test() {
  goal_format.split_label("merge/3") |> should.equal(#("merge", 3))
  goal_format.split_label("weird") |> should.equal(#("weird", 0))
}

pub fn format_term_struct_test() {
  goal_format.format_term(
    heap.new(),
    StructTerm("p", [atom("a"), ConstTerm(ConstInt(1))]),
  )
  |> should.equal("p(a, 1)")
}

// An unbound reader formats as `X<writer>?`; its writer half as `X<writer>`.
pub fn format_unbound_reader_marks_question_test() {
  let #(h, w, r) = heap.allocate_variable(heap.new())
  goal_format.format_term(h, VarRef(r))
  |> should.equal("X" <> int.to_string(w) <> "?")
  goal_format.format_term(h, VarRef(w))
  |> should.equal("X" <> int.to_string(w))
}

// ── engine `:trace` integration ──────────────────────────────────────────────

// A traced run emits at least one reference-shape line (`head :- body` or `→`).
pub fn traced_run_emits_reduction_lines_test() {
  let e = engine.new()
  let #(_e, _env, _out, traces) =
    engine.run_with_limit_traced(e, "X := 2+3", 1_000_000, True)
  list.is_empty(traces) |> should.be_false
  list.all(traces, fn(line) {
    string.contains(line, " :- ") || string.contains(line, " → ")
  })
  |> should.be_true
}

// An untraced run produces no trace lines.
pub fn untraced_run_has_no_trace_lines_test() {
  let e = engine.new()
  let #(_e, _env, _out, traces) =
    engine.run_with_limit_traced(e, "X := 2+3", 1_000_000, False)
  traces |> should.equal([])
}
