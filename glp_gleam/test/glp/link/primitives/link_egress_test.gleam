//// Tests for glp/link/primitives/link_egress (T076) — the ground-relay ship
//// pipeline: the ground gate (FR-010), payload serialization, framing + monotone
//// sequencing.

import gleam/list
import gleeunit/should
import glp/link/primitives/link_egress
import glp/link/primitives/link_handle
import glp/link/reliability/frame_codec
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/runtime/heap
import glp/runtime/terms.{ConstInt, ConstTerm, StructTerm, VarRef}

fn handle() {
  link_handle.new(
    LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1)),
    link_options.default(),
  )
}

pub fn serialize_ground_int_round_trips_via_frame_test() {
  // A ground term serializes to a payload that frames as one Whole frame the codec
  // parses back (the payload is the opaque TLV blob frame_codec wraps).
  let assert Ok(payload) = link_egress.serialize_ground(ConstTerm(ConstInt(10)))
  let assert Ok([frame]) = frame_codec.encode(payload, 0, link_options.default().max_frame_bytes)
  frame_codec.parse_frame(frame)
  |> should.be_ok
}

pub fn ship_ground_frames_and_advances_seq_test() {
  let assert Ok(#(handle2, frames)) =
    link_egress.ship_ground(heap.new(), handle(), ConstTerm(ConstInt(10)))
  // At least one frame shipped, and the outbound sequence advanced (0 → 1).
  { list.length(frames) >= 1 }
  |> should.be_true
  handle2.seq
  |> should.equal(1)
}

pub fn ship_ground_serializes_a_struct_test() {
  link_egress.ship_ground(
    heap.new(),
    handle(),
    StructTerm("pt", [ConstTerm(ConstInt(1)), ConstTerm(ConstInt(2))]),
  )
  |> should.be_ok
}

pub fn ship_ground_rejects_unbound_reader_test() {
  // An unbound cell reaching egress is the ground-relay gate violation (FR-010).
  let #(h, _writer, reader) = heap.allocate_variable(heap.new())
  link_egress.ship_ground(h, handle(), VarRef(reader))
  |> should.be_error
}
