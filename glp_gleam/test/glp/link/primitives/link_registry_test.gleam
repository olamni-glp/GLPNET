//// Tests for glp/link/primitives/link_registry (T076) — idempotency at link identity
//// (FR-007): put/contains, get_or_establish reuse, replace, and remove (distributed
//// GC).

import gleeunit/should
import glp/link/primitives/link_handle
import glp/link/primitives/link_registry
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme

fn id_a() {
  LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1))
}

fn id_b() {
  LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(2))
}

fn handle_for(id) {
  link_handle.new(id, link_options.default())
}

pub fn put_then_contains_test() {
  let assert Ok(reg) =
    link_registry.put(link_registry.new(), id_a(), handle_for(id_a()))
  link_registry.contains(reg, id_a())
  |> should.be_true
  link_registry.contains(reg, id_b())
  |> should.be_false
  link_registry.count(reg)
  |> should.equal(1)
}

pub fn put_over_existing_id_errors_test() {
  let assert Ok(reg) =
    link_registry.put(link_registry.new(), id_a(), handle_for(id_a()))
  // FR-007: a second establishment of the same identity is surfaced, not swallowed.
  link_registry.put(reg, id_a(), handle_for(id_a()))
  |> should.be_error
}

pub fn put_with_mismatched_id_errors_test() {
  link_registry.put(link_registry.new(), id_a(), handle_for(id_b()))
  |> should.be_error
}

pub fn get_or_establish_reuses_on_second_call_test() {
  let reg = link_registry.new()
  let assert Ok(#(reg, _h1)) =
    link_registry.get_or_establish(reg, id_a(), fn() { handle_for(id_a()) })
  // The second call must NOT run `establish` — reuse the stored handle (FR-007).
  let assert Ok(#(reg, _h2)) =
    link_registry.get_or_establish(reg, id_a(), fn() {
      panic as "establish must not run on reuse"
    })
  link_registry.count(reg)
  |> should.equal(1)
}

pub fn replace_advances_handle_test() {
  let assert Ok(reg) =
    link_registry.put(link_registry.new(), id_a(), handle_for(id_a()))
  let advanced = link_handle.mark_closed(handle_for(id_a()))
  let assert Ok(reg) = link_registry.replace(reg, id_a(), advanced)
  let assert Ok(h) = link_registry.try_get(reg, id_a())
  h.closed
  |> should.be_true
}

pub fn replace_unestablished_errors_test() {
  link_registry.replace(link_registry.new(), id_a(), handle_for(id_a()))
  |> should.be_error
}

pub fn remove_reports_existence_test() {
  let assert Ok(reg) =
    link_registry.put(link_registry.new(), id_a(), handle_for(id_a()))
  let #(reg, existed) = link_registry.remove(reg, id_a())
  existed
  |> should.be_true
  link_registry.contains(reg, id_a())
  |> should.be_false
}
