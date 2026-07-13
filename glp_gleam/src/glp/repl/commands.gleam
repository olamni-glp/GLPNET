//// glp/repl/commands — REPL command surface + semantics (feature 050, T032; US2).
////
//// The reference REPL's command handling (Dart `glp_runtime/bin/glp_repl.dart`),
//// for the contract's named set (contracts/gleam-instance-surface.md §"REPL
//// commands"): `load <path>` / bare `<path>.glp`, `<goal>.`, `:trace`, `:limit
//// <n>`, `:quit`. `parse` is pure (the parse surface); `execute` threads a
//// `Session` (the immutable engine + trace flag + reduction limit) and returns the
//// output lines plus a quit flag. Goal results are rendered from the 038
//// `ResultEnvelope` (glp/repl/results) — the ED-1 in-process seam.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/int
import gleam/list
import gleam/string
import glp/engine.{type Engine}
import glp/repl/results

/// A parsed REPL line (the contract's command set; unrecognized input is a Goal,
/// matching the Dart fall-through to `runGoal`).
pub type Command {
  Quit
  ToggleTrace
  SetLimit(Int)
  /// A malformed `:limit` — carries the exact reference message to print.
  LimitUsage(String)
  /// A `.glp` load — path with any `load ` prefix and surrounding quotes stripped.
  Load(String)
  /// A goal to execute (trailing `.` already stripped).
  Goal(String)
  /// An empty line — a no-op (Dart `continue`).
  Blank
}

/// The mutable REPL state threaded across lines: the immutable engine, the trace
/// toggle, and the current reduction limit (Dart `engine.maxCycles`).
pub type Session {
  Session(engine: Engine, trace: Bool, limit: Int)
}

/// Parse one input line into a `Command`. Mirrors the Dart pre-processing: trim,
/// then strip a single trailing `.` UNLESS the line ends in `.glp` (so `foo(X).`
/// becomes the goal `foo(X)` but a bare `merge.glp` load is untouched).
pub fn parse(line: String) -> Command {
  let trimmed = string.trim(line)
  case trimmed {
    "" -> Blank
    _ -> {
      let dedotted = case
        string.ends_with(trimmed, ".") && !string.ends_with(trimmed, ".glp")
      {
        True -> string.trim(string.drop_end(trimmed, 1))
        False -> trimmed
      }
      classify(dedotted)
    }
  }
}

fn classify(t: String) -> Command {
  case t {
    ":quit" | ":q" -> Quit
    ":trace" | ":t" -> ToggleTrace
    _ ->
      case string.starts_with(t, ":limit") {
        True -> parse_limit(t)
        False -> classify_load_or_goal(t)
      }
  }
}

fn parse_limit(t: String) -> Command {
  let parts = t |> string.split(" ") |> list.filter(fn(s) { s != "" })
  case parts {
    [":limit", n_str] ->
      case int.parse(n_str) {
        Ok(n) if n > 0 -> SetLimit(n)
        _ -> LimitUsage("Error: limit must be a positive integer")
      }
    _ -> LimitUsage("Usage: :limit <number>")
  }
}

fn classify_load_or_goal(t: String) -> Command {
  case string.starts_with(t, "load ") {
    True -> Load(strip_quotes(string.trim(string.drop_start(t, 5))))
    False ->
      case string.ends_with(t, ".glp") {
        True -> Load(strip_quotes(t))
        False -> Goal(t)
      }
  }
}

fn strip_quotes(s: String) -> String {
  let n = string.length(s)
  case n >= 2 {
    False -> s
    True ->
      case
        { string.starts_with(s, "\"") && string.ends_with(s, "\"") }
        || { string.starts_with(s, "'") && string.ends_with(s, "'") }
      {
        True -> string.slice(s, 1, n - 2)
        False -> s
      }
  }
}

/// Execute a command against the session. Returns the updated session, the output
/// lines to print, and whether the REPL should exit (Dart `break`). Goal output
/// carries a trailing blank line (Dart prints `''` after each goal block).
pub fn execute(
  session: Session,
  command: Command,
) -> #(Session, List(String), Bool) {
  case command {
    Blank -> #(session, [], False)
    Quit -> #(session, ["Goodbye!"], True)
    ToggleTrace -> {
      let trace = !session.trace
      let msg = case trace {
        True -> "Trace enabled"
        False -> "Trace disabled"
      }
      #(Session(..session, trace: trace), [msg], False)
    }
    SetLimit(n) -> #(
      Session(..session, limit: n),
      ["Goal reduction limit set to " <> int.to_string(n)],
      False,
    )
    LimitUsage(msg) -> #(session, [msg], False)
    Load(path) -> execute_load(session, path)
    Goal(text) -> {
      let #(_engine, env) =
        engine.run_with_limit(session.engine, text, session.limit)
      #(session, list.append(results.render_outcome(env), [""]), False)
    }
  }
}

fn execute_load(
  session: Session,
  path: String,
) -> #(Session, List(String), Bool) {
  case read_source(path) {
    Error(_) -> #(
      session,
      ["Error loading " <> path <> ": File not found"],
      False,
    )
    Ok(source) ->
      case engine.load(session.engine, source) {
        Ok(engine) -> #(
          Session(..session, engine: engine),
          ["✓ Loaded: " <> path],
          False,
        )
        Error(staged) -> #(
          session,
          ["Error loading " <> path <> ": " <> string.inspect(staged)],
          False,
        )
      }
  }
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits)
    Error(_) -> Error(Nil)
  }
}
