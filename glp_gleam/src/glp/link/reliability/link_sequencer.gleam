//// glp/link/reliability/link_sequencer — per-link monotone outbound sequence
//// (wave-3 US4, T036; FR-021).
////
//// Port of `glp_runtime/lib/link/reliability/link_sequencer.dart` (mirror C#
//// `LinkSequencer`): assigns the per-link, per-direction outbound sequence
//// number that becomes a frame's `MessageId` — the dedup + reorder key on the
//// receive side. No wraparound handling: a single link session will not emit
//// 2^32 messages, and a reconnect starts a fresh sequence.

import gleam/int

const mask_u32 = 0xFFFFFFFF

/// The sequence state (immutable — `next` returns the advanced sequencer).
pub opaque type LinkSequencer {
  LinkSequencer(next_value: Int)
}

/// A fresh sequencer starting at `start` (0 by default in the reference).
pub fn new(start: Int) -> LinkSequencer {
  LinkSequencer(int.bitwise_and(start, mask_u32))
}

/// The next sequence number, plus the advanced sequencer.
pub fn next(seq: LinkSequencer) -> #(LinkSequencer, Int) {
  #(LinkSequencer(int.bitwise_and(seq.next_value + 1, mask_u32)), seq.next_value)
}

/// The value `next` will return next (no side effect — Dart `peek`).
pub fn peek(seq: LinkSequencer) -> Int {
  seq.next_value
}
