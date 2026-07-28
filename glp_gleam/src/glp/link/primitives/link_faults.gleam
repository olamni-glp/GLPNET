//// glp/link/primitives/link_faults — the link fault lattice + bounded-silence
//// classification (wave-3 US4, T038; FR-024, SC-007, amended contract rule 6).
////
//// Port of the `glp_runtime/lib/link/primitives/link_faults.dart` lattice —
//// `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` /
//// `permFail(LinkId,Reason)` — as host-level values. A fault is ORDINARY DATA
//// (never a fourth unification verdict, FR-043-025; a disconnect never maps to
//// a logical Fail, FR-044-025). The GLP-facing heap-cursor fan-out
//// (monitor-stream binds) is the engine-integration layer above this and lands
//// with the link kernels; every consumer here observes faults via the
//// `Endpoint.faults` Subject + the pump's `PumpFault` events.
////
//// The bounded-silence heuristic (the seam cannot know it): silence up to
//// `temp_fail_after_ms` is healthy; beyond it the link is `tempFail` (may
//// recover via idempotent redelivery); beyond `perm_fail_after_ms` —
//// default 30 000 ms, the contract's ≤ 30 s bound — the link is `permFail`
//// and distributed GC reclaims it. A deliberate, possibly-wrong give-up
//// (FLP/two-generals), never an indefinite block.

import glp/link/seam/link_fault.{
  type LinkFaultKind, type LinkFaultSignal, Closed, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options.{type LinkOptions}

/// The GLP-visible fault lattice (Dart/C# ground-term shapes, as host values).
pub type LinkFault {
  FaultOk
  FaultClosed(id: LinkId, reason: String)
  FaultTempFail(id: LinkId, reason: String)
  FaultPermFail(id: LinkId, reason: String)
}

/// Refine a coarse seam signal into its lattice value (the sublayer's
/// classification: Closed → closed, Transient → tempFail, Permanent →
/// permFail).
pub fn refine(signal: LinkFaultSignal) -> LinkFault {
  case signal.kind {
    Closed -> FaultClosed(signal.link, signal.reason)
    Transient -> FaultTempFail(signal.link, signal.reason)
    Permanent -> FaultPermFail(signal.link, signal.reason)
  }
}

/// Classify a silence of `silent_ms` on the link (the bounded-silence
/// heuristic): healthy under the temp bound; `tempFail` under the perm bound;
/// `permFail` at or beyond it — the FR-024 ≤ 30 s peer-loss surface (default
/// `perm_fail_after_ms` = 30 000).
pub fn classify_silence(
  id: LinkId,
  silent_ms: Int,
  options: LinkOptions,
) -> LinkFault {
  case silent_ms >= options.perm_fail_after_ms {
    True ->
      FaultPermFail(
        id,
        "peer silent beyond the permanent bound — giving up (FR-024)",
      )
    False ->
      case silent_ms >= options.temp_fail_after_ms {
        True ->
          FaultTempFail(id, "peer silent beyond the transient bound (FR-045)")
        False -> FaultOk
      }
  }
}

/// The refined kind for a raw seam kind (test/inspection convenience).
pub fn kind_of(fault: LinkFault) -> Result(LinkFaultKind, Nil) {
  case fault {
    FaultOk -> Error(Nil)
    FaultClosed(..) -> Ok(Closed)
    FaultTempFail(..) -> Ok(Transient)
    FaultPermFail(..) -> Ok(Permanent)
  }
}
