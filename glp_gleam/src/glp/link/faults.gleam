//// glp/link/faults — the untrusted-frame ingress GATE (feature 050, T052).
////
//// **FR-015 / data-model.md:** "Untrusted on receipt: length/CRC/type validation
//// precedes any decode; violations → Fault, never crash." This module is where that
//// requirement becomes structural rather than aspirational: it is the ONE entry
//// point through which bytes from a peer become a term, and its result type makes
//// the violation path a VALUE (`Error(fault term)`) — there is no exception to
//// forget to catch and no panic to reach.
////
//// Layering (each stage runs only if the previous passed, so no semantic field of a
//// corrupted frame is ever interpreted):
////   1. **length / CRC / type** — `frame_codec.parse_frame` (T046, byte-parity with
////      the Dart/C# codec): header-size + declared-length bounds (a forged length
////      cannot exhaust memory — `max_payload_bytes`), version + kind bytes, exact
////      frame-length match, CRC-32 over the chunk. All BEFORE the chunk is touched.
////   2. **decode** — `term_codec.decode_term` (038): truncation, varints over 64
////      bits, invalid UTF-8, unknown/reserved tags — every rejection a `CodecError`.
////   3. **ground gate** — the base wire is ground-relay (FR-010): a decoded VarRef
////      is a wire-contract violation, not something to localize.
//// Stages 1–3 are exactly `link_wire.decode_frame`'s pipeline; this gate adds the
//// CLASSIFICATION: every violation refines to `permFail(LinkId, reason)` — a
//// protocol violation is `Permanent` per the seam lattice (`link_fault.gleam`), and
//// it rides the ordinary monitor delivery (FR-043/044: data, never a fourth verdict,
//// never a crash, never a logical Fail of the reader's goal).
////
//// Distinct from `link/primitives/link_faults` (C7): that module DELIVERS fault
//// terms onto monitor cursors; this one DECIDES that a byte-level violation is a
//// fault and builds the term. Gate here, deliver there.

import gleam/string
import glp/link/primitives/link_faults
import glp/link/primitives/link_wire.{type WireError}
import glp/link/reliability/frame_codec
import glp/link/seam/link_id.{type LinkId}
import glp/runtime/terms.{type Term}

/// Validate + decode one inbound DATA frame from the untrusted wire.
/// `Ok(#(message_id, ground_term))` on a clean frame; `Error(permFail(id, reason))`
/// — the delivery-ready fault term — on ANY violation. Total: no input crashes it.
pub fn gate_frame(id: LinkId, frame: BitArray) -> Result(#(Int, Term), Term) {
  case link_wire.decode_frame(frame) {
    Ok(decoded) -> Ok(decoded)
    Error(violation) ->
      Error(link_faults.perm_fail(id, describe_violation(violation)))
  }
}

/// Validate + decode the path-B request TOKEN (the out-of-band frame consumed before
/// any link exists — so there is no LinkId to build a fault term against and no
/// monitor stream to deliver it to). The violation surfaces as a classified detail
/// for the kernel's non-fatal abort instead: rejected safely and reported (FR-015),
/// just on the only channel that exists at that point.
pub fn gate_token(frame: BitArray) -> Result(Term, String) {
  case link_wire.decode_token(frame) {
    Ok(term) -> Ok(term)
    Error(violation) -> Error(describe_violation(violation))
  }
}

/// One human-readable, stage-attributed reason per violation class. The stage name
/// leads so a monitor reader (or an operator grepping) can tell a framing violation
/// from a payload one without parsing the detail.
pub fn describe_violation(violation: WireError) -> String {
  case violation {
    link_wire.Framing(e) -> "frame validation failed: " <> describe_frame_error(e)
    link_wire.Codec(e) -> "payload decode failed: " <> string.inspect(e)
    link_wire.NonGround(detail) -> "ground-relay violation: " <> detail
    link_wire.Fragmented ->
      "fragment received but the base layer ships Whole frames only "
      <> "(reassembly is out of the base wire contract)"
  }
}

fn describe_frame_error(e: frame_codec.FrameError) -> String {
  case e {
    frame_codec.CrcMismatch(got, expected) ->
      "crc mismatch (got "
      <> string.inspect(got)
      <> ", expected "
      <> string.inspect(expected)
      <> ")"
    frame_codec.FrameTooShort(len, min) ->
      "frame shorter than header ("
      <> string.inspect(len)
      <> " < "
      <> string.inspect(min)
      <> ")"
    frame_codec.UnsupportedVersion(found, expected) ->
      "unsupported version byte "
      <> string.inspect(found)
      <> " (expected "
      <> string.inspect(expected)
      <> ")"
    frame_codec.UnknownKind(kind) -> "unknown frame kind " <> string.inspect(kind)
    frame_codec.TotalLengthExceedsMax(total, max) ->
      "declared payload length "
      <> string.inspect(total)
      <> " exceeds the "
      <> string.inspect(max)
      <> "-byte bound"
    other -> string.inspect(other)
  }
}

/// Never used for anything but documentation-adjacent tests: the codec layer this
/// gate fronts. Re-exported so T053's adversarial corpus can build raw frames
/// without importing the codec twice.
pub const max_payload_bytes = frame_codec.max_payload_bytes
