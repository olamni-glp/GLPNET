//// glp/contract/ring_descriptor — the runtime-free description of a ring (feature 101,
//// T013/T014 · FR-001).
////
//// This is the contract half of the contract/realization split. It names WHAT a ring
//// must declare about itself; each ring supplies its own values (`glp/ring/beam`,
//// `glp/ring/atomvm`). The type lives at L0 precisely because both rings need it and
//// neither may depend on the other (LATTICE line 27) — and it can live at L0 because it
//// is data, carrying no runtime dependency of its own (C1).
////
//// Deliberately no `run`, no `spawn`, no handle: the moment this module described a
//// process or a port, it would have taken a runtime dependency and C1-R would reject it.
//// The contract is a boundary, not a facade to route calls through.
////
//// Purity here is enforced, not asserted — test/ring/check_contract_purity.sh computes
//// the transitive closure over `gleam/erlang` imports and `@external(erlang)` FFI and
//// fails the build if anything under `glp/contract/` is tainted.

import gleam/int
import gleam/list
import gleam/string

/// Which ring a realization belongs to.
///
/// `Beam` is L1b, the workstation: full Erlang/OTP on the developer machine.
/// `AtomVM` is L1a/L2, the app: AtomVM inside the MAUI Blazor Hybrid host.
///
/// These are siblings and must never share a realization — that constraint is the
/// entire reason this feature exists in the shape it does (008 FR-017).
pub type RingId {
  Beam
  AtomVM
}

/// How a ring reports its conformance evidence.
///
/// `Measured` — the suite ran here and these are its numbers.
/// `Unread`   — the suite did NOT run, and this is why, named.
///
/// There is deliberately no third case meaning "assume fine". An unmeasured ring reports
/// `Unread` with a reason and the aggregate refuses (C4-R); it never quietly contributes
/// a pass. Synthesizing a stand-in host to turn `Unread` into `Measured` is the specific
/// dishonesty this type exists to make awkward.
pub type Conformance {
  Measured(attempted: Int, agreed: Int, diverged: Int, excused: Int)
  Unread(reason: String)
}

/// A ring's self-description.
///
/// `unsupported` names the constructs this ring cannot carry. It is load-bearing for
/// AtomVM (C3: refuse at BUILD time, naming the construct) and is expected to be empty
/// for BEAM. An empty list is a claim — "nothing is unsupported" — not an absence of one.
pub type Ring {
  Ring(
    id: RingId,
    runtime: String,
    unsupported: List(String),
    conformance: Conformance,
  )
}

/// The wire/report token for a ring. Used as the report filename stem and the `ring:`
/// field of a C4 conformance report, so it must stay stable.
pub fn token(id: RingId) -> String {
  case id {
    Beam -> "beam"
    AtomVM -> "atomvm"
  }
}

/// Every ring this capability must deliver. The aggregate requires all of them; a
/// missing one is a refusal, never a smaller denominator (C4-R / SC-006).
pub fn required() -> List(RingId) {
  [Beam, AtomVM]
}

/// Is a construct refused by this ring? Used by a ring's build-time gate (C3).
pub fn refuses(ring: Ring, construct: String) -> Bool {
  list.any(ring.unsupported, fn(u) { string.lowercase(u) == string.lowercase(construct) })
}

/// Render a ring's conformance as the C4 report body.
///
/// `not_run` is always emitted — a report that omits it is rejected by the parser
/// (FR-006), because a silently-empty result is a failure, not a clean sweep.
pub fn report(ring: Ring, denominator: Int) -> String {
  let head = "ring: " <> token(ring.id) <> "\ndenominator: " <> int.to_string(denominator)
  case ring.conformance {
    Measured(attempted, agreed, diverged, excused) ->
      head
      <> "\nattempted: " <> int.to_string(attempted)
      <> "\nagreed: " <> int.to_string(agreed)
      <> "\ndiverged: " <> int.to_string(diverged)
      <> "\nexcused: " <> int.to_string(excused)
      <> "\nnot_run: none"
    Unread(reason) ->
      head
      <> "\nattempted: 0\nagreed: 0\ndiverged: 0\nexcused: 0"
      <> "\nnot_run: " <> token(ring.id) <> "-conformance (" <> reason <> ")"
  }
}
