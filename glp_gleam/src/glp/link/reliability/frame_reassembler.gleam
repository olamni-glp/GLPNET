//// glp/link/reliability/frame_reassembler — fragmented-payload reassembly
//// (wave-3 US4, T036/T037; contract rule 5).
////
//// Port of `glp_runtime/lib/link/reliability/frame_reassembler.dart` (mirror C#
//// `FrameReassembler`): feed each CRC-validated `ParsedFrame`; a `Whole` frame
//// yields its payload immediately, fragments buffer by `message_id` until the
//// set completes. Hardened against malformed/adversarial fragment streams:
//// inconsistent metadata across a message's fragments is rejected; the number
//// of concurrently in-flight partial messages AND the total buffered bytes are
//// both bounded, so a peer cannot exhaust memory by opening many partials or
//// never completing them. A partial or inconsistent payload is NEVER delivered
//// as complete (contract rule 5). Immutable — `accept` returns the advanced
//// state. One reassembler per link, driven by that link's single receive loop.

import gleam/bit_array
import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import glp/link/reliability/frame_codec.{type ParsedFrame, Fragment, Whole}

type Partial {
  Partial(
    total_length: Int,
    frag_count: Int,
    chunks: Dict(Int, BitArray),
    buffered_bytes: Int,
  )
}

pub opaque type FrameReassembler {
  FrameReassembler(
    max_in_flight: Int,
    max_buffered_bytes: Int,
    partials: Dict(Int, Partial),
    total_buffered: Int,
  )
}

/// A fresh reassembler; reference defaults 64 in-flight messages, 64 MiB.
pub fn new(max_in_flight: Int, max_buffered_bytes: Int) -> FrameReassembler {
  FrameReassembler(max_in_flight, max_buffered_bytes, dict.new(), 0)
}

pub fn default() -> FrameReassembler {
  new(64, 64 * 1024 * 1024)
}

/// Messages currently awaiting more fragments.
pub fn in_flight_count(reassembler: FrameReassembler) -> Int {
  dict.size(reassembler.partials)
}

/// Accept one frame. `Ok(#(reassembler, Some(payload)))` when this frame
/// completes a message (or is `Whole`); `Ok(#(reassembler, None))` when a
/// fragment was buffered; `Error` on inconsistent or over-bound input (the
/// reference's `FrameException`) — never a partial delivery.
pub fn accept(
  reassembler: FrameReassembler,
  frame: ParsedFrame,
) -> Result(#(FrameReassembler, Option(BitArray)), String) {
  case frame.kind {
    Whole ->
      case bit_array.byte_size(frame.chunk) == frame.total_length {
        True -> Ok(#(reassembler, Some(frame.chunk)))
        False ->
          Error(
            "whole frame chunk "
            <> int.to_string(bit_array.byte_size(frame.chunk))
            <> " != total "
            <> int.to_string(frame.total_length),
          )
      }
    Fragment -> accept_fragment(reassembler, frame)
  }
}

fn accept_fragment(
  reassembler: FrameReassembler,
  frame: ParsedFrame,
) -> Result(#(FrameReassembler, Option(BitArray)), String) {
  use partial <- try(case dict.get(reassembler.partials, frame.message_id) {
    Error(_) ->
      case dict.size(reassembler.partials) >= reassembler.max_in_flight {
        True ->
          Error(
            "too many in-flight partial messages (max "
            <> int.to_string(reassembler.max_in_flight)
            <> ")",
          )
        False ->
          case
            reassembler.total_buffered + frame.total_length
            > reassembler.max_buffered_bytes
          {
            True ->
              Error(
                "reassembly buffer would exceed "
                <> int.to_string(reassembler.max_buffered_bytes)
                <> " bytes",
              )
            False ->
              Ok(Partial(frame.total_length, frame.frag_count, dict.new(), 0))
          }
      }
    Ok(existing) ->
      case
        existing.total_length != frame.total_length
        || existing.frag_count != frame.frag_count
      {
        True ->
          Error(
            "fragment metadata mismatch for message "
            <> int.to_string(frame.message_id)
            <> ": ("
            <> int.to_string(frame.total_length)
            <> ","
            <> int.to_string(frame.frag_count)
            <> ") vs ("
            <> int.to_string(existing.total_length)
            <> ","
            <> int.to_string(existing.frag_count)
            <> ")",
          )
        False -> Ok(existing)
      }
  })
  // A duplicate fragment (same index re-delivered) is silently ignored;
  // payload-level dedup is the ordering sublayer's job.
  let #(partial, added_bytes) = case dict.has_key(partial.chunks, frame.frag_index) {
    True -> #(partial, 0)
    False -> #(
      Partial(
        ..partial,
        chunks: dict.insert(partial.chunks, frame.frag_index, frame.chunk),
        buffered_bytes: partial.buffered_bytes
          + bit_array.byte_size(frame.chunk),
      ),
      bit_array.byte_size(frame.chunk),
    )
  }
  let reassembler =
    FrameReassembler(
      ..reassembler,
      partials: dict.insert(reassembler.partials, frame.message_id, partial),
      total_buffered: reassembler.total_buffered + added_bytes,
    )
  case dict.size(partial.chunks) < partial.frag_count {
    True -> Ok(#(reassembler, None))
    False -> {
      // Concatenate chunks 0..frag_count-1 and verify the advertised total.
      use payload <- try(
        list.try_fold(
          upto(partial.frag_count),
          <<>>,
          fn(acc, i) {
            case dict.get(partial.chunks, i) {
              Ok(chunk) -> Ok(bit_array.append(acc, chunk))
              Error(_) ->
                Error(
                  "missing fragment "
                  <> int.to_string(i)
                  <> " at completion of message "
                  <> int.to_string(frame.message_id),
                )
            }
          },
        ),
      )
      case bit_array.byte_size(payload) == partial.total_length {
        False ->
          Error(
            "reassembled "
            <> int.to_string(bit_array.byte_size(payload))
            <> " bytes != advertised total "
            <> int.to_string(partial.total_length),
          )
        True ->
          Ok(#(
            FrameReassembler(
              ..reassembler,
              partials: dict.delete(reassembler.partials, frame.message_id),
              total_buffered: reassembler.total_buffered
                - partial.buffered_bytes,
            ),
            Some(payload),
          ))
      }
    }
  }
}

fn try(
  result: Result(a, String),
  continue: fn(a) -> Result(b, String),
) -> Result(b, String) {
  case result {
    Ok(value) -> continue(value)
    Error(reason) -> Error(reason)
  }
}

fn upto(n: Int) -> List(Int) {
  list.index_map(list.repeat(0, n), fn(_, i) { i })
}
