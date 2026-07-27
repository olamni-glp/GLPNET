//// glp/link/reliability/frame_exception — a received frame / reassembly failure
//// (feature 059, T077 — port of glp_runtime/lib/link/reliability/frame_exception.dart,
//// mirror csharp/glp_link/reliability/FrameException.cs).
////
//// A frame failed validation (bad version/CRC/truncation is `frame_codec.FrameError`;
//// inconsistent fragmentation metadata or a reassembly/reorder bound exceeded is this
//// one). FR-022 requires a malformed/corrupt frame be REJECTED CLEANLY — never a
//// silent mis-decode, never an unbounded allocation. The link layer turns it into a
//// transport fault, NOT a GLP unification Fail (FR-043/044). On BEAM it is a `Result`
//// error value, not a thrown exception.

pub type FrameException {
  FrameException(message: String)
}
