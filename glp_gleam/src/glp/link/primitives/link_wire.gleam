//// glp/link/primitives/link_wire — the out-of-band request-token wire (feature 050, C4).
////
//// Path B exchanges a `request(LinkId, FromPeer)` token BEFORE the data plane exists
//// (egress = C5, pump = C6), so the token crosses as a RAW framed payload on the
//// transport endpoint, exactly as the C# oracle ships it: `FrameCodec.Encode(payload,
//// messageId: 0)` over `endpoint.SendBytesAsync`, consumed by the listener before the
//// pump engages so the DATA sequence still starts at 0. This module is that raw codec —
//// a GROUND `terms.Term` ↔ one `Whole` frame — reusing the shipped 038 `term_codec` for
//// the payload bytes and the shipped 038 `frame_codec` for framing (crc32 + TLV).
////
//// Both ends of a Gleam path-B handshake are Gleam, so they need only agree with EACH
//// OTHER; cross-runtime wire parity (a Gleam listener ↔ a C# connector) is a later
//// matrix concern (T056), not base C4.
////
//// GROUND ONLY: the base wire is ground-relay (FR-010). A `VarRef` in the token is an
//// upstream invariant break (the wrappers guard `ground/1`) — surfaced, never defaulted.

import gleam/list
import gleam/option
import glp/codec/term_codec
import glp/link/reliability/frame_codec
import glp/runtime/terms

/// The out-of-band token rides message id 0 (NOT the link sequencer), so the data
/// sequence still starts at 0 once the pump engages (C# `messageId: 0`).
const token_message_id = 0

/// Why a token could not be put on / taken off the wire. Each is surfaced by the kernel
/// as a non-fatal abort, never swallowed.
pub type WireError {
  /// The term (or a decoded token) carried an unbound variable — the ground-relay gate.
  NonGround(detail: String)
  /// The `term_codec` rejected the payload bytes (truncated, bad UTF-8, unknown tag…).
  Codec(error: term_codec.CodecError)
  /// The `frame_codec` rejected the frame (bad crc, version, length…) or the payload.
  Framing(error: frame_codec.FrameError)
  /// The payload spanned more than one frame. The base token is small and single-`Whole`
  /// -frame; reassembly is T052, so a fragment is malformed for the C4 handshake.
  Fragmented
}

/// Encode a GROUND term into ONE self-delimiting `Whole` frame ready for `endpoint.send`.
pub fn encode_token(t: terms.Term) -> Result(BitArray, WireError) {
  case to_codec_term(t) {
    Error(e) -> Error(e)
    Ok(ct) ->
      // `None` MTU → a single Whole frame (no fragmentation); the token is small.
      case frame_codec.encode(term_codec.encode_term(ct), token_message_id, option.None) {
        Error(e) -> Error(Framing(e))
        Ok([frame]) -> Ok(frame)
        Ok(_) -> Error(Fragmented)
      }
  }
}

/// Decode ONE `Whole` frame back into the ground term the peer shipped.
pub fn decode_token(frame: BitArray) -> Result(terms.Term, WireError) {
  case frame_codec.parse_frame(frame) {
    Error(e) -> Error(Framing(e))
    Ok(parsed) ->
      case parsed.kind {
        frame_codec.Fragment -> Error(Fragmented)
        frame_codec.Whole ->
          case term_codec.decode_term(parsed.chunk) {
            Error(e) -> Error(Codec(e))
            Ok(#(ct, _rest)) -> from_codec_term(ct)
          }
      }
  }
}

// ── ground terms.Term ↔ term_codec.Term (the two Term types differ only at VarRef) ──

fn to_codec_term(t: terms.Term) -> Result(term_codec.Term, WireError) {
  case t {
    terms.ConstTerm(c) -> Ok(term_codec.ConstTerm(to_codec_const(c)))
    terms.StructTerm(f, args) ->
      case to_codec_terms(args, []) {
        Error(e) -> Error(e)
        Ok(cargs) -> Ok(term_codec.StructTerm(f, cargs))
      }
    terms.VarRef(_) ->
      Error(NonGround("outbound token carried an unbound variable"))
  }
}

fn to_codec_terms(
  args: List(terms.Term),
  acc: List(term_codec.Term),
) -> Result(List(term_codec.Term), WireError) {
  case args {
    [] -> Ok(list.reverse(acc))
    [h, ..t] ->
      case to_codec_term(h) {
        Error(e) -> Error(e)
        Ok(ch) -> to_codec_terms(t, [ch, ..acc])
      }
  }
}

fn to_codec_const(c: terms.Constant) -> term_codec.Constant {
  case c {
    terms.ConstAtom(s) -> term_codec.ConstAtom(s)
    terms.ConstInt(i) -> term_codec.ConstInt(i)
    terms.ConstReal(r) -> term_codec.ConstReal(r)
    terms.ConstString(s) -> term_codec.ConstString(s)
  }
}

fn from_codec_term(t: term_codec.Term) -> Result(terms.Term, WireError) {
  case t {
    term_codec.ConstTerm(c) -> Ok(terms.ConstTerm(from_codec_const(c)))
    term_codec.StructTerm(f, args) ->
      case from_codec_terms(args, []) {
        Error(e) -> Error(e)
        Ok(cargs) -> Ok(terms.StructTerm(f, cargs))
      }
    term_codec.VarRef(_) ->
      Error(NonGround("inbound token carried an unbound variable"))
  }
}

fn from_codec_terms(
  args: List(term_codec.Term),
  acc: List(terms.Term),
) -> Result(List(terms.Term), WireError) {
  case args {
    [] -> Ok(list.reverse(acc))
    [h, ..t] ->
      case from_codec_term(h) {
        Error(e) -> Error(e)
        Ok(ch) -> from_codec_terms(t, [ch, ..acc])
      }
  }
}

fn from_codec_const(c: term_codec.Constant) -> terms.Constant {
  case c {
    term_codec.ConstAtom(s) -> terms.ConstAtom(s)
    term_codec.ConstInt(i) -> terms.ConstInt(i)
    term_codec.ConstReal(r) -> terms.ConstReal(r)
    term_codec.ConstString(s) -> terms.ConstString(s)
  }
}
