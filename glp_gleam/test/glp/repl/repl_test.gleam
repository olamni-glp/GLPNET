//// REPL scripted-mode tests (feature 050, T035; US2).
////
//// Drives the REPL's testable core — `commands.parse` (the line surface) and
//// `commands.execute` (command semantics over a `Session`) — exactly as a piped
//// session would, without a live stdin: load / goal / :trace / :limit (incl.
//// exhaustion) / :quit. The full subprocess `gleam run` path is exercised by the
//// US3 corpus runner; here the semantics are pinned deterministically in-process.

import gleeunit/should
import glp/engine
import glp/repl/commands.{
  Blank, Goal, LimitUsage, Load, Quit, Session, SetLimit, ToggleTrace,
}

fn fresh_session() -> commands.Session {
  Session(engine: engine.new(), trace: False, limit: 1_000_000)
}

// ── parse: the line surface ──────────────────────────────────────────────────

pub fn parse_goal_strips_trailing_dot_test() {
  commands.parse("foo(X).")
  |> should.equal(Goal("foo(X)"))
}

pub fn parse_bare_glp_path_is_load_test() {
  commands.parse("merge.glp")
  |> should.equal(Load("merge.glp"))
}

pub fn parse_load_prefix_strips_and_dequotes_test() {
  commands.parse("load \"programs/x.glp\"")
  |> should.equal(Load("programs/x.glp"))
}

pub fn parse_quit_test() {
  commands.parse(":quit") |> should.equal(Quit)
  commands.parse(":q") |> should.equal(Quit)
}

pub fn parse_trace_test() {
  commands.parse(":trace") |> should.equal(ToggleTrace)
}

pub fn parse_limit_valid_test() {
  commands.parse(":limit 500")
  |> should.equal(SetLimit(500))
}

pub fn parse_limit_missing_arg_is_usage_test() {
  commands.parse(":limit")
  |> should.equal(LimitUsage("Usage: :limit <number>"))
}

pub fn parse_limit_non_positive_is_error_test() {
  commands.parse(":limit 0")
  |> should.equal(LimitUsage("Error: limit must be a positive integer"))
}

pub fn parse_blank_test() {
  commands.parse("   ") |> should.equal(Blank)
}

// ── execute: command semantics ───────────────────────────────────────────────

// A goal runs end-to-end through the engine and renders the reference outcome
// block (binding line, status, trailing blank).
pub fn execute_goal_renders_outcome_test() {
  let #(_session, output, quit) =
    commands.execute(fresh_session(), Goal("X := 2+3"))
  output |> should.equal(["X = 5", "→ succeeds", ""])
  quit |> should.be_false
}

// :trace toggles the flag and prints the reference message.
pub fn execute_trace_toggles_test() {
  let #(session, output, _) = commands.execute(fresh_session(), ToggleTrace)
  session.trace |> should.be_true
  output |> should.equal(["Trace enabled"])
  let #(session2, output2, _) = commands.execute(session, ToggleTrace)
  session2.trace |> should.be_false
  output2 |> should.equal(["Trace disabled"])
}

// :limit sets the reduction limit and prints the reference message.
pub fn execute_limit_sets_and_reports_test() {
  let #(session, output, _) = commands.execute(fresh_session(), SetLimit(42))
  session.limit |> should.equal(42)
  output |> should.equal(["Goal reduction limit set to 42"])
}

// A goal run under an exhausting limit fails the reference way.
pub fn execute_limit_exhaustion_fails_test() {
  let session = Session(..fresh_session(), limit: 1)
  let #(_session, output, _) = commands.execute(session, Goal("X := 2+3"))
  output
  |> should.equal(["X = 5", "→ failed", "Error: reduction fuel exhausted", ""])
}

// :quit signals exit 0 with the reference farewell.
pub fn execute_quit_signals_exit_test() {
  let #(_session, output, quit) = commands.execute(fresh_session(), Quit)
  output |> should.equal(["Goodbye!"])
  quit |> should.be_true
}

// A missing load path reports the reference file-not-found error, engine unchanged.
pub fn execute_load_missing_file_test() {
  let #(_session, output, quit) =
    commands.execute(fresh_session(), Load("does/not/exist.glp"))
  output |> should.equal(["Error loading does/not/exist.glp: File not found"])
  quit |> should.be_false
}

// A blank line is a no-op (no output, no exit).
pub fn execute_blank_is_noop_test() {
  let #(_session, output, quit) = commands.execute(fresh_session(), Blank)
  output |> should.equal([])
  quit |> should.be_false
}
