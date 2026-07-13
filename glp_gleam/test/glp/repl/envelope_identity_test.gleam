//// Envelope-identity test (feature 050, T036; US2 — FR-009 / SC-004 corollary).
////
//// The ED-1 seam guarantee: a goal's `ResultEnvelope` is byte-identical whether
//// consumed in-process (the REPL) or over the wire (encode→decode). We run a goal
//// through the engine, then assert (1) the wire round-trip is byte-stable
//// (`encode(decode(encode(env))) == encode(env)`) and (2) the in-process rendering
//// equals the decoded-from-wire rendering — so the REPL user and a link peer see
//// the same result for the same computation.

import gleeunit/should
import glp/codec/result_envelope
import glp/engine
import glp/repl/results

// A bound result (X := 2+3 → X = 5) is byte-identical across the wire, and renders
// the same in-process and after decode.
pub fn success_envelope_identity_test() {
  let e = engine.new()
  let #(_e, env) = engine.run(e, "X := 2+3")

  let bytes = result_envelope.encode(env)
  let assert Ok(decoded) = result_envelope.decode(bytes)

  // Wire round-trip is byte-stable.
  result_envelope.encode(decoded)
  |> should.equal(bytes)
  // In-process vs decoded-from-wire render identically (the ED-1 seam).
  results.render_outcome(decoded)
  |> should.equal(results.render_outcome(env))
  // And that shared rendering is the expected outcome.
  results.render_outcome(env)
  |> should.equal(["X = 5", "→ succeeds"])
}

// A failure envelope (unknown predicate) carries its error string identically
// across the wire — the error is part of the seam, not a local REPL string.
pub fn failure_envelope_identity_test() {
  let e = engine.new()
  let #(_e, env) = engine.run(e, "no_such_pred(1)")

  let bytes = result_envelope.encode(env)
  let assert Ok(decoded) = result_envelope.decode(bytes)

  result_envelope.encode(decoded)
  |> should.equal(bytes)
  results.render_outcome(decoded)
  |> should.equal(results.render_outcome(env))
}
