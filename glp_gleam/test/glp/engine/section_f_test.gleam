//// Section F oracle (wave-3 T009): the CSSG modules project loaded through
//// the STATIC LINKER — the same cases the Dart REPL suite's Section F runs
//// (test/cssg_modules_test.sh): project-directory load succeeds, silent plays
//// 1–7 reach Success or Suspended, and the tagged fplays emit their
//// `tagged(Id, …)` output lines through `_output/1`.
////
//// Dart reference mechanism: `GlpEngine.loadProject` (glp_engine.dart:331) —
//// discover → per-module ancestor-scoped type check → link flat → compile.
//// Sources are read from `programs/cssg_modules/` on disk, exactly the files
//// Section F loads (no embedded copies).

import gleam/int
import gleam/list
import gleam/string
import gleeunit/should
import glp/codec/result_envelope
import glp/engine

const cssg_dir = "../programs/cssg_modules"

fn cssg_engine() -> engine.Engine {
  let assert Ok(e) = engine.load_project(engine.new(), cssg_dir)
  e
}

// Project loads: discovery + per-module type check + link + compile all green.
pub fn cssg_project_loads_test() {
  let _e = cssg_engine()
  Nil
}

// Silent plays 1–7 run to Success or Suspended (Section F's
// `check "playN succeeds" "succeeds\|suspended"`).
pub fn cssg_silent_plays_test() {
  let e = cssg_engine()
  list.each([1, 2, 3, 4, 5, 6, 7], fn(n) {
    let #(_e, env) = engine.run(e, "play" <> int.to_string(n))
    case env.status {
      result_envelope.Success | result_envelope.Suspended -> Nil
      other ->
        panic as {
          "play"
          <> int.to_string(n)
          <> " expected Success|Suspended, got "
          <> string.inspect(other)
        }
    }
  })
}

fn run_fplay(name: String) -> List(String) {
  let #(_e, env, output) =
    engine.run_with_limit_capturing(cssg_engine(), name, 1_000_000)
  case env.status {
    result_envelope.Success | result_envelope.Suspended -> Nil
    other ->
      panic as {
        name <> " expected Success|Suspended, got " <> string.inspect(other)
      }
  }
  output
}

// fplay1: both accept the intro — Alice↔Charlie connect through Bob
// (Section F: "tagged(alice.*connected(bob)" / "tagged(charlie.*connected(alice)").
pub fn cssg_fplay1_tagged_output_test() {
  let out = string.join(run_fplay("fplay1"), "\n")
  string.contains(out, "tagged(alice") |> should.be_true
  string.contains(out, "connected(bob") |> should.be_true
  string.contains(out, "tagged(charlie") |> should.be_true
  string.contains(out, "connected(alice") |> should.be_true
}

// fplay2: Alice accepts, Charlie rejects (Section F: "tagged(alice.*rejected").
pub fn cssg_fplay2_rejected_output_test() {
  let out = string.join(run_fplay("fplay2"), "\n")
  string.contains(out, "tagged(alice") |> should.be_true
  string.contains(out, "rejected") |> should.be_true
}

// fplay4: CSSG all-accept — Carol and Dave become friends
// (Section F: "tagged(carol.*connected(dave)").
pub fn cssg_fplay4_carol_dave_test() {
  let out = string.join(run_fplay("fplay4"), "\n")
  string.contains(out, "tagged(carol") |> should.be_true
  string.contains(out, "connected(dave") |> should.be_true
}
