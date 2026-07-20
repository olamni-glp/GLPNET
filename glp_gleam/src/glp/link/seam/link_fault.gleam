//// glp/link/seam/link_fault — raw transport-level fault classification
//// (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/link_fault.dart` (mirror
//// `csharp/glp_link/seam/LinkFault.cs`). The SEAM-level signal, deliberately coarser
//// than the GLP monitor lattice: the reliability sublayer refines these into
//// `ok`/`tempFail`/`permFail`/`closed` ground terms the program reads, applying the
//// bounded-silence heuristic the seam cannot know about. A fault is NEVER a
//// unification verdict (FR-043); a disconnect NEVER maps to a logical Fail (FR-044).

import glp/link/seam/link_id.{type LinkId}

/// The coarse transport-level fault classification (FR-008).
pub type LinkFaultKind {
  /// The peer/transport closed the link cleanly → refined to
  /// `closed(LinkId, Reason)` (`eos` for graceful).
  Closed
  /// A recoverable transport error (reset, timeout, dropped connection) → the
  /// sublayer surfaces `tempFail` and may recover via idempotent
  /// reconnect-redelivery (FR-045). The default classification for silence.
  Transient
  /// An unrecoverable transport error (auth/cert failure, refused, protocol
  /// violation) → the sublayer surfaces `permFail` and runs distributed GC
  /// (FR-024, FR-045).
  Permanent
}

/// An out-of-band fault raised by an endpoint (the `on_fault` event). Carries only
/// what the seam knows: which link, the coarse kind, and a human-readable reason
/// (which becomes the `Reason` argument of the refined GLP fault term).
pub type LinkFaultSignal {
  LinkFaultSignal(link: LinkId, kind: LinkFaultKind, reason: String)
}
