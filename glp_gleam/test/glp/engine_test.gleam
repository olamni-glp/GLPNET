//// Engine-as-typed-value facade tests (feature 050, T029 Slice 1).
////
//// The facade wires construction (`new`/`new_with_prelude`) + `load` over the
//// loader pipeline (contracts/gleam-instance-surface.md §"Engine as typed value"):
//// a fresh engine's runnable program is the prelude alone; `load` prepends user
//// code (user labels win); staged rejections propagate unchanged. The load-time
//// negatives here are the SRSW-neg + type-neg half of T030's smoke set.

import gleeunit/should
import glp/bytecode/program
import glp/diagnostics
import glp/engine

// Reuse the loader's pinned corpus sources (loader_test.gleam) so the facade is
// shown to propagate the SAME staged classification the pipeline produces.
const good_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

const srsw_negative = "T ::= a ; b.
procedure dup(T?, T, T).
dup(X, X, X)."

const type_negative = "T ::= a ; b.
U ::= c ; d.
procedure f(T?, U).
f(a, a).
f(b, a)."

// A bare engine (empty prelude) has no user procedures and no warnings.
pub fn new_with_prelude_is_prelude_only_test() {
  let eng = engine.new_with_prelude("")
  program.label_pc(engine.program(eng), "flip/2")
  |> should.equal(Error(Nil))
  engine.warnings(eng)
  |> should.equal([])
}

// new() reads the on-disk root self.glp and compiles it: the runnable program
// carries the prelude's `:=/2`, so a prelude-only goal is runnable with no load.
pub fn new_reads_disk_prelude_test() {
  let eng = engine.new()
  let assert Ok(_) = program.label_pc(engine.program(eng), ":=/2")
}

// Loading a clean program prepends the prelude and exposes the user procedure.
pub fn load_valid_adds_user_procedure_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, good_source)
  let assert Ok(_) = program.label_pc(engine.program(eng), "flip/2")
}

// T030 load-time negative #1 — SRSW violation rejects at the SRSW stage.
pub fn load_srsw_negative_rejects_test() {
  let eng = engine.new_with_prelude("")
  let assert Error(err) = engine.load(eng, srsw_negative)
  err.stage
  |> should.equal(diagnostics.SrswStage)
  err.class
  |> should.equal(diagnostics.SrswViolation)
}

// T030 load-time negative #2 — a type error rejects at the type-check stage.
pub fn load_type_negative_rejects_test() {
  let eng = engine.new_with_prelude("")
  let assert Error(err) = engine.load(eng, type_negative)
  err.stage
  |> should.equal(diagnostics.TypeCheckStage)
  err.class
  |> should.equal(diagnostics.TypeError)
}
