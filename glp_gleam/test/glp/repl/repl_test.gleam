//// REPL scripted-mode tests (feature 050, T035; US2).
////
//// Drives the REPL's testable core — `commands.parse` (the line surface) and
//// `commands.execute` (command semantics over a `Session`) — exactly as a piped
//// session would, without a live stdin: load / goal / :trace / :limit (incl.
//// exhaustion) / :quit. The full subprocess `gleam run` path is exercised by the
//// US3 corpus runner; here the semantics are pinned deterministically in-process.

import gleam/list
import gleeunit/should
import glp/engine
import glp/repl/commands.{
  Blank, Boot, BootUsage, Bytecode, Goal, LimitUsage, Load, Quit, Session,
  SetLimit, ToggleTrace,
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

// ── wave-3 T019/T020: :bytecode / :boot (contracts/repl-commands.md) ─────────

pub fn parse_bytecode_and_alias_test() {
  commands.parse(":bytecode") |> should.equal(Bytecode)
  commands.parse(":bc") |> should.equal(Bytecode)
}

pub fn parse_boot_test() {
  commands.parse(":boot play1.glp") |> should.equal(Boot("play1.glp"))
  // Dart surface: an optional trailing timeoutSec rides along (unused on the
  // synchronous instance); a bare `:boot` is the reference usage line.
  commands.parse(":boot mad_boot/play1.glp 30")
  |> should.equal(Boot("mad_boot/play1.glp"))
  commands.parse(":boot") |> should.equal(BootUsage)
}

// With nothing loaded, the reference message (Dart glp_repl.dart:205).
pub fn execute_bytecode_empty_test() {
  let #(_session, output, quit) =
    commands.execute(fresh_session(), Bytecode)
  output |> should.equal(["No programs loaded"])
  quit |> should.be_false
}

// After a load, the dump carries the per-program header and PC lines — and the
// command is READ-ONLY: the session comes back unchanged (T024, contract
// invariant 6).
pub fn execute_bytecode_dumps_and_is_readonly_test() {
  let fresh = fresh_session()
  let eng = fresh.engine
  let assert Ok(eng) =
    engine.load(
      eng,
      "m",
      "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero).",
    )
  let session = Session(..fresh, engine: eng)
  let #(session2, output, quit) = commands.execute(session, Bytecode)
  quit |> should.be_false
  // Header present…
  should.be_true(list.contains(output, "Bytecode for m:"))
  // …with at least one disassembly line beyond header+rule.
  should.be_true(list.length(output) > 3)
  // Read-only: the session value is identical (engine, trace, limit untouched).
  session2 |> should.equal(session)
}

// :boot on a missing play file reports the reference error (Dart
// glp_repl.dart:501) and leaves the session usable — never misread as a goal.
// The success-path scenario lives in boot_command_test.gleam (064 T033).
pub fn execute_boot_missing_file_test() {
  let #(session2, output, quit) =
    commands.execute(fresh_session(), Boot("does/not/exist.glp"))
  quit |> should.be_false
  output
  |> should.equal(["Error: boot file not found: does/not/exist.glp"])
  // Session stays usable: a goal still runs afterwards.
  let #(_s, out2, _) = commands.execute(session2, Goal("X := 1+1"))
  should.be_true(list.contains(out2, "X = 2"))
}

// A bare `:boot` prints the reference usage line (Dart glp_repl.dart:192).
pub fn execute_boot_usage_test() {
  let #(_session, output, quit) = commands.execute(fresh_session(), BootUsage)
  quit |> should.be_false
  output |> should.equal(["Usage: :boot <bootfile.glp> [timeoutSec]"])
}

// ── wave-3 T021: an unknown procedure leaves the session usable ──────────────

pub fn execute_unknown_procedure_leaves_session_usable_test() {
  let session = fresh_session()
  let #(session2, out1, quit) =
    commands.execute(session, Goal("no_such_pred(1)"))
  quit |> should.be_false
  should.be_true(list.contains(out1, "→ failed"))
  // The next command on the SAME session works (contract invariant 1).
  let #(_s, out2, _) = commands.execute(session2, Goal("X := 2+2"))
  should.be_true(list.contains(out2, "X = 4"))
}
