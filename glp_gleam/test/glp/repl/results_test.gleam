//// Result rendering tests (feature 050, T033 support; US2). Pin the reference
//// REPL output shape (Dart `_formatTerm` / `_printStatus`) over the codec term
//// model — the ED-1 in-process rendering the corpus runner diffs against goldens.

import gleam/option.{None, Some}
import gleeunit/should
import glp/codec/result_envelope.{Failed, ResultEnvelope, Success, Suspended}
import glp/codec/term_codec.{
  ConstAtom, ConstInt, ConstString, ConstTerm, GlobalVarId, StructTerm, VarRef,
}
import glp/repl/results

fn atom(a: String) {
  ConstTerm(ConstAtom(a))
}

fn gvar(id: Int) {
  VarRef(GlobalVarId("local", id))
}

// ── format_term ──────────────────────────────────────────────────────────────

pub fn format_int_test() {
  results.format_term(ConstTerm(ConstInt(5)))
  |> should.equal("5")
}

pub fn format_atom_test() {
  results.format_term(atom("zero"))
  |> should.equal("zero")
}

pub fn format_string_test() {
  results.format_term(ConstTerm(ConstString("hi")))
  |> should.equal("hi")
}

pub fn format_nil_is_empty_list_test() {
  results.format_term(atom("nil"))
  |> should.equal("[]")
}

pub fn format_struct_test() {
  results.format_term(StructTerm("p", [ConstTerm(ConstInt(1)), atom("a")]))
  |> should.equal("p(1, a)")
}

pub fn format_unbound_var_is_writer_id_test() {
  results.format_term(gvar(7))
  |> should.equal("X7")
}

// A proper list [1, a] built over '.'/2 with a nil terminal.
pub fn format_proper_list_test() {
  let list_term =
    StructTerm(".", [
      ConstTerm(ConstInt(1)),
      StructTerm(".", [atom("a"), atom("nil")]),
    ])
  results.format_term(list_term)
  |> should.equal("[1, a]")
}

// A partial list [1 | X9] — an unbound tail prints as `| X<id>`.
pub fn format_partial_list_test() {
  let list_term = StructTerm(".", [ConstTerm(ConstInt(1)), gvar(9)])
  results.format_term(list_term)
  |> should.equal("[1 | X9]")
}

// A nested structure inside a list: [p(a), b].
pub fn format_nested_struct_in_list_test() {
  let list_term =
    StructTerm(".", [
      StructTerm("p", [atom("a")]),
      StructTerm(".", [atom("b"), atom("nil")]),
    ])
  results.format_term(list_term)
  |> should.equal("[p(a), b]")
}

// ── render_outcome ───────────────────────────────────────────────────────────

fn envelope(status, bindings, unbound, error) {
  ResultEnvelope(
    status: status,
    resolved_bindings: bindings,
    var_to_writer: unbound,
    suspended: [],
    captured: <<>>,
    error: error,
  )
}

// The headline case: X = 5 → succeeds.
pub fn render_success_binding_test() {
  envelope(Success, [#("X", ConstTerm(ConstInt(5)))], [], None)
  |> results.render_outcome
  |> should.equal(["X = 5", "→ succeeds"])
}

pub fn render_failure_test() {
  envelope(Failed, [], [], None)
  |> results.render_outcome
  |> should.equal(["→ failed"])
}

pub fn render_suspension_test() {
  envelope(Suspended, [], [], None)
  |> results.render_outcome
  |> should.equal(["→ suspended"])
}

// An unbound query var renders as its global writer id, then the status.
pub fn render_unbound_var_test() {
  envelope(Suspended, [], [#("Y", GlobalVarId("local", 3))], None)
  |> results.render_outcome
  |> should.equal(["Y = X3", "→ suspended"])
}

// An error line follows the status.
pub fn render_error_test() {
  envelope(Failed, [], [], Some("no matching clause for foo/1"))
  |> results.render_outcome
  |> should.equal(["→ failed", "Error: no matching clause for foo/1"])
}
