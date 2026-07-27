//// glp/link/link_repl — the link-aware scripted REPL entry (feature 059, T074).
////
//// The analogue of glp/repl/repl for the LINK DRIVER: it loads a `.glp` file and runs
//// each goal under `engine.run_link` (the T074 pump) rather than the pure `engine.run`,
//// so a two-process harness can rendezvous two Gleam instances over TCP and round-trip
//// the acceptance link programs (bidi/pathb/mon/sr/pc). Invoked with
//// `gleam run -m glp/link/link_repl`.
////
//// Reuses the reference REPL's line classification (`commands.parse`) and outcome
//// rendering (`results.render_outcome`) so the printed `Loaded` / binding / `→ status`
//// lines match the ordinary REPL exactly — the harness greps the binding line
//// (`Got = [10, 20, 30]`) just as run_link_tests_dart.sh does against the Dart REPL.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/io
import gleam/list
import gleam/string
import glp/engine.{type Engine}
import glp/repl/commands.{type Command, Blank, Goal, Load, Quit, SetLimit}
import glp/repl/results

const default_limit = 1_000_000

type Session {
  Session(engine: Engine, limit: Int)
}

pub fn main() -> Nil {
  let session = Session(engine: engine.new(), limit: default_limit)
  io.println("GLP (gleam link) — link-driver REPL; :quit to exit")
  loop(session)
}

fn loop(session: Session) -> Nil {
  case read_line() {
    Error(_) -> Nil
    Ok(line) -> {
      let #(session, output, quit) = execute(session, commands.parse(line))
      list.each(output, io.println)
      case quit {
        True -> Nil
        False -> loop(session)
      }
    }
  }
}

fn execute(session: Session, command: Command) -> #(Session, List(String), Bool) {
  case command {
    Quit -> #(session, ["Goodbye!"], True)
    Blank -> #(session, [], False)
    SetLimit(n) -> #(Session(..session, limit: n), [], False)
    Load(path) -> execute_load(session, path)
    Goal(text) -> {
      // The link driver: run the goal with the pump + transports.
      let #(_engine, env, output) =
        engine.run_link(session.engine, text, session.limit)
      #(session, list.flatten([output, results.render_outcome(env), [""]]), False)
    }
    // The link REPL ignores :trace / usage lines (non-parity noise).
    _ -> #(session, [], False)
  }
}

fn execute_load(session: Session, path: String) -> #(Session, List(String), Bool) {
  case read_source(path) {
    Error(_) -> #(session, ["Error loading " <> path <> ": File not found"], False)
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

@external(erlang, "glp_repl_ffi", "read_line")
fn read_line() -> Result(String, Nil)

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> Result(String, Nil) {
  case read_file(path) {
    Ok(bits) -> bit_array.to_string(bits)
    Error(_) -> Error(Nil)
  }
}
