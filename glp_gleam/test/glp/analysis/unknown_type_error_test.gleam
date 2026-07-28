//// T073 — a procedure declaration referencing a WHOLLY-UNDEFINED type yields a
//// CLEAN staged `TypeError` (message `UnknownTypeError: <name>`), NOT the
//// `program_dfa` automaton-builder panic (which crashed the REPL / `gleam test`).
//// Dart parity: `Error loading …: UnknownTypeError: Foo?` (the REPL survives).

import gleam/string
import gleeunit/should
import glp/compiler/loader
import glp/diagnostics

// `Foo` is defined nowhere (no prelude, no scope chain).
const src = "procedure p(Foo?).
p(anything)."

pub fn undefined_type_in_proc_decl_is_clean_staged_error_not_panic_test() {
  // The load is REJECTED (a staged error) — crucially it does NOT panic.
  let assert Error(diagnostics.StagedError(_stage, _kind, _pos, message)) =
    loader.load(src, "")
  // The message matches Dart's `UnknownTypeError: Foo?` (the `?` because the
  // undefined type is used in input mode).
  string.contains(message, "UnknownTypeError: Foo?") |> should.be_true
}

// `Undefined` is referenced inside a type DEFINITION's alternative — the second
// panic path (`build_type_automaton`). Dart parity: `UnknownTypeError: Undefined`.
const typedef_src = "Foo ::= bar(Undefined).
procedure q(Foo?).
q(bar(x))."

pub fn undefined_type_in_type_def_alternative_is_clean_staged_error_test() {
  let assert Error(diagnostics.StagedError(_stage, _kind, _pos, message)) =
    loader.load(typedef_src, "")
  string.contains(message, "UnknownTypeError: Undefined") |> should.be_true
}
