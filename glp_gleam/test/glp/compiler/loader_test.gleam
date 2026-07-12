//// Load-pipeline tests (feature 050, T020).
////
//// The loader wires the fixed stage order (parse → SRSW → PE → type check →
//// compile → load) from contracts/gleam-instance-surface.md. These tests pin
//// (a) a self-contained, SRSW-clean, well-typed program loading clean against
//// an empty prelude, and (b) one negative per early stage, each asserting the
//// EARLIEST rejecting stage and its rejection class — the property the contract
//// requires ("later stages do not run"; classes match the reference sections
//// C/D/E shape).

import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/diagnostics

// A two-clause boolean flip: local union type, a proc declaration with full
// input coverage, no variables (SRSW-trivial), no defined guards (PE is a
// no-op). Loads clean with no prelude.
const good_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

pub fn load_success_test() {
  let assert Ok(outcome) = loader.load(good_source, "")
  // The compiled program carries the flip/2 procedure entry label.
  let ops = program.to_ops(outcome.program)
  should.be_true(ops != [])
}

// ── Negatives: earliest stage rejects, later stages do not run ──────────────

// Unterminated clause — the lexer/parser rejects before any later stage.
pub fn parse_negative_test() {
  let assert Error(err) = loader.load("flip(zero, one", "")
  err.stage
  |> should.equal(diagnostics.ParseStage)
}

// A single variable used as a writer three times, at a non-constant (union)
// type — no constant-type relaxation, so SRSW rejects. Parses fine; must not
// reach type checking.
const srsw_negative = "T ::= a ; b.
procedure dup(T?, T, T).
dup(X, X, X)."

pub fn srsw_negative_test() {
  let assert Error(err) = loader.load(srsw_negative, "")
  err.stage
  |> should.equal(diagnostics.SrswStage)
  err.class
  |> should.equal(diagnostics.SrswViolation)
}

// SRSW-clean and parseable, but arg 2 is produced as a `U` while the clause
// supplies an `a` (a `T` constant) — a type error, rejected at the type stage.
const type_negative = "T ::= a ; b.
U ::= c ; d.
procedure f(T?, U).
f(a, a).
f(b, a)."

pub fn type_negative_test() {
  let assert Error(err) = loader.load(type_negative, "")
  err.stage
  |> should.equal(diagnostics.TypeCheckStage)
  err.class
  |> should.equal(diagnostics.TypeError)
}
