//// glp/repl/repl — the REPL loop + `gleam run` entry (feature 050, T031; US2).
////
//// Scripted, newline-fed stdin with a non-interactive EOF exit (the corpus runner
//// pipes `load …` + goals and reads the outcome block). The engine is built once
//// (reading programs/self.glp); the `Session` (engine + trace + limit) threads
//// across lines. Line dispatch + command semantics live in glp/repl/commands (kept
//// separate so they stay unit-testable without a live stdin); this module is the
//// thin IO shell: read a line, execute, print. `main` (glp_gleam) calls `run`.

import gleam/io
import gleam/list
import glp/engine
import glp/repl/commands.{type Session, Session}

/// Default reduction limit until `:limit <n>` overrides it (engine `default_fuel`).
const default_limit = 1_000_000

/// Build a fresh session (the engine reads the on-disk prelude) and run the read
/// loop to EOF. The banner/prompt are non-parity noise (the corpus runner
/// normalizes them); the `✓ Loaded …`, binding, and `→ status` lines are the
/// parity-bearing output.
pub fn run() -> Nil {
  let session =
    Session(engine: engine.new(), trace: False, limit: default_limit)
  io.println("GLP (gleam) — type a goal or `load <file>.glp`; :quit to exit")
  loop(session)
}

fn loop(session: Session) -> Nil {
  io.print("GLP> ")
  case read_line() {
    // EOF → non-interactive exit (exit 0 via `main` returning).
    Error(_) -> Nil
    Ok(line) -> {
      let #(session, output, quit) =
        commands.execute(session, commands.parse(line))
      list.each(output, io.println)
      case quit {
        True -> Nil
        False -> loop(session)
      }
    }
  }
}

@external(erlang, "glp_repl_ffi", "read_line")
fn read_line() -> Result(String, Nil)
