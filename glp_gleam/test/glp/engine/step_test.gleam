//// Facade interactive-stepping tests (feature 050, US2 polish — `start`/`step`/
//// `Event`, the contract Engine surface `step(Engine) -> #(Engine, Event)`).
////
//// `start` boots a goal into a session; `step` advances one reduction at a time
//// (`Reduced`/`Suspended` continue), and a drained queue yields `Done(envelope,
//// output)` — the same envelope one-shot `run` produces. No session → `Idle`.

import gleeunit/should
import glp/codec/result_envelope
import glp/codec/term_codec
import glp/engine.{type Engine, type Event}

/// Step to a terminal event (`Done`/`Idle`/`Errored`), threading the engine.
fn drive(eng: Engine) -> Event {
  let #(eng2, ev) = engine.step(eng)
  case ev {
    engine.Reduced(..) -> drive(eng2)
    engine.Suspended(..) -> drive(eng2)
    _ -> ev
  }
}

// No active session → Idle, engine unchanged.
pub fn step_without_session_is_idle_test() {
  let #(_eng, ev) = engine.step(engine.new())
  ev |> should.equal(engine.Idle)
}

// start(X := 2+3) then step to quiescence → Done with the same success envelope
// one-shot run produces (X = 5).
pub fn start_then_step_to_done_test() {
  let assert Ok(eng) = engine.start(engine.new(), "X := 2+3")
  let assert engine.Done(envelope, _output) = drive(eng)
  envelope.status
  |> should.equal(result_envelope.Success)
  envelope.resolved_bindings
  |> should.equal([#("X", term_codec.ConstTerm(term_codec.ConstInt(5)))])
}

// A bad goal fails to start, leaving the engine session-free (step → Idle).
pub fn start_unknown_predicate_errors_test() {
  let #(eng, ev) = engine.step(engine.new())
  ev |> should.equal(engine.Idle)
  case engine.start(eng, "no_such_pred(1)") {
    Error(reason) ->
      reason |> should.equal("predicate no_such_pred/1 not found")
    Ok(_) -> should.fail()
  }
}

// The interactive result matches one-shot run for the same goal (Done envelope
// equals run's envelope).
pub fn step_result_matches_run_test() {
  let #(_e, run_env) = engine.run(engine.new(), "X := 2+3")
  let assert Ok(eng) = engine.start(engine.new(), "X := 2+3")
  let assert engine.Done(step_env, _) = drive(eng)
  step_env |> should.equal(run_env)
}
