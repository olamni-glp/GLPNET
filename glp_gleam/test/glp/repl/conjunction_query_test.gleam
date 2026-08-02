//// Conjunction-query REPL parity tests (feature 062, US5 follow-up).
////
//// The Gleam REPL previously accepted only a single goal per line; a conjunction
//// query with a top-level comma (`build_person(P), get_age(P?, A).`) failed at the
//// comma because a bare `a, b.` is not a valid clause head. The Dart REPL
//// (`glp_runtime/lib/engine/glp_engine.dart` `runGoal` → `_runConjunction`) splits
//// such a line into its conjuncts and boots them over ONE shared heap so the goals
//// communicate through shared logic variables. These tests pin that parity:
////   - the line surface still hands the whole conjunction to the goal path;
////   - a producer/consumer conjunction binds the consumed query variable;
////   - a missing predicate in a conjunction fails the reference way;
////   - a single goal is UNCHANGED (the unified path is byte-identical for arity-1).

import gleeunit/should
import gleam/list
import glp/engine
import glp/repl/commands.{Goal, Load, Session}

fn fresh_session() -> commands.Session {
  Session(engine: engine.new(), trace: False, limit: 1_000_000)
}

// The demo predicates live at repo root; `gleam test` runs from `glp_gleam/`, so
// the path is one level up (same convention as the prelude at `../programs/self.glp`).
const struct_demo_path = "../programs/tests/typed/struct_demo.glp"

fn loaded_session() -> commands.Session {
  let #(session, output, _quit) =
    commands.execute(fresh_session(), Load(struct_demo_path))
  // Guard: if the demo failed to load, the conjunction assertions below would be
  // meaningless — assert the load succeeded first.
  should.equal(output, ["✓ Loaded: " <> struct_demo_path])
  session
}

// ── line surface ─────────────────────────────────────────────────────────────

// The parser keeps the whole conjunction as one Goal string (the top-level comma is
// NOT a command separator) — the engine splits it into conjuncts downstream.
pub fn parse_conjunction_is_single_goal_line_test() {
  commands.parse("build_person(P), get_age(P?, A).")
  |> should.equal(Goal("build_person(P), get_age(P?, A)"))
}

// ── end-to-end conjunction ─────────────────────────────────────────────────────

// The task's parity scenario: goal 1 produces `P`, goal 2 consumes `P?` and binds
// `A`. Both goals share the same `P`, so `A` resolves to `thirty`.
pub fn conjunction_producer_consumer_binds_shared_var_test() {
  let #(_session, output, _quit) =
    commands.execute(
      loaded_session(),
      Goal("build_person(P), get_age(P?, A)"),
    )
  list.contains(output, "→ succeeds") |> should.be_true
  list.contains(output, "A = thirty") |> should.be_true
  list.contains(output, "P = person(alice, age(thirty), city(seattle))")
  |> should.be_true
}

// Dart parity (glp_engine.dart:693,740): a CONSUMER-first conjunction —
// `get_age(P?, A)` before its producer `build_person(P)` — drains goal 1 into a
// suspend on the unbound `P?`, then goal 2 binds `P` and wakes goal 1 so `A` still
// binds to `thirty`. Because an earlier drain suspended, the reference latches the
// aggregate status to `suspended` even though every goal ultimately completes.
pub fn conjunction_consumer_first_reports_suspended_test() {
  let #(_session, output, _quit) =
    commands.execute(
      loaded_session(),
      Goal("get_age(P?, A), build_person(P)"),
    )
  list.contains(output, "A = thirty") |> should.be_true
  list.contains(output, "→ suspended") |> should.be_true
}

// A three-way conjunction also threads shared variables across every conjunct.
pub fn conjunction_three_goals_binds_all_test() {
  let #(_session, output, _quit) =
    commands.execute(
      loaded_session(),
      Goal("build_person(P), get_age(P?, A), get_city(P?, C)"),
    )
  list.contains(output, "→ succeeds") |> should.be_true
  list.contains(output, "A = thirty") |> should.be_true
  list.contains(output, "C = seattle") |> should.be_true
}

// A missing predicate anywhere in the conjunction fails the reference way — the
// same "predicate …/… not found" error the single-goal path reports.
pub fn conjunction_missing_predicate_fails_test() {
  let #(_session, output, _quit) =
    commands.execute(
      loaded_session(),
      Goal("build_person(P), no_such_pred(P?, A)"),
    )
  list.contains(output, "→ failed") |> should.be_true
  list.contains(output, "Error: predicate no_such_pred/2 not found")
  |> should.be_true
}

// ── single-goal regression (the unified path must not change arity-1 behaviour) ─

pub fn single_goal_unchanged_test() {
  let #(_session, output, _quit) =
    commands.execute(fresh_session(), Goal("X := 2+3"))
  output |> should.equal(["X = 5", "→ succeeds", ""])
}
