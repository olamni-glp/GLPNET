//// glp/mad/global_writers_table tests (feature 050 T050.A0) — W_p lifecycle
//// (spec §3/§4.1/§5/§13): single never-reused counter, direct-index globalize
//// lookup, search-by-(agent,index) localize, created-exactly-once, permanent
//// index-0 serializer.

import gleam/option.{None, Some}
import gleeunit/should
import glp/mad/global_writers_table.{GlobalizeEntry, LocalizeEntry} as wt
import glp/runtime/terms.{ConstAtom, ConstTerm}

fn p() {
  ConstTerm(ConstAtom("p"))
}

fn q() {
  ConstTerm(ConstAtom("q"))
}

pub fn new_starts_at_one_test() {
  let t = wt.new(p())
  wt.next_index(t) |> should.equal(1)
  wt.serializer_addr(t) |> should.equal(None)
  wt.globalize_count(t) |> should.equal(0)
  wt.localize_count(t) |> should.equal(0)
  wt.owner(t) |> should.equal(p())
}

pub fn add_globalize_allocates_and_looks_up_test() {
  let t = wt.new(p())
  let #(t, i1) = wt.add_globalize(t, 100, q())
  let #(t, i2) = wt.add_globalize(t, 200, q())
  i1 |> should.equal(1)
  i2 |> should.equal(2)
  wt.lookup(t, 1) |> should.equal(Ok(GlobalizeEntry(100, q())))
  wt.lookup(t, 2) |> should.equal(Ok(GlobalizeEntry(200, q())))
  wt.lookup(t, 3) |> should.equal(Error(Nil))
  wt.globalize_count(t) |> should.equal(2)
}

pub fn single_counter_shared_test() {
  // spec §3.2: ONE counter shared across Globalize (entry) and allocate_index (no entry).
  let t = wt.new(p())
  let #(t, i1) = wt.add_globalize(t, 100, q())
  let #(t, i2) = wt.allocate_index(t)
  let #(t, i3) = wt.add_globalize(t, 300, q())
  #(i1, i2, i3) |> should.equal(#(1, 2, 3))
  // allocate_index created NO entry (spec §5.1 reader branch)
  wt.lookup(t, 2) |> should.equal(Error(Nil))
  wt.globalize_count(t) |> should.equal(2)
}

pub fn index_never_reused_after_remove_test() {
  // spec §13 Index Uniqueness: indices never reused, even after removal.
  let t = wt.new(p())
  let #(t, i1) = wt.add_globalize(t, 100, q())
  let t = wt.remove_globalize(t, i1)
  wt.lookup(t, i1) |> should.equal(Error(Nil))
  let #(_, i2) = wt.add_globalize(t, 200, q())
  i2 |> should.equal(2)
}

pub fn localize_add_find_dup_test() {
  let t = wt.new(p())
  let assert Ok(t) = wt.add_localize(t, 500, q(), 7)
  wt.find_localize(t, q(), 7)
  |> should.equal(Ok(LocalizeEntry(500, q(), 7)))
  wt.localize_count(t) |> should.equal(1)
  // duplicate (q,7) rejected (spec §13 created-exactly-once)
  wt.add_localize(t, 999, q(), 7) |> should.equal(Error(Nil))
  // non-existent remote index
  wt.find_localize(t, q(), 8) |> should.equal(Error(Nil))
}

pub fn localize_remove_test() {
  let t = wt.new(p())
  let assert Ok(t) = wt.add_localize(t, 500, q(), 7)
  let t = wt.remove_localize(t, q(), 7)
  wt.find_localize(t, q(), 7) |> should.equal(Error(Nil))
  wt.localize_count(t) |> should.equal(0)
}

pub fn serializer_init_and_update_test() {
  let t = wt.new(p())
  let t = wt.init_serializer(t, 42)
  wt.serializer_addr(t) |> should.equal(Some(42))
  // spec §8.3: Receive extends the stream and advances the serializer writer.
  let t = wt.update_serializer(t, 43)
  wt.serializer_addr(t) |> should.equal(Some(43))
}

pub fn remove_globalize_index_zero_is_noop_test() {
  // index 0 is the permanent serializer — remove_globalize(0) is a no-op (spec §4.1).
  let t = wt.new(p())
  let #(t, _) = wt.add_globalize(t, 100, q())
  let t = wt.remove_globalize(t, 0)
  wt.globalize_count(t) |> should.equal(1)
}
