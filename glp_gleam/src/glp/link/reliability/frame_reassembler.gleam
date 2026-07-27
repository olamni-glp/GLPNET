//// glp/link/reliability/frame_reassembler — multi-frame payload reassembly (feature
//// 059, T077 — port of glp_runtime/lib/link/reliability/frame_reassembler.dart, mirror
//// csharp/glp_link/reliability/FrameReassembler.cs).
////
//// Feed it each `frame_codec.ParsedFrame` (already CRC-validated by `parse_frame`); a
//// `Whole` frame yields its payload immediately, while `Fragment`s are buffered by
//// `message_id` until the set is complete (FR-022) — the Fragment path the T074 pump
//// MVP left as Whole-only. Hardened against malformed/adversarial fragment streams
//// (FR-028): inconsistent metadata across a message's fragments is rejected; the number
//// of concurrently in-flight partial messages and the total buffered bytes are both
//// bounded, so a peer cannot exhaust memory. A duplicate fragment (same index
//// re-delivered) is idempotently ignored (payload-level dedup is `inbound_ordering`).
//// One reassembler per link.
////
//// GLEAM MAPPING NOTE: the Dart reassembler mutates its partial-message map in place;
//// here it is an IMMUTABLE value threaded through `accept`, which returns the completed
//// payload (or `None`) WITH the advanced reassembler.

import gleam/bit_array
import gleam/dict.{type Dict}
import gleam/int
import gleam/option.{type Option, None, Some}
import glp/link/reliability/frame_codec.{type ParsedFrame, Fragment, Whole}
import glp/link/reliability/frame_exception.{type FrameException, FrameException}

type Partial {
  Partial(
    total_length: Int,
    frag_count: Int,
    chunks: Dict(Int, BitArray),
    received: Int,
    buffered_bytes: Int,
  )
}

pub opaque type FrameReassembler {
  FrameReassembler(
    partials: Dict(Int, Partial),
    total_buffered: Int,
    max_in_flight: Int,
    max_buffered_bytes: Int,
  )
}

/// A reassembler with `max_in_flight` concurrent partial messages and
/// `max_buffered_bytes` total buffered bytes.
pub fn with_bounds(
  max_in_flight: Int,
  max_buffered_bytes: Int,
) -> FrameReassembler {
  FrameReassembler(
    partials: dict.new(),
    total_buffered: 0,
    max_in_flight: max_in_flight,
    max_buffered_bytes: max_buffered_bytes,
  )
}

/// A reassembler with the default bounds (64 in-flight messages, 64 MiB buffered).
pub fn new() -> FrameReassembler {
  with_bounds(64, 64 * 1024 * 1024)
}

/// Number of messages currently awaiting more fragments.
pub fn in_flight_count(reassembler: FrameReassembler) -> Int {
  dict.size(reassembler.partials)
}

/// Accept one frame. Returns the reassembler + the complete payload when this frame
/// finishes a message (or for a `Whole` frame), otherwise `None`. `Error(FrameException)`
/// on inconsistent or over-bound input (FR-028).
pub fn accept(
  reassembler: FrameReassembler,
  frame: ParsedFrame,
) -> Result(#(FrameReassembler, Option(BitArray)), FrameException) {
  case frame.kind {
    Whole ->
      case bit_array.byte_size(frame.chunk) == frame.total_length {
        False ->
          Error(FrameException(
            "whole frame chunk "
            <> int.to_string(bit_array.byte_size(frame.chunk))
            <> " != total "
            <> int.to_string(frame.total_length),
          ))
        True -> Ok(#(reassembler, Some(frame.chunk)))
      }
    Fragment -> accept_fragment(reassembler, frame)
  }
}

fn accept_fragment(
  reassembler: FrameReassembler,
  frame: ParsedFrame,
) -> Result(#(FrameReassembler, Option(BitArray)), FrameException) {
  case dict.get(reassembler.partials, frame.message_id) {
    // First fragment of a new message: bound-check, then open a partial.
    Error(Nil) ->
      case dict.size(reassembler.partials) >= reassembler.max_in_flight {
        True ->
          Error(FrameException(
            "too many in-flight partial messages (max "
            <> int.to_string(reassembler.max_in_flight)
            <> ")",
          ))
        False ->
          case
            reassembler.total_buffered + frame.total_length
            > reassembler.max_buffered_bytes
          {
            True ->
              Error(FrameException(
                "reassembly buffer would exceed "
                <> int.to_string(reassembler.max_buffered_bytes)
                <> " bytes",
              ))
            False ->
              add_chunk(
                reassembler,
                frame,
                Partial(
                  total_length: frame.total_length,
                  frag_count: frame.frag_count,
                  chunks: dict.new(),
                  received: 0,
                  buffered_bytes: 0,
                ),
              )
          }
      }
    // Subsequent fragment: metadata must be consistent across the message.
    Ok(partial) ->
      case
        partial.total_length == frame.total_length
        && partial.frag_count == frame.frag_count
      {
        False ->
          Error(FrameException(
            "fragment metadata mismatch for message "
            <> int.to_string(frame.message_id),
          ))
        True -> add_chunk(reassembler, frame, partial)
      }
  }
}

fn add_chunk(
  reassembler: FrameReassembler,
  frame: ParsedFrame,
  partial: Partial,
) -> Result(#(FrameReassembler, Option(BitArray)), FrameException) {
  // A duplicate fragment (same index re-delivered) is idempotently ignored.
  let #(partial, total_delta) = case dict.has_key(partial.chunks, frame.frag_index) {
    True -> #(partial, 0)
    False -> {
      let chunk_len = bit_array.byte_size(frame.chunk)
      #(
        Partial(
          ..partial,
          chunks: dict.insert(partial.chunks, frame.frag_index, frame.chunk),
          received: partial.received + 1,
          buffered_bytes: partial.buffered_bytes + chunk_len,
        ),
        chunk_len,
      )
    }
  }
  let total_buffered = reassembler.total_buffered + total_delta

  case partial.received < partial.frag_count {
    // Still awaiting fragments — update the partial in place.
    True ->
      Ok(#(
        FrameReassembler(
          ..reassembler,
          partials: dict.insert(reassembler.partials, frame.message_id, partial),
          total_buffered: total_buffered,
        ),
        None,
      ))
    // Complete — concatenate the chunks in fragment order, verify the total, remove.
    False ->
      case assemble(partial) {
        Error(e) -> Error(e)
        Ok(payload) ->
          Ok(#(
            FrameReassembler(
              ..reassembler,
              partials: dict.delete(reassembler.partials, frame.message_id),
              total_buffered: total_buffered - partial.buffered_bytes,
            ),
            Some(payload),
          ))
      }
  }
}

/// Concatenate a complete partial's chunks in fragment-index order and verify the
/// reassembled length matches the advertised total.
fn assemble(partial: Partial) -> Result(BitArray, FrameException) {
  let payload = concat_chunks(partial.chunks, 0, partial.frag_count, <<>>)
  case bit_array.byte_size(payload) == partial.total_length {
    True -> Ok(payload)
    False ->
      Error(FrameException(
        "reassembled "
        <> int.to_string(bit_array.byte_size(payload))
        <> " bytes != advertised total "
        <> int.to_string(partial.total_length),
      ))
  }
}

/// Concatenate chunks `index..count-1` in order (missing indices contribute nothing;
/// completeness is guaranteed by the `received == frag_count` gate before this runs).
fn concat_chunks(
  chunks: Dict(Int, BitArray),
  index: Int,
  count: Int,
  acc: BitArray,
) -> BitArray {
  case index >= count {
    True -> acc
    False -> {
      let acc = case dict.get(chunks, index) {
        Ok(chunk) -> bit_array.append(acc, chunk)
        Error(Nil) -> acc
      }
      concat_chunks(chunks, index + 1, count, acc)
    }
  }
}
