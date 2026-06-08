import 'link_id.dart';

/// The raw, transport-level fault classification a leaf reports out-of-band of the
/// data path (FR-008). This is the SEAM-level signal, deliberately coarser than
/// the GLP monitor lattice: the reliability sublayer (architecture-context.md §5)
/// refines these into the `ok` / `tempFail` / `permFail` /
/// `closed` ground terms the program reads, applying the bounded-silence
/// heuristic that the seam cannot know about. A fault is NEVER a unification
/// verdict (FR-043) and a disconnect NEVER maps to a logical Fail (FR-044).
enum LinkFaultKind {
  /// The peer/transport closed the link cleanly. Refined by the sublayer to a
  /// terminal `closed(LinkId, Reason)` term (`eos` for graceful;
  /// contracts/link-primitives.md §1, lines 67-70).
  closed,

  /// A recoverable transport error (reset, timeout, dropped connection). The
  /// DEFAULT classification for silence; the sublayer surfaces `tempFail`
  /// and may recover via idempotent reconnect-redelivery (FR-045).
  transient,

  /// An unrecoverable transport error (auth/cert failure, connection refused,
  /// protocol violation). The sublayer surfaces `permFail` and runs
  /// distributed GC (FR-024, FR-045).
  permanent,
}

/// An out-of-band fault notification raised by an [ILinkEndpoint]
/// (the `ILinkEndpoint.onFault` event). Carries only what the seam
/// knows: which link, the coarse [LinkFaultKind], and a human-readable
/// reason. The sublayer maps it onto the per-link monitor stream.
///
/// [link]: The link this fault concerns.
/// [kind]: The coarse transport-level classification.
/// [reason]: A human-readable cause (e.g. "connection reset", "tls handshake failed"). It
/// becomes the `Reason` argument of the refined GLP fault term.
class LinkFaultSignal {
  final LinkId link;
  final LinkFaultKind kind;
  final String reason;

  const LinkFaultSignal(this.link, this.kind, this.reason);

  @override
  bool operator ==(Object other) =>
      other is LinkFaultSignal &&
      link == other.link &&
      kind == other.kind &&
      reason == other.reason;

  @override
  int get hashCode => Object.hash(link, kind, reason);
}
