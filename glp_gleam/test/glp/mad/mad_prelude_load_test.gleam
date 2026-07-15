//// glp/mad — madGLP network prelude load test (feature 050 T050.A4a).
////
//// Verifies the shipped madGLP network system predicates
//// (programs/system/mad_predicates.glp: `global_send/3`, `send_to_net/1`) LOAD
//// through the full Gleam pipeline (parse → SRSW → PE → type-check → compile),
//// merged over the root prelude programs/self.glp — the program the A4b multi-agent
//// parity harness boots each MadEngine on. Reads both `.glp` files from disk via the
//// same OTP `file:read_file/1` FFI the golden-corpus test uses (no new dependency).
////
//// NOTE (escalation, T050.A4): the spec's `send_to_ui/1` (§12.4) and its host kernel
//// `'_send_to_ui'` (§12.5) exist in NEITHER programs/self.glp NOR programs/ at all —
//// they are spec-only. Loading them needs a NEW host kernel (`_send_to_ui`), a
//// Language-Authority §1.14 decision, so they are OUT of this network-prelude load.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

pub fn mad_network_prelude_loads_and_typechecks_test() {
  let self_source = read_source("../programs/self.glp")
  let mad_source = read_source("../programs/system/mad_predicates.glp")

  // Load the madGLP network predicates over the root prelude — the full pipeline
  // (SRSW incl. the `known(T?)` guard non-multiplicity + `ground(Q?)` relaxation,
  // PE, type-check, compile). A rejection here is a real prelude/pipeline gap.
  let assert Ok(outcome) = loader.load(mad_source, self_source)

  // Both network predicates compiled to callable labels — the A4b harness resolves
  // these to boot `send_to_net`/lower `global_send` spawns.
  program.label_pc(outcome.program, "global_send/3") |> is_ok |> should.equal(True)
  program.label_pc(outcome.program, "send_to_net/1") |> is_ok |> should.equal(True)
}

fn is_ok(r: Result(a, b)) -> Bool {
  case r {
    Ok(_) -> True
    Error(_) -> False
  }
}
