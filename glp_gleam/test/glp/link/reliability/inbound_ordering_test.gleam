//// Tests for glp/link/reliability/inbound_ordering (T077) — the reorder buffer + dedup
//// (FR-020/021/023/028).

import gleeunit/should
import glp/link/reliability/inbound_ordering

pub fn in_order_frames_deliver_immediately_test() {
  let o = inbound_ordering.new()
  let assert Ok(#(o, r0)) = inbound_ordering.accept(o, 0, <<1>>)
  r0 |> should.equal([<<1>>])
  let assert Ok(#(o, r1)) = inbound_ordering.accept(o, 1, <<2>>)
  r1 |> should.equal([<<2>>])
  inbound_ordering.next_expected(o) |> should.equal(2)
}

pub fn out_of_order_buffers_then_drains_test() {
  let o = inbound_ordering.new()
  // seq 1 arrives before seq 0 → buffered, nothing deliverable yet.
  let assert Ok(#(o, r1)) = inbound_ordering.accept(o, 1, <<2>>)
  r1 |> should.equal([])
  inbound_ordering.buffered_count(o) |> should.equal(1)
  // seq 2 arrives → still buffered.
  let assert Ok(#(o, r2)) = inbound_ordering.accept(o, 2, <<3>>)
  r2 |> should.equal([])
  // seq 0 fills the gap → the whole contiguous run drains in order.
  let assert Ok(#(o, r0)) = inbound_ordering.accept(o, 0, <<1>>)
  r0 |> should.equal([<<1>>, <<2>>, <<3>>])
  inbound_ordering.buffered_count(o) |> should.equal(0)
  inbound_ordering.next_expected(o) |> should.equal(3)
}

pub fn duplicate_old_frame_is_idempotent_noop_test() {
  let o = inbound_ordering.new()
  let assert Ok(#(o, _)) = inbound_ordering.accept(o, 0, <<1>>)
  // Re-delivery of an already-delivered seq → dropped.
  let assert Ok(#(o, r)) = inbound_ordering.accept(o, 0, <<1>>)
  r |> should.equal([])
  inbound_ordering.next_expected(o) |> should.equal(1)
}

pub fn duplicate_future_frame_is_idempotent_test() {
  let o = inbound_ordering.new()
  let assert Ok(#(o, _)) = inbound_ordering.accept(o, 2, <<3>>)
  // Same future seq re-delivered → not double-buffered.
  let assert Ok(#(o, r)) = inbound_ordering.accept(o, 2, <<3>>)
  r |> should.equal([])
  inbound_ordering.buffered_count(o) |> should.equal(1)
}

pub fn reorder_buffer_bound_is_enforced_test() {
  let o = inbound_ordering.with_config(0, 1)
  // One future frame fits (buffer size 1).
  let assert Ok(#(o, _)) = inbound_ordering.accept(o, 2, <<3>>)
  // A second distinct future frame would exceed the bound → clean error (FR-028).
  inbound_ordering.accept(o, 3, <<4>>)
  |> should.be_error
}
