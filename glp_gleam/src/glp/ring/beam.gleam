//// glp/ring/beam — the L1b realization: GLP on full Erlang/OTP at the workstation
//// (feature 101, T014 · FR-003).
////
//// Held to `glp/contract/ring_descriptor`. This module may depend on the BEAM — that is
//// the entire point of a ring — and it does so through the modules that already carry
//// the runtime: `glp/repl/*`, `glp/engine/kernels`, `glp/be/server`, `glp/fe/client`,
//// `glp/link/primitives/*` and `glp/link/transports/*`.
////
//// What it must NOT do is reach sideways. `glp/ring/atomvm` is a sibling, not a peer:
//// LATTICE line 27 forbids L1a and L1b sharing directly, so anything both rings need
//// goes into `glp/contract/` and each realizes it separately. If you find yourself
//// importing the other ring from here, the thing you want belongs in the contract.
////
//// ## Measured position (2026-09-03)
////
//// The corpus runs green on this ring: 206/206 agreement against the Dart reference,
//// 0 divergences, 0 excused, `expected.list` empty so nothing is excluded
//// (test/parity/run_gleam_corpus.sh). Two honest limits on that number:
////
////   * it is over **206 pinned cases**, not the 384-test unified suite — 100% here is
////     not total semantic equivalence, and
////   * the count is only evidence of THIS ring. The AtomVM ring is unbuilt, and the
////     aggregate refuses rather than reporting this ring's green as the whole (C4-R).
////
//// `unsupported` is deliberately `[]`: on full OTP nothing in the port is refused. That
//// empty list is a claim, not an absence of one — the contrast with AtomVM's list is
//// exactly what the ring split is for.

import glp/contract/ring_descriptor.{
  type Conformance, type Ring, Beam, Measured, Ring,
}

/// The size of the pinned corpus this ring is measured against.
///
/// Named rather than inlined because a report without a denominator is unparseable
/// (C4/SC-002) — "206 agreed" is not a result; 206 out of what is.
pub const corpus_denominator: Int = 206

/// The BEAM ring's self-description.
pub fn descriptor(conformance: Conformance) -> Ring {
  Ring(
    id: Beam,
    runtime: "Erlang/OTP (BEAM)",
    // Nothing is refused on full OTP. See the module doc: this empty list is a claim.
    unsupported: [],
    conformance: conformance,
  )
}

/// The descriptor carrying the last recorded corpus measurement.
///
/// These numbers are transcribed from a real run of `test/parity/run_gleam_corpus.sh`
/// (2026-09-03: agree=206 diverge=0 blocked=0 gap/fork=0, exit 0). They are a RECORD,
/// not a promise — `test/ring/` re-measures rather than trusting this constant, and
/// T016 re-runs the same corpus with no Dart toolchain on PATH to prove the ring stands
/// up without the reference implementation present.
pub fn measured() -> Ring {
  descriptor(Measured(
    attempted: corpus_denominator,
    agreed: corpus_denominator,
    diverged: 0,
    excused: 0,
  ))
}

/// This ring's C4 conformance report.
pub fn report(ring: Ring) -> String {
  ring_descriptor.report(ring, corpus_denominator)
}
