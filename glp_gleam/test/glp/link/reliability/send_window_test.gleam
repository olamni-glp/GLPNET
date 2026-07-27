//// Tests for glp/link/reliability/send_window (T077) — bounded credit accounting
//// (FR-025).

import gleeunit/should
import glp/link/reliability/send_window.{SemaphoreFull}

pub fn new_window_starts_full_test() {
  let w = send_window.new()
  send_window.capacity(w) |> should.equal(8)
  send_window.available(w) |> should.equal(8)
  send_window.in_flight(w) |> should.equal(0)
}

pub fn acquire_and_release_accounting_test() {
  let w = send_window.with_window(2)
  let #(w, ok1) = send_window.try_acquire(w)
  ok1 |> should.be_true
  send_window.in_flight(w) |> should.equal(1)
  let #(w, ok2) = send_window.try_acquire(w)
  ok2 |> should.be_true
  send_window.available(w) |> should.equal(0)
  // Window full → a further acquire fails (the backpressure point).
  let #(w, ok3) = send_window.try_acquire(w)
  ok3 |> should.be_false
  // Release one credit → acquirable again.
  let assert Ok(w) = send_window.release(w)
  send_window.available(w) |> should.equal(1)
  let #(_w, ok4) = send_window.try_acquire(w)
  ok4 |> should.be_true
}

pub fn over_release_is_surfaced_test() {
  // Releasing a full window is a double-ack bug — surfaced, not silently corrupt.
  send_window.release(send_window.with_window(2))
  |> should.equal(Error(SemaphoreFull))
}
