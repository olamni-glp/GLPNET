//// T011 + T016 (wave-3, US1): explicit cross-module resolution cases.
////
//// FR-008: "The runtime MUST resolve references across module boundaries,
//// including modules loaded after the referring module." T011's contract is
//// the disjunction "a module referenced before it is loaded must resolve when
//// it arrives, OR yield a structured error at first call". The Gleam runtime
//// satisfies both arms exactly as the Dart reference does: dynamic dispatch
//// resolves at CALL time (activations replay at every goal boot, so any
//// module loaded before the run — in ANY order — resolves), and a module
//// never loaded yields the reference's structured hard error at the first
//// dispatched call (Dart runner: "module M not activated (no GLP channel)" →
//// RunResult.terminated; here → Errored → Failed envelope naming the module).
////
//// T016 adds the explicit multi-module-load and duplicate-procedure cases
//// (first-load wins — Dart `combinedProgram` insertion order).

import gleam/option.{Some}
import gleam/string
import gleeunit/should
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/engine

const math_service_source = "-module(math_service).

exported procedure double(Integer?, Integer).
double(X, Y?) :- Y := X? * 2.
"

const dispatch_client_source = "-module(dispatch_client).

imported procedure math_service#double(Integer?, Integer).

exported procedure test_double(Integer?, Integer).
test_double(X, Y?) :- math_service # double(X?, Y).
"

// ── T011 arm 1: the referenced module arrives AFTER the referring module ─────

// dispatch_client (the referring module) loads FIRST; math_service arrives
// second. The call still resolves — dispatch is call-time, not load-time.
pub fn late_resolution_module_arrives_after_referrer_test() {
  let e = engine.new()
  let assert Ok(e) = engine.load(e, "dispatch_client", dispatch_client_source)
  let assert Ok(e) = engine.load(e, "math_service", math_service_source)
  let #(_e, env) = engine.run(e, "test_double(5, X)")
  env.status |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("X", term_codec.ConstTerm(term_codec.ConstInt(10)))])
}

// ── T011 arm 2: a module never loaded yields a structured error at first call ─

// Only the referring module is loaded. The first dispatched call surfaces the
// reference's structured error naming the module — never a silent no-op.
pub fn unloaded_module_structured_error_at_first_call_test() {
  let e = engine.new()
  let assert Ok(e) = engine.load(e, "dispatch_client", dispatch_client_source)
  let #(_e, env) = engine.run(e, "test_double(5, X)")
  env.status |> should.equal(result_envelope.Failed)
  let assert Some(reason) = env.error
  string.contains(reason, "math_service") |> should.be_true
  string.contains(reason, "not activated") |> should.be_true
}

// ── T016: multi-module load — distinct files accumulate, both callable ───────
// The reference's Section-A co-load shape (corpus a1: several files loaded into
// one session, goals run against each): every file type-checks INDEPENDENTLY
// against the prelude — direct cross-file calls go through `#` dispatch or the
// project linker (both tested elsewhere), never through bare co-load scope.

const helpers_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

const caller_source = "Tone ::= high ; low.
procedure swap(Tone?, Tone).
swap(high, low).
swap(low, high)."

pub fn multi_module_load_accumulates_test() {
  let e = engine.new_with_prelude("")
  let assert Ok(e) = engine.load(e, "helpers", helpers_source)
  let assert Ok(e) = engine.load(e, "caller", caller_source)
  // Goals against BOTH loaded files run in the same session.
  let #(e, env1) = engine.run(e, "flip(zero, R)")
  env1.status |> should.equal(result_envelope.Success)
  let assert [#("R", term_codec.ConstTerm(term_codec.ConstAtom("one")))] =
    env1.resolved_bindings
  let #(_e, env2) = engine.run(e, "swap(high, T)")
  env2.status |> should.equal(result_envelope.Success)
  let assert [#("T", term_codec.ConstTerm(term_codec.ConstAtom("low")))] =
    env2.resolved_bindings
}

// ── T016: duplicate-procedure resolution — first-load wins ───────────────────

const pick_a = "Bit ::= zero ; one.
procedure pick(Bit).
pick(zero)."

const pick_b = "Bit ::= zero ; one.
procedure pick(Bit).
pick(one)."

// Two files define pick/1 differently: the EARLIER-loaded definition wins
// (Dart `combinedProgram` insertion order — first label occurrence).
pub fn duplicate_procedure_first_load_wins_test() {
  let e = engine.new_with_prelude("")
  let assert Ok(e) = engine.load(e, "a", pick_a)
  let assert Ok(e) = engine.load(e, "b", pick_b)
  let #(_e, env) = engine.run(e, "pick(P)")
  env.status |> should.equal(result_envelope.Success)
  let assert [#("P", term_codec.ConstTerm(term_codec.ConstAtom("zero")))] =
    env.resolved_bindings
}

// And a RE-load of the SAME name replaces in place (FR-015/T012 interplay):
// re-loading "a" with the pick_b definition makes `one` the answer — the stale
// definition is unreachable.
pub fn duplicate_reload_same_name_replaces_test() {
  let e = engine.new_with_prelude("")
  let assert Ok(e) = engine.load(e, "a", pick_a)
  let assert Ok(e) = engine.load(e, "a", pick_b)
  let #(_e, env) = engine.run(e, "pick(P)")
  env.status |> should.equal(result_envelope.Success)
  let assert [#("P", term_codec.ConstTerm(term_codec.ConstAtom("one")))] =
    env.resolved_bindings
}
