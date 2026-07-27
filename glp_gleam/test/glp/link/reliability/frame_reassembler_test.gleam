//// Tests for glp/link/reliability/frame_reassembler (T077) — multi-frame reassembly
//// (FR-022/028), driven by real frames from frame_codec.

import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/reliability/frame_codec
import glp/link/reliability/frame_reassembler

/// An ~80-byte payload — large enough to fragment under a small MTU.
fn payload() -> BitArray {
  <<"the quick brown fox jumps over the lazy dog, then does it again twice more!":utf8>>
}

/// Encode `p` as frames under `max_frame_bytes` and parse each back to a ParsedFrame.
fn frames(p: BitArray, message_id: Int, max: option.Option(Int)) {
  let assert Ok(encoded) = frame_codec.encode(p, message_id, max)
  list.map(encoded, fn(bytes) {
    let assert Ok(frame) = frame_codec.parse_frame(bytes)
    frame
  })
}

pub fn whole_frame_passes_through_immediately_test() {
  let p = payload()
  let assert [whole] = frames(p, 0, None)
  let assert Ok(#(_r, out)) =
    frame_reassembler.accept(frame_reassembler.new(), whole)
  out |> should.equal(Some(p))
}

pub fn multi_fragment_reassembles_to_original_test() {
  let p = payload()
  // Force fragmentation with a small MTU.
  let parts = frames(p, 7, Some(24))
  // More than one fragment was produced.
  { list.length(parts) > 1 } |> should.be_true
  // Feed each fragment; only the last completes the message.
  let #(r, result) =
    list.fold(parts, #(frame_reassembler.new(), None), fn(acc, frame) {
      let #(r, _prev) = acc
      let assert Ok(#(r, out)) = frame_reassembler.accept(r, frame)
      #(r, out)
    })
  result |> should.equal(Some(p))
  // No partial messages remain in flight.
  frame_reassembler.in_flight_count(r) |> should.equal(0)
}

pub fn duplicate_fragment_is_idempotent_test() {
  let p = payload()
  let assert [first, ..] = frames(p, 3, Some(24))
  let r = frame_reassembler.new()
  let assert Ok(#(r, out1)) = frame_reassembler.accept(r, first)
  out1 |> should.equal(None)
  // Re-delivering the same fragment does not corrupt the partial or complete it.
  let assert Ok(#(r, out2)) = frame_reassembler.accept(r, first)
  out2 |> should.equal(None)
  frame_reassembler.in_flight_count(r) |> should.equal(1)
}

pub fn too_many_in_flight_messages_is_bounded_test() {
  // A reassembler that allows only ONE concurrent partial message.
  let r = frame_reassembler.with_bounds(1, 64 * 1024 * 1024)
  let assert [frag_a, ..] = frames(payload(), 100, Some(24))
  let assert [frag_b, ..] = frames(payload(), 200, Some(24))
  let assert Ok(#(r, None)) = frame_reassembler.accept(r, frag_a)
  // Opening a second concurrent partial message exceeds the bound (FR-028).
  frame_reassembler.accept(r, frag_b)
  |> should.be_error
}
