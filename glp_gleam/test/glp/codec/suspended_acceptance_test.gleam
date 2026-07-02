//// US1 Acceptance #3 (T025): a suspended goal emits Status=suspended + the blocking-
//// reader set, and no heap address leaks — the blocking readers and any remaining
//// variable are GlobalVarId(agent_id, local_id), never a bare heap address. Codec-level
//// assertion (survives encode → decode).

import gleam/option.{None}
import gleeunit/should
import glp/codec/result_envelope.{ResultEnvelope, Suspended, decode, encode}
import glp/codec/term_codec.{GlobalVarId, StructTerm, VarRef}

pub fn suspended_status_and_blocking_readers_roundtrip_test() {
  let env =
    ResultEnvelope(
      Suspended,
      [],
      [],
      [GlobalVarId("agent1", 3), GlobalVarId("agent2", 5)],
      <<>>,
      None,
    )
  let assert Ok(decoded) = decode(encode(env))
  decoded |> should.equal(env)
  // no heap-address leak: the blocking-reader set is exactly the two global ids.
  let assert ResultEnvelope(Suspended, _, _, susp, _, _) = decoded
  susp |> should.equal([GlobalVarId("agent1", 3), GlobalVarId("agent2", 5)])
}

pub fn suspended_with_binding_carries_partial_and_var_to_writer_test() {
  let env =
    ResultEnvelope(
      Suspended,
      [#("Partial", StructTerm("waiting_on", [VarRef(GlobalVarId("agent1", 11))]))],
      [#("Q", GlobalVarId("agent1", 11))],
      [GlobalVarId("agent1", 11)],
      <<>>,
      None,
    )
  let assert Ok(decoded) = decode(encode(env))
  decoded |> should.equal(env)
  // the remaining variable inside the binding is a VarRef carrying a GlobalVarId —
  // never a raw heap address.
  let assert ResultEnvelope(
    Suspended,
    [#("Partial", StructTerm("waiting_on", [VarRef(GlobalVarId(agent, local))]))],
    [#("Q", GlobalVarId("agent1", 11))],
    [GlobalVarId("agent1", 11)],
    _,
    _,
  ) = decoded
  agent |> should.equal("agent1")
  local |> should.equal(11)
}
