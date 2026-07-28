//// T078 residual #1a — the engine FACADE auto-activates exported modules so the REPL
//// routes `#` calls end to end (Dart §19.8). Loading a module that declares an
//// `exported procedure` records it; each `run` then spawns its `serve/2` service loop
//// over a channel (as an INFRASTRUCTURE goal, excluded from the terminal status) and
//// runs under the module driver, so a `M # goal(...)` call routes to it and the run
//// still reports `Success` (not `Suspended` because a serve loop is parked).

import gleeunit/should
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/engine

// A service module (named, with an exported procedure) and a client that calls it.
const service = "-module(math_service).
exported procedure echo(Integer?, Integer).
echo(X, X?)."

const client = "-module(client).
imported procedure math_service#echo(Integer?, Integer).
procedure run_echo(Integer?, Integer).
run_echo(X, R?) :- math_service # echo(X?, R)."

pub fn facade_auto_activates_exported_module_and_routes_hash_call_test() {
  let assert Ok(eng) = engine.load(engine.new(), service)
  let assert Ok(eng) = engine.load(eng, client)

  let #(_eng, env) = engine.run(eng, "run_echo(5, R)")

  // The run completed (the parked serve/2 loop did NOT make it Suspended)...
  env.status |> should.equal(result_envelope.Success)
  // ...and the `#` call routed: R = echo(5) = 5, flowed back on the shared cell.
  env.resolved_bindings
  |> should.equal([#("R", term_codec.ConstTerm(term_codec.ConstInt(5)))])
}

// A plain engine (no exported modules loaded) still runs ordinary goals with no
// module activation — the one-shot path is untouched.
pub fn facade_without_exported_modules_runs_plainly_test() {
  let #(_eng, env) = engine.run(engine.new(), "X := 2 + 3")
  env.status |> should.equal(result_envelope.Success)
  env.resolved_bindings
  |> should.equal([#("X", term_codec.ConstTerm(term_codec.ConstInt(5)))])
}
