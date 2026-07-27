//// Tests for glp/link/reliability/link_sequencer (T077) — the monotone outbound
//// sequence source.

import gleeunit/should
import glp/link/reliability/link_sequencer

pub fn next_advances_monotonically_test() {
  let s0 = link_sequencer.new()
  let #(s1, v0) = link_sequencer.next(s0)
  let #(s2, v1) = link_sequencer.next(s1)
  let #(_s3, v2) = link_sequencer.next(s2)
  #(v0, v1, v2)
  |> should.equal(#(0, 1, 2))
}

pub fn peek_does_not_advance_test() {
  let s0 = link_sequencer.with_start(5)
  link_sequencer.peek(s0)
  |> should.equal(5)
  let #(s1, v) = link_sequencer.next(s0)
  v
  |> should.equal(5)
  link_sequencer.peek(s1)
  |> should.equal(6)
}
