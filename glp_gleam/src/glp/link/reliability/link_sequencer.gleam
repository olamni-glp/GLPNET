//// glp/link/reliability/link_sequencer — the per-link monotone outbound sequence
//// number (feature 059, T077 — port of glp_runtime/lib/link/reliability/
//// link_sequencer.dart, mirror csharp/glp_link/reliability/LinkSequencer.cs).
////
//// Assigns the per-link monotone outbound sequence (FR-020) that becomes a frame's
//// `message_id` — the dedup + reorder key on the receive side (`inbound_ordering`).
//// One sequencer per link per direction. It is the transport-level ordering key; the
//// never-reused `(agent,index)` global name inside the payload is the second half of
//// the dedup key (architecture-context.md §4.2). No wraparound handling within a
//// session (a session will not emit 2^32 frames; a reconnect starts a fresh sequence,
//// the global-name idempotency backstop covers cross-session replay).
////
//// GLEAM MAPPING NOTE: the Dart counter mutates in place; here it is an IMMUTABLE
//// value — `next` returns the sequence number WITH the advanced sequencer.

import gleam/int

const mask = 0xFFFFFFFF

pub opaque type LinkSequencer {
  LinkSequencer(next: Int)
}

/// A sequencer starting at `start` (default 0 via `new`).
pub fn with_start(start: Int) -> LinkSequencer {
  LinkSequencer(next: start)
}

/// A sequencer starting at 0.
pub fn new() -> LinkSequencer {
  LinkSequencer(next: 0)
}

/// The next sequence number, with the advanced sequencer (wraps at 2^32).
pub fn next(seq: LinkSequencer) -> #(LinkSequencer, Int) {
  let value = seq.next
  #(LinkSequencer(next: int.bitwise_and(seq.next + 1, mask)), value)
}

/// The value `next` will return (no advance).
pub fn peek(seq: LinkSequencer) -> Int {
  seq.next
}
