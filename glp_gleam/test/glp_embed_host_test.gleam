//// glp_embed minimal host-program test (feature 064, T030; US4, G3-A).
////
//// Contract: specs/064-post-wave-gap-closure/contracts/febe-split.md
//// §Embeddability — "Host contract: a minimal BEAM host program in the test
//// tree loads a project, runs a goal, receives the documented outcome. No
//// REPL, no stdin." This module IS that host: it consumes ONLY the
//// `glp_embed` public surface (load / run / observe) — no engine, repl, or
//// scheduler imports — and asserts:
////   - load(fixture .glp) + run(conjunction goal) yields the documented
////     outcome (the conjunction_query_test scenario: Success, `A = thirty`,
////     the built `person/3` struct) as typed envelope data;
////   - observe drains the run's events (ProgramOutput lines in emission
////     order, then the Outcome) and a second observe is empty;
////   - the error paths are TYPED, not crashes: a missing file →
////     SourceNotFound, a missing project directory → ProjectRejected, a
////     failed goal → a Failed envelope carrying the reference reason.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/list
import gleam/option.{Some}
import gleeunit/should
import glp/codec/result_envelope.{Failed, Success}
import glp/codec/term_codec.{ConstAtom, ConstTerm, StructTerm}
import glp_embed.{Outcome, ProgramOutput, ProjectRejected, SourceNotFound}

// The reused existing fixture (conjunction_query_test's demo): `gleam test`
// runs from `glp_gleam/`, so the repo-root program is one level up.
const struct_demo_path = "../programs/tests/typed/struct_demo.glp"

// The output-emitting host fixture (test-dir-relative from `glp_gleam/`).
const emit_fixture_path = "test/glp_embed_host_fixture.glp"

fn atom(a: String) -> term_codec.Term {
  ConstTerm(ConstAtom(a))
}

// A host reading its own prelude source (its release/priv dir in production;
// the repo copy here) — no engine/repl import, just bytes.
@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_text(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits)
    Error(_) -> Error(Nil)
  }
}

// ── the host scenario: load a program, run a goal, documented outcome ────────

// The febe-split §Embeddability host contract, end to end: the host loads the
// struct_demo fixture and runs the producer/consumer conjunction. Documented
// outcome (conjunction_query_test parity): the run succeeds, `A` resolves to
// `thirty`, and `P` is the built `person(alice, age(thirty), city(seattle))`.
pub fn host_loads_project_runs_goal_gets_documented_outcome_test() {
  let assert Ok(handle) = glp_embed.load(struct_demo_path)
  let #(_handle, env) =
    glp_embed.run(handle, "build_person(P), get_age(P?, A)")

  env.status |> should.equal(Success)
  list.key_find(env.resolved_bindings, "A")
  |> should.equal(Ok(atom("thirty")))
  list.key_find(env.resolved_bindings, "P")
  |> should.equal(
    Ok(StructTerm("person", [
      atom("alice"),
      StructTerm("age", [atom("thirty")]),
      StructTerm("city", [atom("seattle")]),
    ])),
  )
}

// ── observe: the event feed ──────────────────────────────────────────────────

// A run's events arrive in emission order — the `_output/1` line first, then
// the terminal Outcome carrying the same envelope `run` returned — and
// observe DRAINS: a second observe on the successor handle is empty.
pub fn host_observe_drains_output_then_outcome_test() {
  let assert Ok(handle) = glp_embed.load(emit_fixture_path)
  let #(handle, env) = glp_embed.run(handle, "emit(hello)")

  let #(handle, events) = glp_embed.observe(handle)
  events |> should.equal([ProgramOutput("hello"), Outcome(env)])

  let #(_handle, drained) = glp_embed.observe(handle)
  drained |> should.equal([])
}

// A fresh handle has no events before any run.
pub fn host_observe_before_any_run_is_empty_test() {
  let assert Ok(handle) = glp_embed.load(struct_demo_path)
  let #(_handle, events) = glp_embed.observe(handle)
  events |> should.equal([])
}

// ── error paths: typed errors, never a crash ─────────────────────────────────

// A missing `.glp` file is the typed SourceNotFound — the host stays up.
pub fn host_missing_file_is_typed_error_test() {
  glp_embed.load("does/not/exist.glp")
  |> should.equal(Error(SourceNotFound("does/not/exist.glp")))
}

// A missing project DIRECTORY is the typed ProjectRejected (engine.load_project's
// "no modules found" diagnostic), not a crash.
pub fn host_missing_project_dir_is_typed_error_test() {
  glp_embed.load("does/not/exist")
  |> should.equal(
    Error(ProjectRejected("does/not/exist", "no modules found in does/not/exist")),
  )
}

// The CWD-independent seam: a host that carries its own prelude source loads
// and runs WITHOUT `../programs/self.glp` being resolvable from its working
// directory — the surface never panics on the host's CWD (codexreview 064
// cycle-2 embeddability-cwd-panic).
pub fn host_supplies_its_own_prelude_test() {
  let assert Ok(prelude) = read_text("../programs/self.glp")
  let assert Ok(handle) =
    glp_embed.load_with_prelude(struct_demo_path, prelude)
  let #(_handle, env) =
    glp_embed.run(handle, "build_person(P), get_age(P?, A)")
  env.status |> should.equal(Success)
  list.key_find(env.resolved_bindings, "A")
  |> should.equal(Ok(atom("thirty")))
}

// A goal against a missing predicate surfaces as a Failed envelope carrying
// the reference reason — the run API never throws at the host.
pub fn host_failed_goal_is_failed_envelope_test() {
  let assert Ok(handle) = glp_embed.load(struct_demo_path)
  let #(_handle, env) = glp_embed.run(handle, "no_such_pred(1, 2)")
  env.status |> should.equal(Failed)
  env.error |> should.equal(Some("predicate no_such_pred/2 not found"))
}
