//// glp/link/reliability/inbound_ordering — per-link inbound FIFO reconstruction
//// + transport-level dedup (wave-3 US4, T036; FR-021, contract rule 4).
////
//// Port of `glp_runtime/lib/link/reliability/inbound_ordering.dart` (mirror C#
//// `InboundOrdering`): a single sequence number DETECTS disorder but does not
//// RESTORE it, so out-of-order frames are held in a reorder buffer until the gap
//// fills, then released in sequence order. A frame at or below the delivered
//// high-water mark, or one already buffered, is an idempotent no-op — the
//// transport-level half of at-least-once redelivery. The reorder buffer is
//// BOUNDED: a peer cannot force unbounded memory by withholding one early frame
//// while flooding later ones (over-bound input errors loudly).
////
//// One instance per link per direction, driven by that link's single receive
//// loop. Immutable — `accept` returns the advanced state.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list

const mask_u32 = 0xFFFFFFFF

pub opaque type InboundOrdering {
  InboundOrdering(
    next_expected: Int,
    buffer: Dict(Int, BitArray),
    max_buffered: Int,
  )
}

/// A fresh ordering: `start` must match the peer's sequencer start (0 in the
/// reference); `max_buffered` caps out-of-order frames held awaiting a gap
/// (reference default 256).
pub fn new(start: Int, max_buffered: Int) -> InboundOrdering {
  InboundOrdering(start, dict.new(), max_buffered)
}

/// The reference defaults (`InboundOrdering()`): start 0, 256-frame bound.
pub fn default() -> InboundOrdering {
  new(0, 256)
}

/// The next in-order sequence number awaited.
pub fn next_expected(ordering: InboundOrdering) -> Int {
  ordering.next_expected
}

/// Out-of-order frames currently buffered awaiting a gap.
pub fn buffered_count(ordering: InboundOrdering) -> Int {
  dict.size(ordering.buffer)
}

/// Accept one sequenced payload. `Ok(#(ordering, deliverable))` — the payloads
/// now deliverable IN ORDER (empty when the input was a duplicate/old frame, or
/// a future frame buffered awaiting the gap). `Error` when buffering would
/// exceed the bound (the reference's `FrameException`).
pub fn accept(
  ordering: InboundOrdering,
  seq: Int,
  payload: BitArray,
) -> Result(#(InboundOrdering, List(BitArray)), String) {
  case seq < ordering.next_expected {
    // Already delivered → idempotent no-op.
    True -> Ok(#(ordering, []))
    False ->
      case seq == ordering.next_expected {
        True -> {
          // Deliver, then drain any contiguous buffered run behind the gap.
          let #(next, buffer, ready) =
            drain(
              int.bitwise_and(ordering.next_expected + 1, mask_u32),
              ordering.buffer,
              [payload],
            )
          Ok(#(InboundOrdering(..ordering, next_expected: next, buffer: buffer), ready))
        }
        False ->
          // Future frame: buffer it, deduping a re-delivered future.
          case dict.has_key(ordering.buffer, seq) {
            True -> Ok(#(ordering, []))
            False ->
              case dict.size(ordering.buffer) >= ordering.max_buffered {
                True ->
                  Error(
                    "reorder buffer full (max "
                    <> int.to_string(ordering.max_buffered)
                    <> "); missing seq "
                    <> int.to_string(ordering.next_expected),
                  )
                False ->
                  Ok(#(
                    InboundOrdering(
                      ..ordering,
                      buffer: dict.insert(ordering.buffer, seq, payload),
                    ),
                    [],
                  ))
              }
          }
      }
  }
}

fn drain(
  next: Int,
  buffer: Dict(Int, BitArray),
  acc: List(BitArray),
) -> #(Int, Dict(Int, BitArray), List(BitArray)) {
  case dict.get(buffer, next) {
    Error(_) -> #(next, buffer, list.reverse(acc))
    Ok(payload) ->
      drain(
        int.bitwise_and(next + 1, mask_u32),
        dict.delete(buffer, next),
        [payload, ..acc],
      )
  }
}
