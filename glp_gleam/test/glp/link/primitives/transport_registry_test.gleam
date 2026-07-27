//// Tests for glp/link/primitives/transport_registry (T076) — scheme→leaf selection
//// (FR-013): register the loopback leaf, select it, miss an unregistered scheme, and
//// reject an ambiguous double-registration.

import gleeunit/should
import glp/link/primitives/transport_registry
import glp/link/seam/link_scheme
import glp/link/transports/loopback

pub fn register_and_select_loopback_test() {
  let assert Ok(reg) =
    transport_registry.register(transport_registry.new(), loopback.new())
  transport_registry.select(reg, link_scheme.loopback())
  |> should.be_ok
}

pub fn select_unregistered_scheme_errors_test() {
  transport_registry.select(transport_registry.new(), link_scheme.tcp())
  |> should.be_error
}

pub fn double_register_conflicting_leaf_errors_test() {
  let assert Ok(reg) =
    transport_registry.register(transport_registry.new(), loopback.new())
  // A DIFFERENT loopback leaf for the already-served "loopback" scheme is the
  // ambiguous-configuration error.
  transport_registry.register(reg, loopback.new())
  |> should.be_error
}

pub fn schemes_lists_registered_test() {
  let assert Ok(reg) =
    transport_registry.register(transport_registry.new(), loopback.new())
  transport_registry.schemes(reg)
  |> should.equal(["loopback"])
}
