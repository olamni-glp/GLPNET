//// glp/link/reliability/inbound_ordering — per-link inbound FIFO reconstruction +
//// transport-level dedup (feature 059, T077 — port of glp_runtime/lib/link/
//// reliability/inbound_ordering.dart, mirror csharp/glp_link/reliability/
//// InboundOrdering.cs).
////
//// A single sequence number DETECTS disorder but does not RESTORE it, so a reorder
//// buffer is required (architecture-context.md §4.2): out-of-order frames are held
//// until the gap fills, then released in sequence order (FR-020/023/053). A frame at
//// or below the delivered high-water mark, or one already buffered, is an idempotent
//// no-op — the transport-level half of at-least-once redelivery (FR-021/027), upstream
//// of the global-name dedup gate in the madGLP Receive. The reorder buffer is bounded
//// (FR-028): a peer cannot force unbounded memory by withholding one early frame while
//// flooding later ones. One instance per link per direction.
////
//// GLEAM MAPPING NOTE: the Dart buffer mutates in place; here it is an IMMUTABLE value
//// threaded through `accept`, which returns the deliverable-in-order payloads WITH the
//// advanced ordering.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import glp/link/reliability/frame_exception.{type FrameException, FrameException}

const mask = 0xFFFFFFFF

pub opaque type InboundOrdering {
  InboundOrdering(
    next_expected: Int,
    buffer: Dict(Int, BitArray),
    max_buffered: Int,
  )
}

/// An ordering starting at `start` (must match the peer's sequencer start) with a
/// `max_buffered` cap on out-of-order frames held awaiting a gap.
pub fn with_config(start: Int, max_buffered: Int) -> InboundOrdering {
  InboundOrdering(next_expected: start, buffer: dict.new(), max_buffered: max_buffered)
}

/// An ordering starting at 0 with the default 256-frame reorder bound.
pub fn new() -> InboundOrdering {
  with_config(0, 256)
}

/// The next in-order sequence number awaited.
pub fn next_expected(ordering: InboundOrdering) -> Int {
  ordering.next_expected
}

/// Out-of-order frames currently buffered awaiting a gap.
pub fn buffered_count(ordering: InboundOrdering) -> Int {
  dict.size(ordering.buffer)
}

/// Accept one sequenced payload. Returns the ordering + the payloads now deliverable
/// IN ORDER (empty when the input was a duplicate/old frame, or a future frame
/// buffered awaiting its gap). `Error(FrameException)` when buffering would exceed the
/// bound (FR-028).
pub fn accept(
  ordering: InboundOrdering,
  seq: Int,
  payload: BitArray,
) -> Result(#(InboundOrdering, List(BitArray)), FrameException) {
  case seq < ordering.next_expected {
    // Already delivered (seq < high-water) → idempotent no-op.
    True -> Ok(#(ordering, []))
    False ->
      case seq == ordering.next_expected {
        True -> {
          // Deliver this frame, then drain any contiguous buffered run.
          let #(ordering, ready) =
            drain(
              InboundOrdering(..ordering, next_expected: advance(ordering.next_expected)),
              [payload],
            )
          Ok(#(ordering, ready))
        }
        False ->
          // Future frame: buffer it, deduping a re-delivered future; bound-check.
          case dict.has_key(ordering.buffer, seq) {
            True -> Ok(#(ordering, []))
            False ->
              case dict.size(ordering.buffer) >= ordering.max_buffered {
                True ->
                  Error(FrameException(
                    "reorder buffer full (max "
                    <> int.to_string(ordering.max_buffered)
                    <> "); missing seq "
                    <> int.to_string(ordering.next_expected),
                  ))
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

/// Drain the contiguous run buffered ahead of `next_expected`, accumulating the ready
/// payloads (in order).
fn drain(
  ordering: InboundOrdering,
  ready_rev: List(BitArray),
) -> #(InboundOrdering, List(BitArray)) {
  case dict.get(ordering.buffer, ordering.next_expected) {
    Ok(payload) ->
      drain(
        InboundOrdering(
          ..ordering,
          buffer: dict.delete(ordering.buffer, ordering.next_expected),
          next_expected: advance(ordering.next_expected),
        ),
        [payload, ..ready_rev],
      )
    Error(Nil) -> #(ordering, list.reverse(ready_rev))
  }
}

fn advance(n: Int) -> Int {
  int.bitwise_and(n + 1, mask)
}
