/// A received frame failed validation (bad version byte, bad CRC, truncated
/// header, inconsistent fragmentation metadata, or a reassembly bound exceeded).
/// FR-022 requires a malformed/corrupt frame be REJECTED cleanly — never a
/// silent mis-decode, never an unbounded allocation. The link layer turns this
/// into a transport fault, not a GLP unification Fail (FR-043/FR-044).
class FrameException implements Exception {
  final String message;

  FrameException(this.message);

  @override
  String toString() => 'FrameException: $message';
}
