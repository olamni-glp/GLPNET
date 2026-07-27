//// Engine-as-typed-value facade tests (feature 050, T029 Slice 1).
////
//// The facade wires construction (`new`/`new_with_prelude`) + `load` over the
//// loader pipeline (contracts/gleam-instance-surface.md Â§"Engine as typed value"):
//// a fresh engine's runnable program is the prelude alone; `load` prepends user
//// code (user labels win); staged rejections propagate unchanged. The load-time
//// negatives here are the SRSW-neg + type-neg half of T030's smoke set.

import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/analysis/type_ast.{Pos}
import glp/bytecode/program
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/diagnostics
import glp/engine
import glp/engine/goal_boot
import glp/link/seam/link_scheme
import glp/link/seam/transport
import glp/link/transports/loopback
import glp/parser/ast
import glp/runtime/heap
import glp/runtime/terms

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
  let assert Ok(eng) = engine.load(eng, "good", good_source)
  let assert Ok(_) = program.label_pc(engine.program(eng), "flip/2")
}

// T030 load-time negative #1 â€” SRSW violation rejects at the SRSW stage.
pub fn load_srsw_negative_rejects_test() {
  let eng = engine.new_with_prelude("")
  let assert Error(err) = engine.load(eng, "srsw_neg", srsw_negative)
  err.stage
  |> should.equal(diagnostics.SrswStage)
  err.class
  |> should.equal(diagnostics.SrswViolation)
}

// T030 load-time negative #2 â€” a type error rejects at the type-check stage.
pub fn load_type_negative_rejects_test() {
  let eng = engine.new_with_prelude("")
  let assert Error(err) = engine.load(eng, "type_neg", type_negative)
  err.stage
  |> should.equal(diagnostics.TypeCheckStage)
  err.class
  |> should.equal(diagnostics.TypeError)
}

// â”€â”€ T030: X := 2+3 end-to-end through the engine API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// The headline parity gate: a prelude-only arithmetic goal runs against the real
// on-disk self.glp (`:=/2` + the `_add` kernel), reducing to Success with X = 5,
// reported as a deep-resolved binding â€” Dart-identical.
pub fn run_assignment_binds_result_test() {
  let #(_eng, env) = engine.run(engine.new(), "X := 2+3")

  env.status
  |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("X", term_codec.ConstTerm(term_codec.ConstInt(5)))])
  env.var_to_writer
  |> should.equal([])
  env.suspended
  |> should.equal([])
}

// A goal that reads an unbound input suspends: status Suspended, no bindings, the
// produced query var (Out) reported as a varâ†’writer, exactly one blocking reader.
pub fn run_suspended_goal_reports_suspension_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "good", good_source)
  let #(_eng, env) = engine.run(eng, "flip(In?, Out)")

  env.status
  |> should.equal(result_envelope.Suspended)
  env.resolved_bindings
  |> should.equal([])
  let assert [#("Out", _)] = env.var_to_writer
  let assert [_blocking_reader] = env.suspended
}

// A missing predicate is a Failed envelope carrying the reason (not a crash).
pub fn run_unknown_predicate_fails_test() {
  let #(_eng, env) = engine.run(engine.new(), "no_such_pred(1, 2)")
  env.status
  |> should.equal(result_envelope.Failed)
  let assert Some(_reason) = env.error
}

// â”€â”€ goal-boot: proper list-of-consts materialises to a cons chain â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// [1, 2, 3] in argument position builds `.(1, .(2, .(3, nil)))` bound to one
// writer, with const heads placed inline (Dart `_buildListTerm`).
pub fn goal_boot_builds_const_list_test() {
  let p = Pos(0, 0)
  let ci = fn(n) { ast.ConstTerm(terms.ConstInt(n), p) }
  let list123 =
    ast.ListTerm(
      Some(ci(1)),
      Some(ast.ListTerm(
        Some(ci(2)),
        Some(ast.ListTerm(Some(ci(3)), None, p)),
        p,
      )),
      p,
    )
  let atom = ast.Atom("foo", [list123], p)

  let assert Ok(boot) = goal_boot.setup_goal(heap.new(), atom)
  let assert Ok(terms.VarRef(addr)) = program.get_reg(boot.regs, 0)
  let assert Ok(#(_, heap.Bound(cell))) = heap.deref(boot.heap, addr)
  // Head is the inline constant 1; tail is a further cons (structural check).
  let assert terms.StructTerm(".", [terms.ConstTerm(terms.ConstInt(1)), _tail]) =
    cell
}

// â”€â”€ transport injection seam (wave-3 T007/T008, gap G6) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// A fresh engine holds no transports; injecting loopback makes exactly that
// scheme resolvable through the composition root, and an uninjected scheme
// (tcp) reports Error(Nil) â€” never auto-instantiated.
pub fn transport_injection_seam_test() {
  let eng = engine.new_with_prelude("")
  should.equal(engine.transports(eng), [])
  let assert Error(Nil) = engine.transport_for(eng, link_scheme.loopback())

  let eng2 = engine.with_transports(eng, [loopback.new()])
  let assert Ok(t) = engine.transport_for(eng2, link_scheme.loopback())
  should.be_true(transport.serves(t, link_scheme.loopback()))
  let assert Error(Nil) = engine.transport_for(eng2, link_scheme.tcp())
}

// with_transports REPLACES the injected set (composition-root semantics: the
// set is assembled once, in one place â€” not accumulated behind the caller's back).
pub fn with_transports_replaces_test() {
  let eng =
    engine.new_with_prelude("")
    |> engine.with_transports([loopback.new()])
    |> engine.with_transports([])
  should.equal(engine.transports(eng), [])
  let assert Error(Nil) = engine.transport_for(eng, link_scheme.loopback())
}

// â”€â”€ re-load replacement (wave-3 T012, FR-015) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

const v2_source = "Bit ::= zero ; one.
procedure flop(Bit?, Bit).
flop(zero, one).
flop(one, zero)."

// Re-loading the SAME name replaces its definitions: after v2 (flop/2 only),
// v1's flip/2 label is gone from the runnable program â€” stale definitions are
// unreachable (Dart `_loadedPrograms[name] = program` map overwrite).
pub fn reload_same_name_replaces_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "m", good_source)
  let assert Ok(_) = program.label_pc(engine.program(eng), "flip/2")
  let assert Ok(eng) = engine.load(eng, "m", v2_source)
  let assert Ok(_) = program.label_pc(engine.program(eng), "flop/2")
  program.label_pc(engine.program(eng), "flip/2")
  |> should.equal(Error(Nil))
}

// Distinct names still ACCUMULATE (the multi-file corpus session semantics):
// both files' procedures stay reachable.
pub fn distinct_names_accumulate_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "m1", good_source)
  let assert Ok(eng) = engine.load(eng, "m2", v2_source)
  let assert Ok(_) = program.label_pc(engine.program(eng), "flip/2")
  let assert Ok(_) = program.label_pc(engine.program(eng), "flop/2")
}

// â”€â”€ T014: SRSW rejection names the variable and the clause (FR-005) â”€â”€â”€â”€â”€â”€â”€â”€â”€

// The staged diagnostic carries the offending variable name AND the clause
// identity (procedure signature + line) â€” Dart `sig: Line N: â€¦` format.
pub fn srsw_error_names_variable_and_clause_test() {
  let eng = engine.new_with_prelude("")
  let assert Error(err) = engine.load(eng, "srsw_neg", srsw_negative)
  should.be_true(string.contains(err.reason, "dup/3"))
  should.be_true(string.contains(err.reason, "\"X\""))
  should.be_true(string.contains(err.reason, "Line "))
}

// â”€â”€ T015: a failed load leaves the engine usable, prior program intact â”€â”€â”€â”€â”€â”€

pub fn failed_load_leaves_engine_usable_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "good", good_source)
  // A rejected load returns Error and MUST NOT damage the engine value â€¦
  let assert Error(_) = engine.load(eng, "bad", srsw_negative)
  // â€¦ so the prior program still loads goals and runs (FR-003).
  let assert Ok(_) = program.label_pc(engine.program(eng), "flip/2")
  let #(_eng, env) = engine.run(eng, "flip(zero, Out)")
  env.status
  |> should.equal(result_envelope.Success)
}

// â”€â”€ T018: suspension is reported distinctly from failure (FR-006) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

// A suspended goal: status Suspended AND no error. A failed goal: status Failed
// AND an error present. The two outcomes are never conflated.
pub fn suspension_is_distinct_from_failure_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "good", good_source)
  let #(_e1, suspended) = engine.run(eng, "flip(In?, Out)")
  suspended.status
  |> should.equal(result_envelope.Suspended)
  suspended.error
  |> should.equal(None)

  let #(_e2, failed) = engine.run(eng, "no_such(1)")
  failed.status
  |> should.equal(result_envelope.Failed)
  let assert Some(_) = failed.error
}

// â”€â”€ T018a: writer-MGU polarity survives the registry-rebuilt program â”€â”€â”€â”€â”€â”€â”€â”€

// After a re-load through the loaded-programs registry (T012's rebuild path),
// a goal still binds ONLY its writer: the result arrives as a resolved binding
// on the query writer, with no reader bound and no writer-to-writer binding
// (FR-007; closes analyze finding C1 at the loader level â€” the ModuleTerm-level
// re-verify belongs to the dispatch subsystem work).
pub fn writer_mgu_survives_reload_rebuild_test() {
  let eng = engine.new_with_prelude("")
  let assert Ok(eng) = engine.load(eng, "m", v2_source)
  let assert Ok(eng) = engine.load(eng, "m", good_source)
  let #(_eng, env) = engine.run(eng, "flip(zero, Out)")
  env.status
  |> should.equal(result_envelope.Success)
  // Out (the writer) carries the binding; nothing else was bound.
  env.resolved_bindings
  |> should.equal([
    #("Out", term_codec.ConstTerm(term_codec.ConstAtom("one"))),
  ])
  env.var_to_writer
  |> should.equal([])
  env.suspended
  |> should.equal([])
}
