//// glp/link/primitives/link_wire — term ↔ single wire-frame codec shared by the
//// egress drainer, the inbound pump, and the path-B handshake (feature 059, T074).
////
//// One place for "a ground term as one self-delimiting frame": serialize it to the
//// 038 TLV blob (`link_egress` / `term_codec`), wrap it in ONE `frame_codec` Whole
//// frame, and the inverse on the way in. The path-B in-band request token
//// (`_link_request` ships it, `_link_listen` reads it) and the ordinary inbound frame
//// decode both go through here, so the wire shape is defined once. Fragment
//// reassembly of oversized payloads is T077 — this MVP handles the `Whole` frame the
//// acceptance programs produce.

import gleam/list
import gleam/option.{None}
import glp/codec/term_codec
import glp/link/primitives/link_egress
import glp/link/reliability/frame_codec
import glp/runtime/terms.{type Term, ConstTerm, StructTerm}

/// Encode a GROUND `term` as one wire frame: serialize to the TLV blob, then frame it
/// as a single Whole frame with `message_id` (path-B ships the request token with
/// message_id 0 as a raw out-of-band frame, so the data sequence still starts at 0).
pub fn encode_term_frame(term: Term, message_id: Int) -> Result(BitArray, String) {
  case link_egress.serialize_ground(term) {
    Error(why) -> Error(why)
    Ok(payload) ->
      // No max_frame_bytes → a single Whole frame (frame_codec never fails here).
      case frame_codec.encode(payload, message_id, None) {
        Ok([frame, ..]) -> Ok(frame)
        Ok([]) -> Error("frame_codec produced no frame")
        Error(_) -> Error("frame encode rejected the payload")
      }
  }
}

/// Decode one inbound wire frame to its ground term: parse the FrameCodec frame, take
/// the (Whole-frame) payload, and decode the TLV term blob back to a runtime term. A
/// `VarRef` cannot appear in a ground-relay frame (surfaced, never fabricated).
pub fn decode_term_frame(bytes: BitArray) -> Result(Term, String) {
  case frame_codec.parse_frame(bytes) {
    Error(_) -> Error("undecodable frame")
    Ok(frame) ->
      case frame.kind {
        frame_codec.Whole -> decode_payload_term(frame.chunk)
        frame_codec.Fragment ->
          Error("fragment reassembly rides the pump's frame_reassembler (T077)")
      }
  }
}

/// Decode a (already-reassembled) TLV payload blob to its ground runtime term — the
/// pump's post-reassembly/post-ordering decode step (T077).
pub fn decode_payload_term(payload: BitArray) -> Result(Term, String) {
  case term_codec.decode_term(payload) {
    Ok(#(codec_term, _rest)) -> from_codec_term(codec_term)
    Error(_) -> Error("undecodable term payload")
  }
}

/// Map a `term_codec.Term` (wire model) back to the runtime `terms.Term`.
fn from_codec_term(t: term_codec.Term) -> Result(Term, String) {
  case t {
    term_codec.ConstTerm(c) -> Ok(ConstTerm(from_codec_const(c)))
    term_codec.StructTerm(functor, args) ->
      case list.try_map(args, from_codec_term) {
        Ok(mapped) -> Ok(StructTerm(functor, mapped))
        Error(e) -> Error(e)
      }
    term_codec.VarRef(_) ->
      Error("inbound frame carried an unbound var (ground-relay violated)")
  }
}

fn from_codec_const(c: term_codec.Constant) -> terms.Constant {
  case c {
    term_codec.ConstAtom(s) -> terms.ConstAtom(s)
    term_codec.ConstInt(v) -> terms.ConstInt(v)
    term_codec.ConstReal(v) -> terms.ConstReal(v)
    term_codec.ConstString(s) -> terms.ConstString(s)
  }
}
