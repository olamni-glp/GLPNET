//// glp/ring/atomvm — the L1a/L2 realization: GLP on AtomVM inside the MAUI Blazor Hybrid
//// app (feature 101, T018 · FR-004).
////
//// Held to `glp/contract/ring_descriptor`, exactly as `glp/ring/beam` is, and NEVER reaching
//// sideways to it: `beam` is a sibling, not a peer. LATTICE line 27 forbids L1a and L1b
//// sharing directly, which is why anything both need lives in `glp/contract/`.
////
//// ## The subset constraint, and what is actually known about it
////
//// AtomVM runs a SUBSET of BEAM/OTP. `unsupported()` below carries the constructs measured as
//// outside it. Engineer ruling `Q-GLPNETS17-01` (2026-09-04) directed BOTH halves of T017:
//// adopt the measured boundary now, AND install a real AtomVM to measure exhaustively. This
//// module is the first half.
////
//// 🔴 **The list is a LOWER BOUND, not the subset.** It comes from a spike that genuinely ran
//// AtomVM 0.6.6 (`AtomVM-linux-x86_64-static-mbedtls-v0.6.6`) and observed
//// `module proc_lib cannot be resolved` on a `gleam_otp` actor build, while the raw
//// `erlang:spawn` + `gleam_erlang` Subjects variant ran byte-identical to Erlang
//// (`docs/research/gleam-atomvm/dossier.md` §3, §4.3). That is an observed failure and an
//// observed success. Everything NOT in the list is simply unmeasured — upstream documents
//// AtomVM's subset as substantially narrower than full OTP (ETS, several stdlib modules,
//// parts of the process API), and none of that was exercised here. Listing it would be the
//// guess `research.md` R3 forbade.
////
//// ## Why the refusal is at build time
////
//// FR-004 requires a loud refusal naming the construct. A runtime rejection is a silent
//// degrade until the offending path executes — the workaround shape Principle II forbids. The
//// gate is `test/ring/check_atomvm_subset.sh`, which fails the build and names the construct.
//// 🔴 The list below is a SECOND COPY of `test/ring/atomvm-unsupported.list`, which is what the
//// gate actually reads. Gleam cannot read a file at build time, so the duplication is real and
//// cannot be designed away here. It is therefore made LOUD rather than promised away:
//// `test/ring/test_list_single_source.sh` FAILS the moment the two disagree (Principle VIII).
//// An earlier version of this comment claimed the module "documents rather than duplicates" the
//// list — it did not, and nothing would have caught the drift. The analyze pass of 2026-09-04
//// found it.
////
//// ## Conformance is UNREAD, and stays that way
////
//// This ring reports `Unread` with a named reason, never a pass and never a zero that reads
//// like one, because BOTH of these are true here (re-measured at report time, not assumed):
////
////   * the AtomVM toolchain is absent on this host — no `atomvm`, no `packbeam`;
////   * the MAUI Blazor Hybrid host is target-side and absent from this repo (`maui` = 0
////     occurrences in product code).
////
//// **Do not synthesize a stand-in host to turn this into a `Measured`.** A local Erlang
//// process pretending to be the app host produces evidence about the stand-in, not about
//// AtomVM, and it would be invisible in a report that carried only counts. R4 forbids it.
//// The aggregate refuses on this ring, and that refusal is correct.

import glp/contract/ring_descriptor.{
  type Conformance, type Ring, AtomVM, Ring, Unread,
}

/// Constructs measured as outside AtomVM's BEAM/OTP subset.
///
/// Mirrors `test/ring/atomvm-unsupported.list`, which is the single source of truth and the
/// file the build gate reads. A LOWER BOUND — see the module doc.
pub fn unsupported() -> List(String) {
  [
    "proc_lib", "gleam_otp", "gleam/otp", "gleam/erlang/process.spawn", "gen_server",
    "gen_statem", "gen_event", "supervisor",
  ]
}

/// Why this ring's conformance cannot be measured in this repo, on this host.
///
/// Re-measured by `test/ring/report_atomvm_unread.sh` before it is emitted — if either
/// premise stops holding, that script exits 2 and tells you to revisit rather than continuing
/// to report UNREAD out of habit.
pub const unread_reason: String = "AtomVM toolchain absent on this host (no atomvm/packbeam) and the MAUI Blazor Hybrid host is target-side, absent from this repo; construct list is a lower bound measured on 0.6.6 only"

/// The AtomVM ring's self-description.
pub fn descriptor(conformance: Conformance) -> Ring {
  Ring(
    id: AtomVM,
    runtime: "AtomVM 0.6.6 (BEAM/OTP subset)",
    unsupported: unsupported(),
    conformance: conformance,
  )
}

/// The ring as it honestly stands today: UNREAD, with the reason named.
pub fn current() -> Ring {
  descriptor(Unread(unread_reason))
}
